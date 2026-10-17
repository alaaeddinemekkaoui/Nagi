using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Pulse.Core;

namespace Pulse.Windows;

public sealed class ObsService
{
    public async Task<ObsSnapshot> SampleAsync(bool enabled, int port, string password, CancellationToken token)
    {
        if (!enabled) return new(false, false, false, null, "OBS connection off");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(3000);
            using var session = await Session.ConnectAsync(port, password, timeout.Token);
            var stats = await session.RequestAsync("GetStats", timeout.Token);
            var record = await session.RequestAsync("GetRecordStatus", timeout.Token);
            var stream = await session.RequestAsync("GetStreamStatus", timeout.Token);
            var fps = stats.TryGetProperty("activeFps", out var f) && f.TryGetDouble(out var value) && double.IsFinite(value) ? value : (double?)null;
            var scenes = session.Supports("GetSceneList") ? await session.RequestAsync("GetSceneList", timeout.Token) : default;
            return new(true, record.GetProperty("outputActive").GetBoolean(), stream.GetProperty("outputActive").GetBoolean(), fps, "OBS WebSocket connected")
            {
                Scenes = scenes.ValueKind == JsonValueKind.Object && scenes.TryGetProperty("scenes", out var list)
                    ? list.EnumerateArray().Select(s => s.GetProperty("sceneName").GetString()!).Where(n => !string.IsNullOrEmpty(n)).ToArray() : [],
                CurrentScene = scenes.ValueKind == JsonValueKind.Object && scenes.TryGetProperty("currentProgramSceneName", out var scene) ? scene.GetString() : null
            };
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return new(false, false, false, null, "OBS connection timed out"); }
        catch (Exception ex) when (ex is WebSocketException or JsonException or InvalidDataException or ObsAuthenticationException or InvalidOperationException or KeyNotFoundException)
        { return new(false, false, false, null, ex is ObsAuthenticationException ? "OBS password required or incorrect" : "OBS unavailable · check WebSocket server and port"); }
    }
    public Task<OperationResult> ToggleRecordingAsync(int port, string password, CancellationToken token) => ToggleOutputAsync(port, password, false, token);
    public Task<OperationResult> ToggleStreamingAsync(int port, string password, CancellationToken token) => ToggleOutputAsync(port, password, true, token);
    private async Task<OperationResult> ToggleOutputAsync(int port, string password, bool streaming, CancellationToken token)
    {
        var label = streaming ? "streaming" : "recording";
        var statusRequest = streaming ? "GetStreamStatus" : "GetRecordStatus";
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(5000);
            using var session = await Session.ConnectAsync(port, password, timeout.Token);
            var current = await session.RequestAsync(statusRequest, timeout.Token);
            var start = !current.GetProperty("outputActive").GetBoolean();
            await session.RequestAsync(streaming ? start ? "StartStream" : "StopStream" : start ? "StartRecord" : "StopRecord", timeout.Token);
            for (var attempt = 0; attempt < 15; attempt++)
            {
                await Task.Delay(150, timeout.Token);
                var state = await session.RequestAsync(statusRequest, timeout.Token);
                if (state.GetProperty("outputActive").GetBoolean() == start)
                    return OperationResult.Ok("OBS " + label + (start ? " started" : " stopped"));
            }
            return OperationResult.Fail("OBS did not change " + label + " state. Check its output setup.");
        }
        catch (Exception ex) when (ex is WebSocketException or JsonException or InvalidDataException or ObsAuthenticationException or InvalidOperationException or KeyNotFoundException or OperationCanceledException)
        { return OperationResult.Fail("OBS " + label + " could not be changed. Check the connection and output setup."); }
    }
    public async Task<OperationResult> SwitchSceneAsync(int port, string password, string scene, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(scene)) return OperationResult.Fail("Choose an OBS scene in Hardware & studio settings.");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(5000);
            using var session = await Session.ConnectAsync(port, password, timeout.Token);
            var scenes = await session.RequestAsync("GetSceneList", timeout.Token);
            if (!scenes.GetProperty("scenes").EnumerateArray().Any(s => s.GetProperty("sceneName").GetString() == scene))
                return OperationResult.Fail("That OBS scene is unavailable. Choose another scene in settings.");
            await session.RequestAsync("SetCurrentProgramScene", timeout.Token, requestData: new { sceneName = scene });
            for (var attempt = 0; attempt < 15; attempt++)
            {
                var current = await session.RequestAsync("GetSceneList", timeout.Token);
                if (current.GetProperty("currentProgramSceneName").GetString() == scene) return OperationResult.Ok("OBS scene: " + scene);
                await Task.Delay(150, timeout.Token);
            }
            return OperationResult.Fail("OBS did not switch scenes.");
        }
        catch (Exception ex) when (ex is WebSocketException or JsonException or InvalidDataException or ObsAuthenticationException or InvalidOperationException or KeyNotFoundException or OperationCanceledException)
        { return OperationResult.Fail("OBS scene could not be changed. Check the connection."); }
    }
    private sealed class ObsAuthenticationException : Exception { }
    private sealed class Session(ClientWebSocket socket) : IDisposable
    {
        private HashSet<string> _requests = [];
        public static async Task<Session> ConnectAsync(int port, string password, CancellationToken token)
        {
            if (port is < 1024 or > 65535) throw new InvalidDataException("Invalid OBS port.");
            var socket = new ClientWebSocket(); var session = new Session(socket);
            try
            {
                await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}"), token);
                var hello = await session.ReadAsync(token);
                if (hello.GetProperty("op").GetInt32() != 0) throw new InvalidDataException("Expected OBS Hello.");
                var identify = new Dictionary<string, object> { ["rpcVersion"] = 1, ["eventSubscriptions"] = 0 };
                if (hello.GetProperty("d").TryGetProperty("authentication", out var auth))
                {
                    if (password.Length == 0) throw new ObsAuthenticationException();
                    identify["authentication"] = IntegrationMath.ObsAuthentication(password, auth.GetProperty("salt").GetString()!, auth.GetProperty("challenge").GetString()!);
                }
                await session.WriteAsync(new { op = 1, d = identify }, token);
                var identified = await session.ReadAsync(token);
                if (identified.GetProperty("op").GetInt32() != 2) throw new ObsAuthenticationException();
                var version = await session.RequestAsync("GetVersion", token, false);
                if (version.TryGetProperty("availableRequests", out var requests)) session._requests = requests.EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);
                return session;
            }
            catch { session.Dispose(); throw; }
        }
        private async Task WriteAsync(object value, CancellationToken token) => await socket.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)).AsMemory(), WebSocketMessageType.Text, true, token);
        private async Task<JsonElement> ReadAsync(CancellationToken token)
        {
            var buffer = new byte[4096]; using var stream = new MemoryStream();
            while (true)
            {
                var result = await socket.ReceiveAsync(buffer.AsMemory(), token);
                if (result.MessageType == WebSocketMessageType.Close) throw new ObsAuthenticationException();
                if (result.MessageType != WebSocketMessageType.Text || stream.Length + result.Count > 128 * 1024) throw new InvalidDataException("Invalid OBS message.");
                stream.Write(buffer, 0, result.Count); if (result.EndOfMessage) break;
            }
            using var document = JsonDocument.Parse(stream.ToArray()); return document.RootElement.Clone();
        }
        public bool Supports(string type) => _requests.Contains(type);
        public async Task<JsonElement> RequestAsync(string type, CancellationToken token, bool check = true, object? requestData = null)
        {
            if (check && !_requests.Contains(type)) throw new InvalidDataException("Unsupported OBS request.");
            var id = Guid.NewGuid().ToString("N"); await WriteAsync(new { op = 6, d = new { requestType = type, requestId = id, requestData } }, token);
            for (var attempt = 0; attempt < 16; attempt++)
            {
                var response = await ReadAsync(token); if (response.GetProperty("op").GetInt32() != 7) continue;
                var data = response.GetProperty("d"); if (data.GetProperty("requestId").GetString() != id) continue;
                if (!data.GetProperty("requestStatus").GetProperty("result").GetBoolean()) throw new InvalidDataException("OBS request failed.");
                return data.TryGetProperty("responseData", out var result) ? result.Clone() : JsonSerializer.SerializeToElement(new { });
            }
            throw new InvalidDataException("No matching OBS response.");
        }
        public void Dispose() => socket.Dispose();
    }
}
