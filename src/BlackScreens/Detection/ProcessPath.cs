using System.Text;

namespace BlackScreens.Detection;

/// <summary>
/// Resolves a process id to its executable path. Cheaper than a <see cref="System.Diagnostics.Process"/>
/// lookup and it does not throw for processes the app cannot fully open.
/// </summary>
internal static class ProcessPath
{
    public static string? ForId(int processId)
    {
        if (processId <= 0)
        {
            return null;
        }

        var handle = NativeMethods.OpenProcess(NativeMethods.ProcessQueryLimitedInformation, false, processId);
        if (handle == 0)
        {
            return null;
        }

        try
        {
            var buffer = new StringBuilder(1024);
            var size = buffer.Capacity;
            if (!NativeMethods.QueryFullProcessImageName(handle, 0, buffer, ref size))
            {
                return null;
            }

            var path = buffer.ToString(0, size);
            return path.Length == 0 ? null : path;
        }
        catch
        {
            return null;
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    public static string NameForId(int processId)
    {
        var path = ForId(processId);
        return path is null ? string.Empty : Path.GetFileNameWithoutExtension(path);
    }
}
