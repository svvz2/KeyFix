using System.ComponentModel;
using System.Runtime.InteropServices;

namespace KeyFix.Windows;

public sealed class GlobalMouseHookService : IDisposable
{
    private const int WhMouseLowLevel = 14;
    private const int WmRightButtonDown = 0x0204;
    private const int WmRightButtonUp = 0x0205;
    private readonly HookProcedure _hookProcedure;
    private readonly TextSelectionDetector _textSelectionDetector = new();
    private nint _hookHandle;
    private nint _rightButtonTargetWindow;
    private bool _rightButtonHadSelectedText;

    public GlobalMouseHookService() => _hookProcedure = OnMouseEvent;

    public event EventHandler<GlobalRightClickEventArgs>? RightClickDetected;

    public void Start()
    {
        if (_hookHandle != nint.Zero)
        {
            return;
        }

        _hookHandle = SetWindowsHookEx(WhMouseLowLevel, _hookProcedure, GetModuleHandle(null), 0);
        if (_hookHandle == nint.Zero)
        {
            throw new Win32Exception("تعذر تشغيل خيار التصحيح بزر الفأرة الأيمن.");
        }
    }

    public void Dispose()
    {
        if (_hookHandle == nint.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_hookHandle);
        _hookHandle = nint.Zero;
    }

    private nint OnMouseEvent(int code, nint message, nint eventData)
    {
        if (code >= 0 && message == WmRightButtonDown)
        {
            try
            {
                var data = Marshal.PtrToStructure<LowLevelMouseData>(eventData);
                _rightButtonTargetWindow = NativeMethods.GetForegroundWindow();
                _rightButtonHadSelectedText =
                    _textSelectionDetector.HasSelectedText(
                        _rightButtonTargetWindow,
                        data.Point.X,
                        data.Point.Y);
            }
            catch
            {
                _rightButtonTargetWindow = nint.Zero;
                _rightButtonHadSelectedText = false;
            }
        }
        else if (code >= 0 && message == WmRightButtonUp)
        {
            try
            {
                var data = Marshal.PtrToStructure<LowLevelMouseData>(eventData);
                var targetWindowHandle = NativeMethods.GetForegroundWindow();
                var hasSelectedText = targetWindowHandle == _rightButtonTargetWindow &&
                                      _rightButtonHadSelectedText;
                RightClickDetected?.Invoke(
                    this,
                    new GlobalRightClickEventArgs(
                        data.Point.X,
                        data.Point.Y,
                        targetWindowHandle,
                        hasSelectedText));
            }
            catch
            {
                // A global hook must return immediately and must never propagate managed exceptions.
            }
            finally
            {
                _rightButtonTargetWindow = nint.Zero;
                _rightButtonHadSelectedText = false;
            }
        }

        return CallNextHookEx(_hookHandle, code, message, eventData);
    }

    private delegate nint HookProcedure(int code, nint message, nint eventData);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LowLevelMouseData
    {
        public NativePoint Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int hookType, HookProcedure callback, nint moduleHandle, uint threadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hookHandle);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hookHandle, int code, nint message, nint eventData);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
}

public sealed record GlobalRightClickEventArgs(
    int X,
    int Y,
    nint TargetWindowHandle,
    bool HasSelectedText);
