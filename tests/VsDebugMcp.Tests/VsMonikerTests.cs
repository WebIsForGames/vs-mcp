using VsDebugMcp.VisualStudio;

namespace VsDebugMcp.Tests;

public class VsMonikerTests
{
    [Theory]
    [InlineData("!VisualStudio.DTE.17.0:1234", 17, 1234, "2022")]
    [InlineData("!VisualStudio.DTE.18.0:42", 18, 42, "2026")]
    public void ParsesSupportedVersions(string displayName, int majorVersion, int processId, string productYear)
    {
        var moniker = VsMoniker.ParseSupported(displayName);

        Assert.Equal(new VsMoniker(majorVersion, processId), moniker);
        Assert.Equal(productYear, moniker!.ProductYear);
    }

    [Theory]
    [InlineData("!VisualStudio.DTE.16.0:1234")]
    [InlineData("!VisualStudio.DTE.17.0")]
    [InlineData("!VisualStudio.Solution.17.0:1234")]
    [InlineData("!VisualStudio.DTE.17.0:99999999999")]
    [InlineData("!{8E4A6F58-5D1C-4F53-9C2B-3A2D5F7E1B90}")]
    [InlineData("")]
    public void IgnoresOtherEntries(string displayName) => Assert.Null(VsMoniker.ParseSupported(displayName));

    [Fact]
    public void DescribesInstance() => Assert.Equal("Visual Studio 2026 (pid 42)", new VsMoniker(18, 42).ToString());
}
