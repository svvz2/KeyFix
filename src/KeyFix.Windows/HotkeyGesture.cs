namespace KeyFix.Windows;

public sealed record HotkeyGesture(uint Modifiers, uint VirtualKey)
{
    public static HotkeyGesture FixSelection { get; } = new(NativeMethods.ModControl | NativeMethods.ModAlt, 0x4B);

    public static HotkeyGesture FixLastWord { get; } = new(NativeMethods.ModControl | NativeMethods.ModAlt, 0x20);

    public static HotkeyGesture Undo { get; } = new(NativeMethods.ModControl | NativeMethods.ModAlt, 0x5A);

    public static HotkeyGesture TogglePause { get; } = new(NativeMethods.ModControl | NativeMethods.ModAlt, 0x50);
}
