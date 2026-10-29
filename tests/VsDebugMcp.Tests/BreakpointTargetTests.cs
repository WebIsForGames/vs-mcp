using ModelContextProtocol;
using VsDebugMcp.VisualStudio;

namespace VsDebugMcp.Tests;

public class BreakpointTargetTests
{
    [Fact]
    public void CreatesLineTarget()
    {
        var target = BreakpointTarget.Create("C:/src/./main.cpp", 5, function: null);

        Assert.Equal(new BreakpointTarget(@"C:\src\main.cpp", 5, null), target);
        Assert.Equal(@"C:\src\main.cpp:5", target.ToString());
    }

    [Fact]
    public void CreatesFunctionTarget()
    {
        var target = BreakpointTarget.Create(file: null, line: null, " _wassert ");

        Assert.Equal(new BreakpointTarget(null, null, "_wassert"), target);
        Assert.Equal("function _wassert", target.ToString());
    }

    [Theory]
    [InlineData(@"C:\src\main.cpp", 5, "_wassert", "not both")]
    [InlineData(null, 5, "_wassert", "not both")]
    [InlineData(null, null, null, "Pass file and line")]
    [InlineData(@"C:\src\main.cpp", null, " ", "Pass file and line")]
    [InlineData(@"C:\src\main.cpp", 0, null, "1 or greater")]
    public void RejectsIncompleteOrMixedTargets(string? file, int? line, string? function, string message) =>
        Assert.Contains(message, Assert.Throws<McpException>(() => BreakpointTarget.Create(file, line, function)).Message);
}
