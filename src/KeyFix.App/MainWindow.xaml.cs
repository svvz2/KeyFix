using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Net.Http;
using System.Text;
using KeyFix.Core;
using KeyFix.Windows;
using Forms = System.Windows.Forms;

namespace KeyFix.App;

public partial class MainWindow : Window
{
    private const int FixSelectionHotkeyId = 1;
    private const int FixLastWordHotkeyId = 2;
    private const int UndoHotkeyId = 3;
    private const int TogglePauseHotkeyId = 4;

    private readonly MainViewModel _viewModel;
    private readonly TextCorrectionService _correctionService;
    private readonly TypingMistakeDetector _typingDetector;
    private readonly GlobalHotkeyService _hotkeyService;
    private readonly GlobalMouseHookService _mouseHookService;
    private readonly GlobalKeyboardHookService _keyboardHookService;
    private readonly GitHubChannelClient _githubChannelClient = new();
    private readonly QuickFixWindow _quickFixWindow;
    private readonly SmartSuggestionWindow _smartSuggestionWindow;
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly Forms.ToolStripMenuItem _pauseMenuItem;
    private bool _allowExit;
    private bool _isProcessing;
    private bool _closeTipShown;
    private bool _inputServicesReady;
    private readonly StringBuilder _typingBuffer = new(24);
    private nint _typingWindowHandle;
    private long _typingRevision;
    private string? _lastSuggestedSource;
    private DateTimeOffset _lastSuggestionAt;
    private Uri _instagramUri = new("https://www.instagram.com/22r2z/");
    private Uri _telegramUri = new("https://t.me/Civil_Sajad");
    private Uri _issuesUri = new($"{GitHubChannelClient.DefaultRepositoryUrl}/issues");
    private Uri _downloadUri = new(GitHubChannelClient.DefaultRepositoryUrl);
    private Uri _releaseNotesUri = new($"{GitHubChannelClient.DefaultRepositoryUrl}/releases");
    private Uri _announcementUri = new($"{GitHubChannelClient.DefaultRepositoryUrl}/discussions");
    private bool _updateNotificationShown;

    public MainWindow(
        MainViewModel viewModel,
        TextCorrectionService correctionService,
        TypingMistakeDetector typingDetector,
        GlobalHotkeyService hotkeyService,
        GlobalMouseHookService mouseHookService,
        GlobalKeyboardHookService keyboardHookService)
    {
        _viewModel = viewModel;
        _correctionService = correctionService;
        _typingDetector = typingDetector;
        _hotkeyService = hotkeyService;
        _mouseHookService = mouseHookService;
        _keyboardHookService = keyboardHookService;
        DataContext = viewModel;
        DiagnosticLog.Write("Main window InitializeComponent starting.");
        InitializeComponent();
        DiagnosticLog.Write("Main window InitializeComponent completed.");
        SetActiveNavigation(HomeNavButton);

        _quickFixWindow = new QuickFixWindow();
        _quickFixWindow.CorrectionRequested += OnQuickFixRequested;
        _mouseHookService.RightClickDetected += OnGlobalRightClick;
        _smartSuggestionWindow = new SmartSuggestionWindow();
        _smartSuggestionWindow.Accepted += OnSmartSuggestionAccepted;
        _smartSuggestionWindow.Dismissed += OnSmartSuggestionDismissed;
        _keyboardHookService.KeyObserved += OnGlobalKeyObserved;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        var menu = new Forms.ContextMenuStrip();
        var openItem = new Forms.ToolStripMenuItem("فتح KeyFix", null, (_, _) => Dispatcher.Invoke(ShowWindow));
        _pauseMenuItem = new Forms.ToolStripMenuItem("إيقاف مؤقت", null, (_, _) => Dispatcher.Invoke(ToggleEnabled));
        var contactItem = new Forms.ToolStripMenuItem("تواصل ويانا");
        contactItem.DropDownItems.Add(new Forms.ToolStripMenuItem("Instagram  @22r2z", null, (_, _) => Dispatcher.Invoke(() => OpenExternalLink(_instagramUri))));
        contactItem.DropDownItems.Add(new Forms.ToolStripMenuItem("Telegram  @Civil_Sajad", null, (_, _) => Dispatcher.Invoke(() => OpenExternalLink(_telegramUri))));
        contactItem.DropDownItems.Add(new Forms.ToolStripMenuItem("GitHub  الدعم والملاحظات", null, (_, _) => Dispatcher.Invoke(() => OpenExternalLink(_issuesUri))));
        var checkUpdatesItem = new Forms.ToolStripMenuItem("فحص التحديثات", null, (_, _) => Dispatcher.Invoke(() => _ = RefreshGitHubChannelAsync(showResult: true)));
        var exitItem = new Forms.ToolStripMenuItem("خروج", null, (_, _) => Dispatcher.Invoke(ExitApplication));
        menu.Items.Add(openItem);
        menu.Items.Add(_pauseMenuItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(contactItem);
        menu.Items.Add(checkUpdatesItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);
        DiagnosticLog.Write("Tray menu created.");

        _trayIcon = new Forms.NotifyIcon
        {
            Text = "KeyFix — مصحح تخطيط الكيبورد",
            Icon = TrayIconFactory.Create(),
            Visible = true,
            ContextMenuStrip = menu
        };
        DiagnosticLog.Write("Tray icon created.");
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowWindow);

        SourceInitialized += OnSourceInitialized;
        Activated += (_, _) => _viewModel.RefreshEnvironment();
        StateChanged += OnStateChanged;
        Closing += OnClosing;
        Closed += OnClosed;
        UpdatePauseMenu();
        Loaded += OnLoaded;

#if DEBUG
        Loaded += CaptureDebugSnapshotIfRequested;
        Loaded += ShowDebugSuggestionIfRequested;
#endif
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await RefreshGitHubChannelAsync(showResult: false);
    }

