# vs-mcp: debugging C++ in Visual Studio from Claude Code

**English** | [Русский](README.ru.md)

An MCP server and a skill that let Claude drive the debugger of an already running Visual Studio 2022 or 2026. Claude can set a breakpoint, start debugging, wait for a stop, report where and why execution stopped, and continue.

The server connects to VS through EnvDTE COM automation (the Running Object Table). No VS extension is needed: you work in your usual VS with the solution open, and the breakpoints Claude sets appear right in the editor.

## Requirements

- Windows, Visual Studio 2022 (17.x) and/or 2026 (18.x) with the C++ workload.
- .NET 10 SDK to build; the .NET 10 Runtime is enough to run.
- Claude Code.
- VS and Claude Code must run with the same privileges. A VS started as administrator is invisible to a non-elevated server.

## Build

```powershell
.\build.ps1
```

The script publishes the server to `plugin/server/`, where the plugin starts it from.

Packages are restored only at the versions and hashes in `src/VsDebugMcp/packages.lock.json`. If a package was tampered with or the dependency graph changed, the build stops. After changing package versions in `VsDebugMcp.csproj`, run `dotnet restore src\VsDebugMcp` to update the lock file, and commit it.

## Installation

### As a plugin (server and skill together)

In a terminal:

```bash
claude plugin marketplace add O:\Personal\Projects\vs-mcp
```

```bash
claude plugin install vs-cpp-debug@vs-mcp
```

The plugin appears in new Claude Code sessions, including the Code tab of the Claude app. The `/plugin` window in the app cannot add a marketplace from a local folder, so use the terminal commands.

Claude Code copies the plugin into its cache, so run `.\build.ps1` first. After a rebuild, run `claude plugin marketplace update vs-mcp`, then reinstall the plugin: `claude plugin uninstall vs-cpp-debug@vs-mcp` and `claude plugin install vs-cpp-debug@vs-mcp` again.

To try it without installing, start Claude Code with the plugin for a single session:

```bash
claude --plugin-dir O:\Personal\Projects\vs-mcp\plugin
```

### Manually

```bash
claude mcp add --scope user vs-debug -- O:\Personal\Projects\vs-mcp\plugin\server\VsDebugMcp.exe
```

Then copy the `plugin/skills/vs-cpp-debug` folder to `%USERPROFILE%\.claude\skills\`.

## Tools

| Tool | What it does |
|---|---|
| `vs_list_instances` | Running VS 2022/2026 instances: pid, version, solution, debugger mode |
| `vs_select_instance` | Picks an instance when several are running. A single VS is selected automatically |
| `vs_set_breakpoint` | Breakpoint at an absolute path and line, with an optional C++ condition |
| `vs_remove_breakpoint` | Removes the breakpoint at a line |
| `vs_list_breakpoints` | All breakpoints, including their binding state (`bindState`, `boundLines`) |
| `vs_start_debugging` | Builds and starts debugging (F5). On a build failure returns the compiler errors |
| `vs_wait_for_break` | Waits for a stop (up to 300 s). Returns the reason, file, line and function, or `timedOut`, or reports that the program exited |
| `vs_continue` | Continues from break mode |
| `vs_get_state` | Current mode and stop location |
| `vs_stop_debugging` | Stops debugging (Shift+F5) |

Debugger modes: `design` (not debugging), `run`, `break`.

## Usage

Open the solution in VS and ask Claude, for example: "Set a breakpoint at the start of Square in main.cpp, run it and tell me which values it is called with." The workflow is described in the [`vs-cpp-debug`](plugin/skills/vs-cpp-debug/SKILL.md) skill.

## Sample project

`samples/BreakpointDemo` is a small C++ console application. The toolset follows the VS version: v143 in VS 2022, v145 in VS 2026. Both VS versions were tested on it:

- breakpoint and conditional breakpoint;
- breakpoint on a blank line (VS binds it to the next line with code);
- repeated hits in a loop;
- build error;
- wait timeout;
- stopping debugging;
- choosing between two running instances.

## Limitations

- **VS modal dialogs block automation.** The server reports "busy and rejected the call"; close the dialog in VS.
- **Build before start.** With `build: false`, an out-of-date project makes VS show an "out of date" dialog.
- **Not in the MVP:** inspecting variables, evaluating expressions, call stack, stepping, attaching to a process.

## Security

- The server talks only to Claude Code over stdin/stdout and to the local VS over COM. It opens no network ports and makes no network connections itself.
- The server rejects network paths (`\\server\share\…`) and device paths (`\\?\…`, `\\.\…`) before touching the file: checking a file on someone else's server can send it your Windows credentials. Mapped drives with a drive letter work.
- `vs_start_debugging` builds and runs the startup project with your privileges, and build events and MSBuild tasks can run any command. Debug only trusted solutions; debug other people's projects in a separate virtual machine. Do not run VS as administrator.
- Tool results go to Claude: solution and file paths, process, function and module names, breakpoint conditions, build error text. Do not use the server with code that must not be sent to your model provider.
- NuGet package versions and hashes are pinned by the lock file, see [Build](#build).

## Layout

```
src/VsDebugMcp/          MCP server (C#, .NET 10, ModelContextProtocol SDK)
  Com/                   STA thread, OLE message filter, Running Object Table
  VisualStudio/          finding VS instances, debugger operations
  Tools/                 MCP tools
tests/VsDebugMcp.Tests/  unit tests (no VS needed)
samples/BreakpointDemo/  C++ project for manual testing
plugin/                  Claude Code plugin: skill, .mcp.json, server/ after the build
Plans/                   plans
```

Tests:

```bash
dotnet test VsDebugMcp.slnx
```
