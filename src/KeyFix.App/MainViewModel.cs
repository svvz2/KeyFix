using System.Collections.ObjectModel;
using KeyFix.Core;
using KeyFix.Windows;

namespace KeyFix.App;

public sealed class MainViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly SettingsStore _settingsStore;
    private readonly StartupService _startupService;
    private readonly ILayoutConverter _converter;
    private readonly ForegroundWindowService _foregroundWindows;
    private readonly KeyboardLayoutService _keyboardLayouts;
    private bool _isEnabled;
    private bool _switchLayoutAfterCorrection;
    private bool _showNotifications;
    private bool _showRightClickAction;
    private bool _enableSmartSuggestions;
    private bool _startWithWindows;
    private ConversionDirection _preferredDirection;
    private string _previewInput = "hgsghl ugd;l";
    private string _previewOutput = "السلام عليكم";
    private string _newExcludedApplication = string.Empty;
    private string _statusTitle = "الحماية فعّالة";
    private string _statusDetail = "KeyFix جاهز لتصحيح النص من أي برنامج";
    private string _currentLayout = "جاهز";

    public MainViewModel(
        AppSettings settings,
        SettingsStore settingsStore,
        StartupService startupService,
        ILayoutConverter converter,
        ForegroundWindowService foregroundWindows,
        KeyboardLayoutService keyboardLayouts)
    {
        _settings = settings;
        _settingsStore = settingsStore;
        _startupService = startupService;
        _converter = converter;
        _foregroundWindows = foregroundWindows;
        _keyboardLayouts = keyboardLayouts;
        _isEnabled = settings.IsEnabled;
        _switchLayoutAfterCorrection = settings.SwitchLayoutAfterCorrection;
        _showNotifications = settings.ShowNotifications;
        _showRightClickAction = settings.ShowRightClickAction;
        _enableSmartSuggestions = settings.EnableSmartSuggestions;
        _startWithWindows = settings.StartWithWindows;
        _preferredDirection = settings.PreferredDirection;
        ExcludedApplications = new ObservableCollection<string>(settings.ExcludedApplications);

        ConvertPreviewCommand = new RelayCommand(_ => ConvertPreview());
        AddExcludedApplicationCommand = new RelayCommand(_ => AddExcludedApplication(), _ => !string.IsNullOrWhiteSpace(NewExcludedApplication));
        RemoveExcludedApplicationCommand = new RelayCommand(RemoveExcludedApplication);
        RefreshEnvironment();
    }

    public ObservableCollection<string> ExcludedApplications { get; }

    public IReadOnlyList<DirectionOption> DirectionOptions { get; } =
    [
        new(ConversionDirection.Auto, "تلقائي"),
        new(ConversionDirection.EnglishToArabic, "إنكليزي ← عربي"),
        new(ConversionDirection.ArabicToEnglish, "عربي ← إنكليزي")
    ];

    public RelayCommand ConvertPreviewCommand { get; }

    public RelayCommand AddExcludedApplicationCommand { get; }

    public RelayCommand RemoveExcludedApplicationCommand { get; }

    public AppSettings Settings => _settings;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                _settings.IsEnabled = value;
                StatusTitle = value ? "الحماية فعّالة" : "الحماية متوقفة";
                StatusDetail = value ? "KeyFix جاهز لتصحيح النص من أي برنامج" : "التصحيح متوقف مؤقتاً";
                Save();
            }
        }
    }

    public bool SwitchLayoutAfterCorrection
    {
        get => _switchLayoutAfterCorrection;
        set
        {
            if (SetProperty(ref _switchLayoutAfterCorrection, value))
            {
                _settings.SwitchLayoutAfterCorrection = value;
                Save();
            }
        }
    }

    public bool ShowNotifications
    {
        get => _showNotifications;
        set
        {
            if (SetProperty(ref _showNotifications, value))
            {
                _settings.ShowNotifications = value;
                Save();
            }
        }
    }

    public bool ShowRightClickAction
    {
        get => _showRightClickAction;
        set
        {
            if (SetProperty(ref _showRightClickAction, value))
            {
                _settings.ShowRightClickAction = value;
                Save();
            }
        }
    }

    public bool EnableSmartSuggestions
    {
        get => _enableSmartSuggestions;
        set
        {
            if (SetProperty(ref _enableSmartSuggestions, value))
            {
                _settings.EnableSmartSuggestions = value;
                Save();
            }
        }
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (SetProperty(ref _startWithWindows, value))
            {
                _settings.StartWithWindows = value;
                _startupService.SetEnabled(value);
                Save();
            }
        }
    }

    public ConversionDirection PreferredDirection
    {
        get => _preferredDirection;
        set
        {
            if (SetProperty(ref _preferredDirection, value))
            {
                _settings.PreferredDirection = value;
                ConvertPreview();
                Save();
            }
        }
    }

    public string PreviewInput
    {
        get => _previewInput;
        set => SetProperty(ref _previewInput, value);
    }

    public string PreviewOutput
    {
        get => _previewOutput;
        private set => SetProperty(ref _previewOutput, value);
    }

    public string NewExcludedApplication
    {
        get => _newExcludedApplication;
        set
        {
            if (SetProperty(ref _newExcludedApplication, value))
            {
                AddExcludedApplicationCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusTitle
    {
        get => _statusTitle;
        private set => SetProperty(ref _statusTitle, value);
    }

    public string StatusDetail
    {
        get => _statusDetail;
        private set => SetProperty(ref _statusDetail, value);
    }

    public string CurrentLayout
    {
        get => _currentLayout;
        private set => SetProperty(ref _currentLayout, value);
    }

    public void SetStatus(string title, string detail)
    {
        StatusTitle = title;
        StatusDetail = detail;
    }

    public void RefreshEnvironment()
    {
        var window = _foregroundWindows.GetCurrent();
        CurrentLayout = window is null ? "غير متوفر" : _keyboardLayouts.GetCurrentLayoutName(window);
    }

    public void Save()
    {
        _settings.ExcludedApplications = [.. ExcludedApplications];
        _settingsStore.Save(_settings);
    }

    private void ConvertPreview()
    {
        PreviewOutput = string.IsNullOrEmpty(PreviewInput)
            ? string.Empty
            : _converter.Convert(PreviewInput, PreferredDirection).ConvertedText;
    }

    private void AddExcludedApplication()
    {
        var name = NewExcludedApplication.Trim();
        if (name.Length == 0 || ExcludedApplications.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        ExcludedApplications.Add(name);
        NewExcludedApplication = string.Empty;
        Save();
    }

    private void RemoveExcludedApplication(object? value)
    {
        if (value is string name && ExcludedApplications.Remove(name))
        {
            Save();
        }
    }
}

public sealed record DirectionOption(ConversionDirection Value, string Label);
