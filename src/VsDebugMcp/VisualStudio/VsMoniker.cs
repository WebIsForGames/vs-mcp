using System.Text.RegularExpressions;

namespace VsDebugMcp.VisualStudio;

/// <summary>ROT registration of a devenv.exe automation object, e.g. "!VisualStudio.DTE.17.0:12345".</summary>
public sealed partial record VsMoniker(int MajorVersion, int ProcessId)
{
    /// <summary>Product year for supported versions (17 = 2022, 18 = 2026), otherwise null.</summary>
    public string? ProductYear => MajorVersion switch
    {
        17 => "2022",
        18 => "2026",
        _ => null,
    };

    public override string ToString() => $"Visual Studio {ProductYear ?? $"{MajorVersion}.0"} (pid {ProcessId})";

    /// <summary>Parses a ROT display name; returns null for other objects and unsupported versions.</summary>
    public static VsMoniker? ParseSupported(string displayName)
    {
        var match = Pattern().Match(displayName);
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, out var majorVersion)
            || !int.TryParse(match.Groups[2].Value, out var processId))
        {
            return null;
        }

        var moniker = new VsMoniker(majorVersion, processId);
        return moniker.ProductYear is null ? null : moniker;
    }

    [GeneratedRegex(@"^!VisualStudio\.DTE\.(\d+)\.0:(\d+)$")]
    private static partial Regex Pattern();
}
