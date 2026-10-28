using ModelContextProtocol;
using VsDebugMcp.VisualStudio;

namespace VsDebugMcp.Tests;

public class InstanceSelectorTests
{
    private static readonly VsMoniker Vs2022 = new(17, 100);
    private static readonly VsMoniker Vs2026 = new(18, 200);

    [Fact]
    public void PicksTheOnlyRunningInstance() => Assert.Equal(Vs2022, InstanceSelector.Select([Vs2022], null));

    [Fact]
    public void PicksTheSelectedInstance() => Assert.Equal(Vs2026, InstanceSelector.Select([Vs2022, Vs2026], 200));

    [Fact]
    public void FailsWhenNothingIsRunning()
    {
        var error = Assert.Throws<McpException>(() => InstanceSelector.Select([], null));

        Assert.Contains("No running Visual Studio", error.Message);
    }

    [Fact]
    public void AsksToSelectWhenSeveralAreRunning()
    {
        var error = Assert.Throws<McpException>(() => InstanceSelector.Select([Vs2022, Vs2026], null));

        Assert.Contains("pid 100", error.Message);
        Assert.Contains("pid 200", error.Message);
        Assert.Contains("vs_select_instance", error.Message);
    }

    [Fact]
    public void FailsWhenTheSelectedInstanceIsGone()
    {
        var error = Assert.Throws<McpException>(() => InstanceSelector.Select([Vs2022], 200));

        Assert.Contains("pid 200", error.Message);
    }
}