    private async Task RefreshGitHubChannelAsync(bool showResult)
    {
        ChannelStatusText.Text = "نتحقق من أخبار المشروع عبر GitHub…";
        try
        {
            var channel = await _githubChannelClient.FetchAsync();
            if (channel is null)
            {
                ChannelStatusText.Text = "روابط التواصل جاهزة — تعذر قراءة قناة المشروع حالياً";
                SetChannelConnectionState(connected: false);
                return;
            }

            ApplyGitHubChannel(channel);
            ChannelStatusText.Text = "الحسابات والتحديثات مرتبطة بمستودع KeyFix الرسمي";
            SetChannelConnectionState(connected: true);
            DiagnosticLog.Write($"GitHub channel loaded. Latest version: {channel.Update.LatestVersion}.");

            if (GitHubChannelClient.HasNewerRelease(channel))
            {
                ShowAvailableUpdate(channel.Update);
            }
            else if (showResult)
            {
                Notify("KeyFix محدّث", "أنت تستخدم آخر إصدار متوفر حالياً");
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            ChannelStatusText.Text = "روابط التواصل جاهزة — فحص التحديثات يحتاج اتصالاً بالإنترنت";
            SetChannelConnectionState(connected: false);
            DiagnosticLog.Write($"GitHub channel unavailable: {exception.GetType().Name}.");
            if (showResult)
            {
                Notify("تعذر فحص التحديثات", "تحقق من اتصال الإنترنت وحاول مرة ثانية", isError: true);
            }
        }
    }

    private void SetChannelConnectionState(bool connected)
    {
        ChannelConnectionText.Text = connected ? "GitHub Connected" : "GitHub Offline";
        ChannelConnectionBadge.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(connected ? "#E7EEE9" : "#F0EEE8"));
        ChannelConnectionDot.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(connected ? "#4C9B68" : "#9A9D97"));
        ChannelConnectionText.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(connected ? "#4C8060" : "#737872"));
    }

    private void ApplyGitHubChannel(GitHubChannelConfig channel)
    {
        if (GitHubChannelClient.TryGetHttpsUri(channel.Social.InstagramUrl, out var instagramUri))
        {
            _instagramUri = instagramUri;
        }

        if (GitHubChannelClient.TryGetHttpsUri(channel.Social.TelegramUrl, out var telegramUri))
        {
            _telegramUri = telegramUri;
        }

        if (GitHubChannelClient.TryGetHttpsUri(channel.Project.IssuesUrl, out var issuesUri))
        {
            _issuesUri = issuesUri;
        }

        var instagramHandle = NormalizeHandle(channel.Social.InstagramHandle, "@22r2z");
        var telegramHandle = NormalizeHandle(channel.Social.TelegramHandle, "@Civil_Sajad");
        HomeInstagramButton.Content = $"Instagram  {instagramHandle}";
        AboutInstagramButton.Content = $"Instagram   {instagramHandle}";
        HomeTelegramButton.Content = $"Telegram  {telegramHandle}";
        AboutTelegramButton.Content = $"Telegram   {telegramHandle}";

        if (channel.Announcement.Enabled &&
            !string.IsNullOrWhiteSpace(channel.Announcement.Title) &&
            !string.IsNullOrWhiteSpace(channel.Announcement.Message))
        {
            AnnouncementTitleText.Text = CleanChannelText(channel.Announcement.Title, "خبر من KeyFix", 70);
            AnnouncementMessageText.Text = CleanChannelText(channel.Announcement.Message, string.Empty, 220);
            AnnouncementButton.Content = CleanChannelText(channel.Announcement.ButtonLabel, "اقرأ التفاصيل", 28);
            AnnouncementButton.Visibility = GitHubChannelClient.TryGetHttpsUri(channel.Announcement.Url, out _announcementUri)
                ? Visibility.Visible
                : Visibility.Collapsed;
            AnnouncementBanner.Visibility = Visibility.Visible;
        }
        else
        {
            AnnouncementBanner.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowAvailableUpdate(GitHubUpdateInfo update)
    {
        var version = CleanChannelText(update.LatestVersion, "جديد", 24);
        UpdateTitleText.Text = $"يتوفر تحديث KeyFix {version}";
        UpdateMessageText.Text = CleanChannelText(
            update.Message,
            "نزّل النسخة الجديدة من صفحة الإصدار الرسمية على GitHub.",
            220);

        GitHubChannelClient.TryGetHttpsUri(update.DownloadUrl, out _downloadUri);
        GitHubChannelClient.TryGetHttpsUri(update.ReleaseNotesUrl, out _releaseNotesUri);
        UpdateBanner.Visibility = Visibility.Visible;

        if (!_updateNotificationShown)
        {
            _updateNotificationShown = true;
            Notify("يتوفر تحديث جديد", $"KeyFix {version} جاهز للتحميل من GitHub");
        }
    }

    private static string NormalizeHandle(string? handle, string fallback)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            return fallback;
        }

        var normalized = handle.Trim();
        if (normalized.Length > 32)
        {
            normalized = normalized[..32];
        }

        return normalized.StartsWith('@') ? normalized : $"@{normalized}";
    }

    private static string CleanChannelText(string? value, string fallback, int maximumLength)
    {
        var result = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return result.Length <= maximumLength ? result : result[..maximumLength] + "…";
    }

