using ModelContextProtocol;
using VsDebugMcp.VisualStudio;

namespace VsDebugMcp.Tests;

public class SourcePathTests
{
    [Theory]
    [InlineData(@"src\main.cpp")]
    [InlineData(@"\src\main.cpp")]
    [InlineData("")]
    public void RejectsPathsThatAreNotAbsolute(string file) => Assert.Throws<McpException>(() => SourcePath.Normalize(file));

    [Fact]
    public void NormalizesSeparatorsAndDots() => Assert.Equal(@"C:\src\main.cpp", SourcePath.Normalize("C:/src/./lib/../main.cpp"));

    [Theory]
    [InlineData(@"C:\Src\Main.cpp", @"c:\src\main.cpp", true)]
    [InlineData(@"C:\src\main.cpp", "C:/src/main.cpp", true)]
    [InlineData(@"C:\src\main.cpp", @"C:\src\other.cpp", false)]
    [InlineData("", @"C:\src\main.cpp", false)]
    [InlineData(null, @"C:\src\main.cpp", false)]
    public void ComparesPaths(string? left, string? right, bool expected) => Assert.Equal(expected, SourcePath.Same(left, right));
}
