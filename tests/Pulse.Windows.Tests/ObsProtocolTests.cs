using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pulse.Core;
using Pulse.Windows;
using Xunit;

namespace Pulse.Windows.Tests;

public sealed class ObsProtocolTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task ObsV5HandshakeAuthenticationAndReadOnlyStatusRequestsWork(bool authenticated)
    {
        using var cancellation = new CancellationTokenSource(6000);
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = ServeAsync(listener, authenticated, false, cancellation.Token);
        var result = await new ObsService().SampleAsync(true, port, authenticated ? "secret" : "", cancellation.Token);
        Assert.True(result.Connected, result.Status); Assert.True(result.Recording); Assert.False(result.Streaming); Assert.Equal(60, result.RenderFps);
        var requests = await server; Assert.Equal(["GetVersion", "GetStats", "GetRecordStatus", "GetStreamStatus"], requests);
    }
    [Fact] public async Task IncorrectObsPasswordIsReportedWithoutLeakingThePassword()
    {
        using var cancellation = new CancellationTokenSource(6000);
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var server = ServeAsync(listener, true, true, cancellation.Token);
        var result = await new ObsService().SampleAsync(true, ((IPEndPoint)listener.LocalEndpoint).Port, "wrong-secret", cancellation.Token);
        Assert.False(result.Connected); Assert.Contains("password", result.Status); Assert.DoesNotContain("wrong-secret", result.Status); await server;
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task RecordingSuccessRequiresObservedOutputStateRatherThanOnlyAnAcknowledgement(bool stateChanges)
    {
        using var cancellation = new CancellationTokenSource(8000); using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var server = ServeAsync(listener, false, false, cancellation.Token, stateChanges);
        var result = await new ObsService().ToggleRecordingAsync(((IPEndPoint)listener.LocalEndpoint).Port, "", cancellation.Token);
        Assert.Equal(stateChanges, result.Success); await server;
    }
    [Fact] public async Task ConnectedObsExposesScenesAndCurrentProgramScene()
    {
        using var cancellation = new CancellationTokenSource(8000); using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var server = ServeAsync(listener, false, false, cancellation.Token, output: "Scene");
        var result = await new ObsService().SampleAsync(true, ((IPEndPoint)listener.LocalEndpoint).Port, "", cancellation.Token);
        Assert.True(result.Connected); Assert.Equal(["Starting", "Game", "Break"], result.Scenes); Assert.Equal("Starting", result.CurrentScene);
        Assert.Contains("GetSceneList", await server);
    }
    [Theory] [InlineData(false, true)] [InlineData(true, true)] [InlineData(false, false)]
    public async Task StreamingStartAndStopRequireObservedState(bool initiallyActive, bool stateChanges)
    {
        using var cancellation = new CancellationTokenSource(8000); using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var server = ServeAsync(listener, false, false, cancellation.Token, stateChanges, "Stream", initiallyActive);
        var result = await new ObsService().ToggleStreamingAsync(((IPEndPoint)listener.LocalEndpoint).Port, "", cancellation.Token);
        Assert.Equal(stateChanges, result.Success);
        var requests = await server; Assert.Contains(initiallyActive ? "StopStream" : "StartStream", requests);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task SceneSwitchSendsSelectedNameAndVerifiesProgramScene(bool stateChanges)
    {
        using var cancellation = new CancellationTokenSource(8000); using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var server = ServeAsync(listener, false, false, cancellation.Token, stateChanges, "Scene");
        var result = await new ObsService().SwitchSceneAsync(((IPEndPoint)listener.LocalEndpoint).Port, "", "Game", cancellation.Token);
        Assert.Equal(stateChanges, result.Success);
        Assert.Contains("SetCurrentProgramScene", await server);
    }
    private static async Task<string[]> ServeAsync(TcpListener listener, bool authenticated, bool reject, CancellationToken token, bool? recordingTest = null, string output = "Record", bool initiallyActive = false)
    {
        using var client = await listener.AcceptTcpClientAsync(token); using var stream = client.GetStream();
        var header = new StringBuilder(); var single = new byte[1];
        while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal) && header.Length < 8192)
        { if (await stream.ReadAsync(single, token) == 0) throw new IOException(); header.Append((char)single[0]); }
        var key = header.ToString().Split("\r\n").Single(x => x.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase)).Split(':', 2)[1].Trim();
        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n"), token);
        using var ws = WebSocket.CreateFromStream(stream, true, null, TimeSpan.FromSeconds(10));
        object data = authenticated ? new { rpcVersion = 1, authentication = new { salt = "salt", challenge = "challenge" } } : new { rpcVersion = 1 };
        await Send(ws, new { op = 0, d = data }, token);
        var identify = await Read(ws, token); Assert.Equal(1, identify.GetProperty("op").GetInt32());
        if (reject) { await ws.CloseOutputAsync(WebSocketCloseStatus.PolicyViolation, "Authentication failed", token); return []; }
        if (authenticated) Assert.Equal(IntegrationMath.ObsAuthentication("secret", "salt", "challenge"), identify.GetProperty("d").GetProperty("authentication").GetString());
        await Send(ws, new { op = 2, d = new { negotiatedRpcVersion = 1 } }, token);
        var requests = new List<string>();
        var recording = initiallyActive; var currentScene = "Starting";
        for (var i = 0; i < (recordingTest == false ? 18 : output == "Scene" && recordingTest is null ? 5 : 4); i++)
        {
            var request = (await Read(ws, token)).GetProperty("d"); var type = request.GetProperty("requestType").GetString()!; requests.Add(type);
            if (type == "Start" + output && recordingTest == true) recording = true;
            if (type == "Stop" + output && recordingTest == true) recording = false;
            if (type == "SetCurrentProgramScene")
            {
                Assert.Equal("Game", request.GetProperty("requestData").GetProperty("sceneName").GetString());
                if (recordingTest == true) currentScene = "Game";
            }
            object response = type switch
            {
                "GetVersion" => new { availableRequests = new[] { "GetStats", "GetRecordStatus", "GetStreamStatus", "StartRecord", "StopRecord", "StartStream", "StopStream" }.Concat(output == "Scene" ? new[] { "GetSceneList", "SetCurrentProgramScene" } : []).ToArray() },
                "GetSceneList" => new { currentProgramSceneName = currentScene, scenes = new[] { new { sceneName = "Starting" }, new { sceneName = "Game" }, new { sceneName = "Break" } } },
                "GetStreamStatus" => new { outputActive = output == "Stream" && recording },
                "GetStats" => new { activeFps = 60 }, "GetRecordStatus" => new { outputActive = recordingTest.HasValue ? recording : true }, _ => new { outputActive = false }
            };
            await Send(ws, new { op = 7, d = new { requestId = request.GetProperty("requestId").GetString(), requestStatus = new { result = true, code = 100 }, responseData = response } }, token);
        }
        return requests.ToArray();
    }
    private static async Task Send(WebSocket ws, object value, CancellationToken token) => await ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)), WebSocketMessageType.Text, true, token);
    private static async Task<JsonElement> Read(WebSocket ws, CancellationToken token)
    {
        var data = new byte[8192]; using var message = new MemoryStream(); WebSocketReceiveResult result;
        do { result = await ws.ReceiveAsync(new ArraySegment<byte>(data), token); message.Write(data, 0, result.Count); } while (!result.EndOfMessage);
        using var doc = JsonDocument.Parse(message.ToArray()); return doc.RootElement.Clone();
    }
}
