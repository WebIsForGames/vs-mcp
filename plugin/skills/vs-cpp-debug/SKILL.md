---
name: vs-cpp-debug
description: Debug C++ code in a running Visual Studio 2022 or 2026 through the vs-debug MCP tools - set line breakpoints, start debugging, wait until a breakpoint is hit, report where execution stopped, and continue. Use when the user asks to debug a C++ program in Visual Studio, check whether execution reaches a line, stop at a line, or see how often or with which values code runs.
---

# Debugging C++ in Visual Studio

The `vs-debug` MCP server drives the debugger of a Visual Studio 2022/2026 instance the user already has open. Everything happens in the user's IDE: breakpoints you set appear in their editor and the program runs under their debugger, so the user may be interacting with the same session.

## Workflow

1. **Find Visual Studio.** Call `vs_list_instances`.
   - Empty list: ask the user to open the solution in Visual Studio. A Visual Studio started as administrator is invisible to a non-elevated MCP server; both must run at the same elevation.
   - Several instances: choose the one whose `solution` contains the code in question and call `vs_select_instance`. Ask the user if it is unclear.
   - `mode` is `design` (not debugging), `run` or `break` (stopped).
2. **Set breakpoints** with `vs_set_breakpoint`. Read the source first and pass the absolute path and a line with executable code (not a blank line, comment or lone brace).
   - Use `condition` (a C++ expression such as `value == 2`) to stop only in the interesting case instead of continuing through many hits.
   - Remember which breakpoints you created. `alreadyExisted: true` means it was already there, so it is not yours to remove.
3. **Run** according to `mode`:
   - `design`: `vs_start_debugging`. It builds first; on a build failure it returns the compiler errors instead of starting. Fix or report them, do not just retry.
   - `break`: `vs_continue`.
   - `run`: go straight to waiting.
4. **Wait** with `vs_wait_for_break` (default 60 s, maximum 300 s):
   - `mode: break`: report `breakReason` and `location` (file:line and function). With `breakReason: breakpoint`, `breakpoint.hits` counts hits in this debugging session.
   - `mode: run` with `timedOut: true`: no stop yet. If reaching the code needs user input or an action in the program, tell the user what to do, then wait again. Do not loop silently for minutes; check in with the user.
   - `mode: design`: the program exited without stopping there. See Breakpoint binding.
5. **Continue** with `vs_continue`, then `vs_wait_for_break` for the next stop. When the next stop comes immediately (a breakpoint in a loop), `vs_continue` itself returns `mode: break`; that is already the next stop.
6. **Clean up** when finished: remove the breakpoints you created with `vs_remove_breakpoint`, never the user's own. Call `vs_stop_debugging` only if you started the session or the user agrees: it terminates the program.

## Breakpoint binding

`vs_set_breakpoint` and `vs_list_breakpoints` report `bindState`:

- `notDebugging`: normal before debugging starts; binding happens when the module loads.
- `bound`: `boundLines` shows where the debugger placed the breakpoint. It can be later than the requested line (blank line, declaration without code); stops then report the bound line.
- `unbound` while debugging: the module is not loaded yet (a DLL loaded later), there is no PDB, the build is optimized Release, or the source does not match the binary. Suggest the Debug configuration and a rebuild.

## Other stop reasons

- `exceptionThrown`, `exceptionNotHandled`: the program raised an exception at `location`; the breakpoint may not have been reached.
- `userBreak`, `step`: the user paused or stepped in Visual Studio.

When the state changes unexpectedly, call `vs_get_state` and adapt instead of overriding what the user is doing.

## Errors

- "busy and rejected the call": a modal dialog is open in Visual Studio; ask the user to close it.
- "no longer available": Visual Studio was closed; call `vs_list_instances`.
- "Several Visual Studio instances are running": call `vs_select_instance`.

## Example

User: "Is Square() ever called with 2?"

1. `vs_list_instances`: one instance, `mode: design`.
2. Read `main.cpp`; `vs_set_breakpoint` on the first statement of `Square` with `condition: "value == 2"`.
3. `vs_start_debugging`, then `vs_wait_for_break`.
4. `mode: break`, `breakReason: breakpoint`, location `main.cpp:5` in `Square`: answer "Yes, it stopped in Square with value == 2 at main.cpp:5."
5. `vs_remove_breakpoint` for that line, then `vs_continue` to let the program finish.
