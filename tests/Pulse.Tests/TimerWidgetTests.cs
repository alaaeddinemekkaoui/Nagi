using Pulse.Core;
using Xunit;

namespace Pulse.Tests;

public sealed class TimerWidgetTests
{
    [Fact]
    public void TimerWidgetReservesItsSlotBeforeStartingAndRunningTimersRemainIndependent()
    {
        var idle = new ProfileWorkspace(Profile.Work, [], ["Timer"]);
        var hidden = idle with { Widgets = [] };
        foreach (var horizontal in new[] { true, false })
        {
            var visible = DockPresentation.RailExtent(idle, horizontal, false, false, null);
            var running = DockPresentation.RailExtent(hidden, horizontal, false, false, null, true);
            var stopped = DockPresentation.RailExtent(hidden, horizontal, false, false, null);
            Assert.Equal(visible, running);
            Assert.Equal(horizontal ? 52 : 42, visible - stopped);
        }
    }
}
