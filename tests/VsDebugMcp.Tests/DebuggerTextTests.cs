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
}
