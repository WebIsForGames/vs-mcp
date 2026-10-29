using VsDebugMcp.VisualStudio;

namespace VsDebugMcp.Tests;

public class DebuggerSnapshotTests
{
    private static readonly SourceLocation Crt = new(null, null, "00007ff87a291c50", @"C:\Windows\System32\ucrtbased.dll");
    private static readonly SourceLocation Main = new(@"C:\src\main.cpp", 81, "main", @"C:\src\app.exe");
    private static readonly SourceLocation Startup = new(@"C:\crt\exe_common.inl", 79, "invoke_main", @"C:\src\app.exe");

    [Fact]
    public void KeepsCurrentFrameWithSource() => Assert.Equal(Main, DebuggerSnapshot.SourceFrame(Main, [Crt, Startup]));

    [Fact]
    public void FindsNearestFrameWithSource() => Assert.Equal(Main, DebuggerSnapshot.SourceFrame(Crt, [Crt, Main, Startup]));

    [Fact]
    public void KeepsCurrentFrameWhenNoFrameHasSource() => Assert.Equal(Crt, DebuggerSnapshot.SourceFrame(Crt, [Crt]));

    [Fact]
    public void UsesStackWhenCurrentFrameIsUnknown() => Assert.Equal(Main, DebuggerSnapshot.SourceFrame(null, [Main]));

    [Fact]
    public void DoesNotReadStackWhenCurrentFrameHasSource() => Assert.Equal(Main, DebuggerSnapshot.SourceFrame(Main, Unreadable()));

    private static IEnumerable<SourceLocation> Unreadable()
    {
        throw new InvalidOperationException("The stack must not be read.");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}
