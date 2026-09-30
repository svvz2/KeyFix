using System.ComponentModel;

namespace KeyFix.Windows;

public sealed class GlobalHotkeyService : IDisposable
{
    private readonly Dictionary<int, HotkeyGesture> _registered = [];
    private nint _windowHandle;

    public void Attach(nint windowHandle)
    {
        if (windowHandle == nint.Zero)
        {
            throw new ArgumentException("A valid window handle is required.", nameof(windowHandle));
        }

        _windowHandle = windowHandle;
    }

    public void Register(int id, HotkeyGesture gesture)
    {
        if (_windowHandle == nint.Zero)
        {
            throw new InvalidOperationException("Attach must be called before registering hotkeys.");
        }

        if (_registered.ContainsKey(id))
        {
            NativeMethods.UnregisterHotKey(_windowHandle, id);
        }

        if (!NativeMethods.RegisterHotKey(_windowHandle, id, gesture.Modifiers, gesture.VirtualKey))
        {
            throw new Win32Exception($"تعذر تسجيل الاختصار رقم {id}. قد يكون مستخدماً من برنامج آخر.");
        }

        _registered[id] = gesture;
    }

    public void Dispose()
    {
        foreach (var id in _registered.Keys)
        {
            NativeMethods.UnregisterHotKey(_windowHandle, id);
        }

        _registered.Clear();
    }
}
