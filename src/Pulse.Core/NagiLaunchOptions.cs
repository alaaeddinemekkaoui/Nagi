using System.Security.Cryptography;
using System.Text;

namespace Pulse.Core;

public sealed record NagiLaunchOptions(bool ShowSetup, string? DataDirectory, int? WaitForExit)
{
    public static NagiLaunchOptions Parse(IEnumerable<string> arguments)
    {
        var args = arguments.ToArray(); bool setup = false; string? directory = null; int? parent = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--setup": setup = true; break;
                case "--data-dir" when i + 1 < args.Length:
                    directory = args[++i];
                    if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("The data directory must be an absolute path.");
                    directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)); break;
                case "--wait-for-exit" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out var id) || id <= 0) throw new ArgumentException("Invalid restart process.");
                    parent = id; break;
                default: throw new ArgumentException("Unknown or incomplete Nagi launch option.");
            }
        }
        return new(setup, directory, parent);
    }
    public string Scope(string name)
    {
        var normal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pulse");
        if (DataDirectory is null || string.Equals(DataDirectory, normal, StringComparison.OrdinalIgnoreCase)) return name;
        return name + "." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(DataDirectory.ToUpperInvariant())))[..16];
    }
}
