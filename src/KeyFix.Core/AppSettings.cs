namespace KeyFix.Core;

public sealed class AppSettings
{
    public bool IsEnabled { get; set; } = true;

    public CorrectionMode Mode { get; set; } = CorrectionMode.Manual;

    public ConversionDirection PreferredDirection { get; set; } = ConversionDirection.Auto;

    public bool SwitchLayoutAfterCorrection { get; set; } = true;

    public bool ShowNotifications { get; set; } = true;

    public bool ShowRightClickAction { get; set; } = true;

    public bool EnableSmartSuggestions { get; set; } = true;

    public bool StartWithWindows { get; set; }

    public List<string> ExcludedApplications { get; set; } =
    [
        "KeePass",
        "KeePassXC",
        "1Password",
        "Bitwarden"
    ];
}
