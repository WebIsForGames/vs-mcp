using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VsDebugMcp.Com;
using VsDebugMcp.VisualStudio;

var builder = Host.CreateApplicationBuilder(args);

// stdout carries the MCP protocol, so every log line goes to stderr.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<StaComDispatcher>();
builder.Services.AddSingleton<VsSession>();
builder.Services.AddSingleton<DebuggerService>();

builder.Services
    .AddMcpServer(options => options.ServerInstructions =
        "Controls the debugger of a running Visual Studio 2022/2026 instance. " +
        "Typical flow: vs_set_breakpoint -> vs_start_debugging (or vs_continue) -> vs_wait_for_break -> vs_continue. " +
        "Breakpoint file paths must be absolute.")
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
