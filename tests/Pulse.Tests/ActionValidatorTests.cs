using Pulse.Core;
using Xunit;

namespace Pulse.Tests;

public sealed class ActionValidatorTests
{
    [Theory]
    [InlineData("https://example.com", true)] [InlineData("http://localhost:1234", true)]
    [InlineData("javascript:alert(1)", false)] [InlineData("file:///C:/Windows", false)] [InlineData("ms-settings:privacy", false)]
    [InlineData("https://user:password@example.com", false)] [InlineData("not a url", false)]
    public void UrlSchemesAreRestricted(string target, bool valid) => Assert.Equal(valid, ActionValidator.Validate(new("Test", ActionKind.Url, target)).Success);
    [Theory] [InlineData("cmd.exe /c calc")] [InlineData("relative.exe")] [InlineData(@"\\server\share\app.exe")]
    public void CommandLinesRelativePathsAndNetworkPathsAreRejected(string target) => Assert.False(ActionValidator.Validate(new("Test", ActionKind.Application, target)).Success);
    [Theory] [InlineData("Work", true)] [InlineData("Gaming", true)] [InlineData("Focus", true)] [InlineData("Automatic", false)] [InlineData("99", false)]
    public void ProfileActionsRequireARealProfile(string target, bool valid) => Assert.Equal(valid, ActionValidator.Validate(new("Test", ActionKind.Profile, target)).Success);
    [Fact] public void ScriptsAndIndirectLaunchersAreRejectedEvenWhenTheyExist()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            foreach (var name in new[] { "run.ps1", "run.cmd", "link.lnk", "page.hta", "script.js", "powershell.exe", "cmd.exe" })
            {
                var path = Path.Combine(directory, name); File.WriteAllText(path, "");
                Assert.False(ActionValidator.Validate(new("Test", name.EndsWith(".exe") ? ActionKind.Application : ActionKind.File, path)).Success);
            }
            var safe = Path.Combine(directory, "notes.txt"); File.WriteAllText(safe, "");
            Assert.True(ActionValidator.Validate(new("Notes", ActionKind.File, safe)).Success);
            Assert.True(ActionValidator.Validate(new("Folder", ActionKind.Folder, directory)).Success);
        }
        finally { Directory.Delete(directory, true); }
    }
}
