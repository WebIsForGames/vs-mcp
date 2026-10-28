# Publishes the MCP server into plugin/server, where plugin/.mcp.json expects it.
# Restore is locked to packages.lock.json, so a changed package graph fails the build.
param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'

dotnet publish "$PSScriptRoot\src\VsDebugMcp\VsDebugMcp.csproj" --configuration $Configuration --output "$PSScriptRoot\plugin\server" -p:RestoreLockedMode=true
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}
