# Publishes the MCP server into plugin/server, where plugin/.mcp.json expects it.
param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'

dotnet publish "$PSScriptRoot\src\VsDebugMcp\VsDebugMcp.csproj" --configuration $Configuration --output "$PSScriptRoot\plugin\server"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}