#if DEBUG
    private void ShowDebugSuggestionIfRequested(object sender, RoutedEventArgs e)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("KEYFIX_SHOW_SMART_SUGGESTION"), "1", StringComparison.Ordinal))
        {
            return;
        }

        _smartSuggestionWindow.ShowSuggestion(
            new TypingSuggestion("hgsghl", "السلام", ConversionDirection.EnglishToArabic, 0.98),
            " ",
            new WindowInteropHelper(this).Handle);
    }

    private async void CaptureDebugSnapshotIfRequested(object sender, RoutedEventArgs e)
    {
        var path = Environment.GetEnvironmentVariable("KEYFIX_CAPTURE_UI");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var requestedPage = Environment.GetEnvironmentVariable("KEYFIX_CAPTURE_PAGE");
        if (string.Equals(requestedPage, "settings", StringComparison.OrdinalIgnoreCase))
        {
            ShowPage(SettingsPage, SettingsNavButton);
        }
        else if (string.Equals(requestedPage, "about", StringComparison.OrdinalIgnoreCase))
        {
            ShowPage(AboutPage, AboutNavButton);
        }
        else
        {
            ShowPage(DashboardPage, HomeNavButton);
        }

        if (string.Equals(Environment.GetEnvironmentVariable("KEYFIX_CAPTURE_MAXIMIZED"), "1", StringComparison.Ordinal))
        {
            WindowState = WindowState.Maximized;
        }

        await Task.Delay(350);
        var dpi = VisualTreeHelper.GetDpi(this);
        var width = Math.Max(1, (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX));
        var height = Math.Max(1, (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY));
        var bitmap = new RenderTargetBitmap(width, height, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        encoder.Save(stream);
        DiagnosticLog.Write("Debug UI snapshot captured.");
    }
#endif

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _hotkeyService.Attach(handle);
        var source = HwndSource.FromHwnd(handle);
        source?.AddHook(WindowProcedure);

        try
        {
            _hotkeyService.Register(FixSelectionHotkeyId, HotkeyGesture.FixSelection);
            _hotkeyService.Register(FixLastWordHotkeyId, HotkeyGesture.FixLastWord);
            _hotkeyService.Register(UndoHotkeyId, HotkeyGesture.Undo);
            _hotkeyService.Register(TogglePauseHotkeyId, HotkeyGesture.TogglePause);
            _mouseHookService.Start();
            _inputServicesReady = true;
            UpdateSmartMonitoringState();
            DiagnosticLog.Write("All global hotkeys registered.");
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Global input registration failed: {exception.GetType().Name}.");
            Notify("تعذر تسجيل أحد الاختصارات", exception.Message, isError: true);
        }
    }

    private nint WindowProcedure(nint windowHandle, int message, nint wParam, nint lParam, ref bool handled)
    {
        if ((uint)message == SingleInstanceWindowMessenger.ShowMainWindowMessage)
        {
            handled = true;
            Dispatcher.BeginInvoke(ShowWindow);
            return nint.Zero;
        }

        if (message != 0x0312)
        {
            return nint.Zero;
        }

        handled = true;
        var id = wParam.ToInt32();
        if (id == TogglePauseHotkeyId)
        {
            ToggleEnabled();
        }
        else
        {
            _ = HandleCorrectionHotkeyAsync(id);
        }

        return nint.Zero;
    }

    private async Task HandleCorrectionHotkeyAsync(int id)
    {
        if (_isProcessing)
        {
            return;
        }

        _isProcessing = true;
        try
        {
            // Wait for the physical shortcut keys to be released. A fixed delay is not
            // reliable when the user holds Ctrl+Alt a little longer in any application.
            await _correctionService.WaitForShortcutModifiersReleasedAsync();
            CorrectionOutcome outcome;
            if (id == UndoHotkeyId)
            {
                outcome = _correctionService.Undo();
            }
            else
            {
                outcome = await _correctionService.FixSelectionAsync(
                    _viewModel.Settings,
                    selectPreviousWord: id == FixLastWordHotkeyId);
            }

            DiagnosticLog.Write($"Hotkey {id} completed with status {outcome.Status}.");

            Notify(
                outcome.IsSuccess ? "تم بنجاح" : "KeyFix",
                outcome.CorrectedText is { Length: > 0 } corrected ? $"{outcome.Message}: {Shorten(corrected)}" : outcome.Message,
                isError: outcome.Status == CorrectionStatus.Failed);
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private void OnGlobalRightClick(object? sender, GlobalRightClickEventArgs e)
    {
        DiagnosticLog.Write($"Right-click observed. Text selection: {e.HasSelectedText}.");
        Dispatcher.BeginInvoke(async () =>
        {
            await Task.Delay(120);
            var canOffer = _correctionService.CanOfferRightClickAction(
                _viewModel.Settings,
                e.TargetWindowHandle,
                e.HasSelectedText);
            DiagnosticLog.Write($"Right-click action available: {canOffer}.");
            if (!canOffer)
            {
                _quickFixWindow.HideAction();
                return;
            }

            _quickFixWindow.ShowAt(e.X, e.Y, e.TargetWindowHandle);
            DiagnosticLog.Write("Right-click action shown.");
        });
    }

    private async void OnQuickFixRequested(nint targetWindowHandle)
    {
        if (_isProcessing)
        {
            return;
        }

        _isProcessing = true;
        try
        {
            if (!_correctionService.ActivateWindow(targetWindowHandle))
            {
                Notify("تعذر التصحيح", "لم يعد التطبيق الأصلي متاحاً", isError: true);
                return;
            }

            await Task.Delay(140);
            var outcome = await _correctionService.FixSelectionAsync(
                _viewModel.Settings,
                directionOverride: ConversionDirection.Auto);
            DiagnosticLog.Write($"Right-click correction completed with status {outcome.Status}.");
            Notify(
                outcome.IsSuccess ? "تم تصحيح اللغة" : "KeyFix",
                outcome.CorrectedText is { Length: > 0 } corrected ? $"{outcome.Message}: {Shorten(corrected)}" : outcome.Message,
                isError: outcome.Status == CorrectionStatus.Failed);
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private void OnGlobalKeyObserved(object? sender, GlobalKeyObservedEventArgs e)
    {
        Dispatcher.BeginInvoke(() => ProcessObservedKey(e));
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.EnableSmartSuggestions) or nameof(MainViewModel.IsEnabled))
        {
            UpdateSmartMonitoringState();
        }
    }

    private void UpdateSmartMonitoringState()
    {
        if (!_inputServicesReady)
        {
            return;
        }

        if (_viewModel.IsEnabled && _viewModel.EnableSmartSuggestions)
        {
            try
            {
                _keyboardHookService.Start();
            }
            catch (Exception exception)
            {
                DiagnosticLog.Write($"Smart monitoring failed: {exception.GetType().Name}.");
                Notify("تعذر تشغيل الاقتراحات الذكية", exception.Message, isError: true);
            }
        }
        else
        {
            _keyboardHookService.Stop();
            ResetTypingBuffer();
            _smartSuggestionWindow.Dismiss();
        }
    }

    private void ProcessObservedKey(GlobalKeyObservedEventArgs e)
    {
        _typingRevision++;
        if (_smartSuggestionWindow.IsVisible)
        {
            _smartSuggestionWindow.Dismiss();
        }

        if (!_correctionService.CanMonitorTyping(_viewModel.Settings, e.TargetWindowHandle))
        {
            ResetTypingBuffer();
            return;
        }

        if (_typingWindowHandle != e.TargetWindowHandle)
        {
            ResetTypingBuffer();
            _typingWindowHandle = e.TargetWindowHandle;
        }

        switch (e.Kind)
        {
            case GlobalKeyKind.Character when e.Character is { } character:
                if (_typingBuffer.Length >= 24)
                {
                    ResetTypingBuffer();
                    _typingWindowHandle = e.TargetWindowHandle;
                }

                _typingBuffer.Append(character);
                break;

            case GlobalKeyKind.Backspace:
                if (_typingBuffer.Length > 0)
                {
                    _typingBuffer.Length--;
                }
                break;

            case GlobalKeyKind.Boundary when e.Character is { } delimiter:
                var word = _typingBuffer.ToString();
                _typingBuffer.Clear();
                var suggestion = _typingDetector.AnalyzeWord(word);
                if (suggestion is not null && !IsSuggestionThrottled(suggestion.OriginalText))
                {
                    var revision = _typingRevision;
                    _ = ShowSuggestionAfterInputSettlesAsync(
                        suggestion,
                        delimiter.ToString(),
                        e.TargetWindowHandle,
                        revision);
                }
                break;

            default:
                ResetTypingBuffer();
                break;
        }
    }

    private async Task ShowSuggestionAfterInputSettlesAsync(
        TypingSuggestion suggestion,
        string delimiter,
        nint targetWindowHandle,
        long revision)
    {
        await Task.Delay(110);
        if (revision != _typingRevision ||
            !_correctionService.CanMonitorTyping(_viewModel.Settings, targetWindowHandle))
        {
            return;
        }

        _lastSuggestedSource = suggestion.OriginalText;
        _lastSuggestionAt = DateTimeOffset.UtcNow;
        _smartSuggestionWindow.ShowSuggestion(suggestion, delimiter, targetWindowHandle);
        _viewModel.SetStatus("اقتراح ذكي", "اكتشف KeyFix احتمال استخدام تخطيط غير صحيح");
    }

    private async void OnSmartSuggestionAccepted(object? sender, SmartSuggestionActionEventArgs e)
    {
        if (_isProcessing)
        {
            return;
        }

        _isProcessing = true;
        _typingRevision++;
        ResetTypingBuffer();
        try
        {
            var outcome = await _correctionService.FixRecentTextAsync(
                _viewModel.Settings,
                e.TargetWindowHandle,
                e.Suggestion.OriginalText,
                e.Suggestion.SuggestedText,
                e.Delimiter,
                e.Suggestion.Direction);
            DiagnosticLog.Write($"Smart suggestion completed with status {outcome.Status}.");
            _viewModel.SetStatus(
                outcome.IsSuccess ? "تم تطبيق الاقتراح" : "تعذر تطبيق الاقتراح",
                outcome.Message);
            if (!outcome.IsSuccess)
            {
                Notify("تعذر تطبيق الاقتراح", outcome.Message, isError: true);
            }
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private void OnSmartSuggestionDismissed(object? sender, EventArgs e)
    {
        if (_viewModel.IsEnabled)
        {
            _viewModel.SetStatus(
                "الحماية فعّالة",
                _viewModel.EnableSmartSuggestions ? "KeyFix يراقب أخطاء التخطيط محلياً" : "الاختصارات جاهزة");
        }
    }

    private bool IsSuggestionThrottled(string source) =>
        DateTimeOffset.UtcNow - _lastSuggestionAt < TimeSpan.FromSeconds(18) ||
        string.Equals(source, _lastSuggestedSource, StringComparison.OrdinalIgnoreCase) &&
        DateTimeOffset.UtcNow - _lastSuggestionAt < TimeSpan.FromMinutes(2);

    private void ResetTypingBuffer()
    {
        _typingBuffer.Clear();
        _typingWindowHandle = nint.Zero;
    }

    private void Notify(string title, string message, bool isError = false)
    {
        _viewModel.SetStatus(title, message);
        if (!_viewModel.ShowNotifications)
        {
            return;
        }

        _trayIcon.BalloonTipTitle = title;
        _trayIcon.BalloonTipText = message;
        _trayIcon.BalloonTipIcon = isError ? Forms.ToolTipIcon.Error : Forms.ToolTipIcon.Info;
        _trayIcon.ShowBalloonTip(2200);
    }

    private static string Shorten(string text) => text.Length <= 42 ? text : text[..39] + "…";

    private void ToggleEnabled()
    {
        _viewModel.IsEnabled = !_viewModel.IsEnabled;
        UpdatePauseMenu();
        Notify(_viewModel.IsEnabled ? "KeyFix يعمل" : "KeyFix متوقف", _viewModel.IsEnabled ? "الاختصارات جاهزة" : "تم إيقاف التصحيح مؤقتاً");
    }

    private void UpdatePauseMenu() => _pauseMenuItem.Text = _viewModel.IsEnabled ? "إيقاف مؤقت" : "تشغيل";

    private void ShowWindow()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Topmost = true;
        Activate();
        Topmost = false;
        Focus();
    }

    private void OpenInstagram_Click(object sender, RoutedEventArgs e) =>
        OpenExternalLink(_instagramUri);

    private void OpenTelegram_Click(object sender, RoutedEventArgs e) =>
        OpenExternalLink(_telegramUri);

    private void OpenGitHubSupport_Click(object sender, RoutedEventArgs e) =>
        OpenExternalLink(_issuesUri);

    private void DownloadUpdate_Click(object sender, RoutedEventArgs e) =>
        OpenExternalLink(_downloadUri);

    private void OpenReleaseNotes_Click(object sender, RoutedEventArgs e) =>
        OpenExternalLink(_releaseNotesUri);

    private void OpenAnnouncement_Click(object sender, RoutedEventArgs e) =>
        OpenExternalLink(_announcementUri);

    private void OpenExternalLink(Uri address)
    {
        try
        {
            Process.Start(new ProcessStartInfo(address.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Opening contact link failed: {exception.GetType().Name}.");
            Notify("تعذر فتح الرابط", "تأكد من وجود متصفح افتراضي على Windows", isError: true);
        }
    }

    private void ExitApplication()
    {
        _allowExit = true;
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowExit)
        {
            return;
        }

        e.Cancel = true;
        Hide();
        if (!_closeTipShown)
        {
            _closeTipShown = true;
            Notify("KeyFix مستمر بالعمل", "تقدر تفتحه من أيقونة النظام أو تستخدم الاختصارات مباشرة");
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _hotkeyService.Dispose();
        _mouseHookService.Dispose();
        _keyboardHookService.Dispose();
        _githubChannelClient.Dispose();
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _quickFixWindow.ClosePermanently();
        _smartSuggestionWindow.ClosePermanently();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _viewModel.Save();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        MaximizeGlyph.Text = WindowState == WindowState.Maximized ? "❐" : "□";

        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void HomeNavButton_Click(object sender, RoutedEventArgs e) => ShowPage(DashboardPage, HomeNavButton);

    private void SettingsNavButton_Click(object sender, RoutedEventArgs e) => ShowPage(SettingsPage, SettingsNavButton);

    private void AboutNavButton_Click(object sender, RoutedEventArgs e) => ShowPage(AboutPage, AboutNavButton);

    private void ShowPage(UIElement page, System.Windows.Controls.Button activeNavigationButton)
    {
        DashboardPage.Visibility = Visibility.Collapsed;
        SettingsPage.Visibility = Visibility.Collapsed;
        AboutPage.Visibility = Visibility.Collapsed;
        page.Visibility = Visibility.Visible;
        SetActiveNavigation(activeNavigationButton);
    }

    private void SetActiveNavigation(System.Windows.Controls.Button activeButton)
    {
        foreach (var button in new[] { HomeNavButton, SettingsNavButton, AboutNavButton })
        {
            button.Background = button == activeButton
                ? (System.Windows.Media.Brush)FindResource("SidebarRaisedBrush")
                : System.Windows.Media.Brushes.Transparent;
            button.Foreground = button == activeButton
                ? (System.Windows.Media.Brush)FindResource("SidebarTextBrush")
                : (System.Windows.Media.Brush)FindResource("SidebarMutedBrush");
        }
    }
}
