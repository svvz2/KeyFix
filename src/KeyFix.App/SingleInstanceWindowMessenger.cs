using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KeyFix.App;

internal static class SingleInstanceWindowMessenger
{
    private const string MessageName = "KeyFix.Desktop.ShowMainWindow.v1";
    private const int SwShow = 5;

    internal static uint ShowMainWindowMessage { get; } = RegisterWindowMessage(MessageName);

    internal static void RequestShowExistingWindow()
    {
        for (var attempt = 0; attempt < 15; attempt++)
        {
            var window = FindExistingProcessWindow();
            if (window != nint.Zero)
            {
                ShowWindowAsync(window, SwShow);
                PostMessage(window, ShowMainWindowMessage, nint.Zero, nint.Zero);
                SetForegroundWindow(window);
                return;
            }

            Thread.Sleep(100);
        }
    }

    private static nint FindExistingProcessWindow()
    {
        using var current = Process.GetCurrentProcess();
        foreach (var process in Process.GetProcessesByName(current.ProcessName))
        {
            using (process)
            {
                if (process.Id == current.Id)
                {
                    continue;
                }

                nint found = nint.Zero;
                EnumWindows((window, _) =>
                {
                    GetWindowThreadProcessId(window, out var processId);
                    if (processId != process.Id)
                    {
                        return true;
                    }

                    found = window;
                    return false;
                }, nint.Zero);

                if (found != nint.Zero)
                {
                    return found;
                }
            }
        }

        return nint.Zero;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);

    private delegate bool EnumWindowsProcedure(nint windowHandle, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProcedure callback, nint parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint windowHandle, out int processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(nint windowHandle, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint windowHandle, uint message, nint wParam, nint lParam);
}
