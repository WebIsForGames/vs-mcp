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

        var path = Path.GetFullPath(file);

        // A file check on a UNC path connects to that server and may send it Windows credentials.
        // Device paths (\\?\UNC\..., \\.\...) reach the same places, so only drive paths are accepted.
        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new McpException($"Network and device paths are not allowed, got '{file}'. Use a path on a local or mapped drive.");
        }

        return path;
    }

    public static bool Same(string? left, string? right) =>
        !string.IsNullOrEmpty(left)
        && !string.IsNullOrEmpty(right)
        && string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
}
