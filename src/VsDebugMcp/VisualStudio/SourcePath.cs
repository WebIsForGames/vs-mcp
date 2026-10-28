using ModelContextProtocol;

namespace VsDebugMcp.VisualStudio;

public static class SourcePath
{
    public static string Normalize(string file)
    {
        if (string.IsNullOrWhiteSpace(file) || !Path.IsPathFullyQualified(file))
        {
            throw new McpException($"File path must be absolute, got '{file}'.");
        }

        return Path.GetFullPath(file);
    }

    public static bool Same(string? left, string? right) =>
        !string.IsNullOrEmpty(left)
        && !string.IsNullOrEmpty(right)
        && string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
}
