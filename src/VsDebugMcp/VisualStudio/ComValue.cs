using System.Runtime.InteropServices;

namespace VsDebugMcp.VisualStudio;

internal static class ComValue
{
    public static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    /// <summary>Reads an optional detail that Visual Studio may refuse to provide in its current state.</summary>
    public static T? Try<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or NotImplementedException or ArgumentException)
        {
            return default;
        }
    }
}
