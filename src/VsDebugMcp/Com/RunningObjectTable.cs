using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace VsDebugMcp.Com;

/// <summary>Reads the COM Running Object Table. Call only on the <see cref="StaComDispatcher"/> thread.</summary>
public static class RunningObjectTable
{
    /// <summary>
    /// Returns the objects whose moniker display name <paramref name="select"/> maps to a non-null key.
    /// </summary>
    public static List<(TKey Key, object Object)> GetObjects<TKey>(Func<string, TKey?> select)
        where TKey : class
    {
        Marshal.ThrowExceptionForHR(GetRunningObjectTable(0, out var rot));
        Marshal.ThrowExceptionForHR(CreateBindCtx(0, out var bindCtx));
        rot.EnumRunning(out var enumMoniker);

        var result = new List<(TKey, object)>();
        var monikers = new IMoniker[1];
        while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
        {
            string displayName;
            try
            {
                monikers[0].GetDisplayName(bindCtx, null, out displayName);
            }
            catch (COMException)
            {
                continue;
            }

            if (select(displayName) is { } key && rot.GetObject(monikers[0], out var obj) == 0)
            {
                result.Add((key, obj));
            }
        }

        return result;
    }

    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable rot);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(int reserved, out IBindCtx bindCtx);
}
