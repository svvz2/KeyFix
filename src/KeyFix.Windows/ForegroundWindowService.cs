using System.Diagnostics;

namespace KeyFix.Windows;

public sealed class ForegroundWindowService
{
    public ForegroundWindowInfo? GetCurrent() => Get(NativeMethods.GetForegroundWindow());

    public ForegroundWindowInfo? Get(nint handle)
    {
        if (handle == nint.Zero)
        {
            return null;
        }

        var threadId = NativeMethods.GetWindowThreadProcessId(handle, out var processId);
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return new ForegroundWindowInfo(handle, threadId, process.ProcessName, process.MainWindowTitle);
        }
        catch
        {
            return new ForegroundWindowInfo(handle, threadId, string.Empty, string.Empty);
        }
    }

    public bool Activate(nint handle) =>
        handle != nint.Zero && NativeMethods.IsWindow(handle) && NativeMethods.SetForegroundWindow(handle);
}
