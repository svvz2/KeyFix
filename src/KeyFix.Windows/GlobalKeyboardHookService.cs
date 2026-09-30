using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace KeyFix.Windows;

public sealed class GlobalKeyboardHookService : IDisposable
{
    private const int WhKeyboardLowLevel = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const uint LlkhfInjected = 0x10;
    private const uint VkBack = 0x08;
    private const uint VkTab = 0x09;
    private const uint VkReturn = 0x0D;
    private const uint VkEscape = 0x1B;
    private const uint VkSpace = 0x20;
    private const uint VkDelete = 0x2E;
    private const int VkMenu = 0x12;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;

    private readonly HookProcedure _hookProcedure;
    private nint _hookHandle;

#if DEBUG
    private static readonly bool ObserveInjectedInputForTesting =
        string.Equals(Environment.GetEnvironmentVariable("KEYFIX_TEST_INJECTED_INPUT"), "1", StringComparison.Ordinal);
#endif

    public GlobalKeyboardHookService() => _hookProcedure = OnKeyboardEvent;

    public event EventHandler<GlobalKeyObservedEventArgs>? KeyObserved;

    public void Start()
    {
        if (_hookHandle != nint.Zero)
        {
            return;
        }

        _hookHandle = SetWindowsHookEx(WhKeyboardLowLevel, _hookProcedure, GetModuleHandle(null), 0);
        if (_hookHandle == nint.Zero)
        {
            throw new Win32Exception("تعذر تشغيل مراقبة اقتراحات الكتابة.");
        }
    }

    public void Dispose()
    {
        Stop();
    }

    public void Stop()
    {
        if (_hookHandle == nint.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_hookHandle);
        _hookHandle = nint.Zero;
    }

    private nint OnKeyboardEvent(int code, nint message, nint eventData)
    {
        if (code >= 0 && (message == WmKeyDown || message == WmSysKeyDown))
        {
            try
            {
                var data = Marshal.PtrToStructure<LowLevelKeyboardData>(eventData);
                var isInjected = (data.Flags & LlkhfInjected) != 0;
#if DEBUG
                if (!isInjected || ObserveInjectedInputForTesting)
#else
                if (!isInjected)
#endif
                {
                    Publish(data);
                }
            }
            catch
            {
                // Low-level hooks must never leak exceptions into the Windows input chain.
            }
        }

        return CallNextHookEx(_hookHandle, code, message, eventData);
    }

    private void Publish(LowLevelKeyboardData data)
    {
        var target = NativeMethods.GetForegroundWindow();
        if (target == nint.Zero)
        {
            return;
        }

        if (data.VirtualKey == VkBack)
        {
            Raise(GlobalKeyKind.Backspace, null, target);
            return;
        }

        if (data.VirtualKey is VkTab or VkReturn or VkEscape or VkDelete || HasCommandModifier())
        {
            Raise(GlobalKeyKind.Reset, null, target);
            return;
        }

        if (data.VirtualKey == VkSpace)
        {
            Raise(GlobalKeyKind.Boundary, ' ', target);
            return;
        }

        var character = Translate(data, target);
        if (character is null)
        {
            return;
        }

        if (char.IsLetter(character.Value))
        {
            Raise(GlobalKeyKind.Character, character, target);
        }
        else if (character is '.' or ',' or '!' or '?' or '،' or '؛' or '؟')
        {
            Raise(GlobalKeyKind.Boundary, character, target);
        }
        else
        {
            Raise(GlobalKeyKind.Reset, null, target);
        }
    }

    private static char? Translate(LowLevelKeyboardData data, nint target)
    {
        var keyboardState = new byte[256];
        if (!GetKeyboardState(keyboardState))
        {
            return null;
        }

        keyboardState[data.VirtualKey] |= 0x80;
        var threadId = NativeMethods.GetWindowThreadProcessId(target, out _);
        var layout = NativeMethods.GetKeyboardLayout(threadId);
        var buffer = new StringBuilder(8);
        var count = ToUnicodeEx(data.VirtualKey, data.ScanCode, keyboardState, buffer, buffer.Capacity, 0, layout);
        return count > 0 ? buffer[0] : null;
    }

    private static bool HasCommandModifier() =>
        IsPressed(NativeMethods.VkControl) || IsPressed(VkMenu) || IsPressed(VkLWin) || IsPressed(VkRWin);

    private static bool IsPressed(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private void Raise(GlobalKeyKind kind, char? character, nint target) =>
        KeyObserved?.Invoke(this, new GlobalKeyObservedEventArgs(kind, character, target));

    private delegate nint HookProcedure(int code, nint message, nint eventData);

    [StructLayout(LayoutKind.Sequential)]
    private struct LowLevelKeyboardData
    {
        public uint VirtualKey;
        public uint ScanCode;
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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardState(byte[] keyboardState);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(
        uint virtualKey,
        uint scanCode,
        byte[] keyboardState,
        [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder buffer,
        int bufferSize,
        uint flags,
        nint keyboardLayout);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
}

public enum GlobalKeyKind
{
    Character,
    Boundary,
    Backspace,
    Reset
}

public sealed record GlobalKeyObservedEventArgs(GlobalKeyKind Kind, char? Character, nint TargetWindowHandle);
