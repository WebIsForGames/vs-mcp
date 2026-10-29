using EnvDTE;
using VsDebugMcp.VisualStudio;

namespace VsDebugMcp.Tests;

public class DebuggerTextTests
{
    [Theory]
    [InlineData(dbgDebugMode.dbgDesignMode, "design")]
    [InlineData(dbgDebugMode.dbgRunMode, "run")]
    [InlineData(dbgDebugMode.dbgBreakMode, "break")]
    public void NamesModes(dbgDebugMode mode, string expected) => Assert.Equal(expected, DebuggerText.Mode(mode));

    [Theory]
    [InlineData(dbgEventReason.dbgEventReasonBreakpoint, "breakpoint")]
    [InlineData(dbgEventReason.dbgEventReasonExceptionThrown, "exceptionThrown")]
    [InlineData(dbgEventReason.dbgEventReasonExceptionNotHandled, "exceptionNotHandled")]
    [InlineData(dbgEventReason.dbgEventReasonUserBreak, "userBreak")]
    [InlineData(dbgEventReason.dbgEventReasonStep, "step")]
    public void NamesBreakReasons(dbgEventReason reason, string expected) => Assert.Equal(expected, DebuggerText.Reason(reason));

    [Fact]
    public void ExceptionReasonConstantsMatchReasonNames()
    {
        Assert.Equal(DebuggerText.ExceptionThrown, DebuggerText.Reason(dbgEventReason.dbgEventReasonExceptionThrown));
        Assert.Equal(DebuggerText.ExceptionNotHandled, DebuggerText.Reason(dbgEventReason.dbgEventReasonExceptionNotHandled));
    }

    [Fact]
    public void ExtractsExceptionMessageWithoutNotices()
    {
        const string output =
            "'ExceptionDemo.exe' (Win32): Loaded 'C:\\Windows\\System32\\ntdll.dll'. \r\n" +
            "The thread 77672 has exited with code 0 (0x0).\r\n" +
            "Exception thrown: read access violation.\r\n" +
            "**pointer** was nullptr.\r\n" +
            "\r\n";

        Assert.Equal("Exception thrown: read access violation.\n**pointer** was nullptr.", DebuggerText.ExceptionMessage(output));
    }

    [Fact]
    public void KeepsOnlyTheLastMessageLines() =>
        Assert.Equal("3\n4\n5\n6", DebuggerText.ExceptionMessage("1\n2\n3\n4\n5\n6\n"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("'ExceptionDemo.exe' (Win32): Unloaded 'C:\\Windows\\System32\\kernel.appcore.dll'\r\nThe program '[42] ExceptionDemo.exe' has exited with code 0 (0x0).\r\n")]
    public void FindsNoMessageInNotices(string? output) => Assert.Null(DebuggerText.ExceptionMessage(output));
}
