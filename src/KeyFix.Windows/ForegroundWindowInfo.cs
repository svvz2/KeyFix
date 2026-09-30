namespace KeyFix.Windows;

public sealed record ForegroundWindowInfo(nint Handle, uint ThreadId, string ProcessName, string WindowTitle);
