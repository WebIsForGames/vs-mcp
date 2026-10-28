using VsDebugMcp.VisualStudio;

namespace VsDebugMcp.Tests;

public class DebuggerServiceTests
{
    [Theory]
    [InlineData(-5, 1)]
    [InlineData(0, 1)]
    [InlineData(60, 60)]
    [InlineData(300, 300)]
    [InlineData(1000, 300)]
    public void ClampsWaitTimeout(int requestedSeconds, int expectedSeconds) =>
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), DebuggerService.ClampWaitTimeout(requestedSeconds));
}
