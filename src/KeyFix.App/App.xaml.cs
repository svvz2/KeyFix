using System.Threading;
using System.Windows;
using System.Windows.Threading;
using KeyFix.Core;
using KeyFix.Windows;

namespace KeyFix.App;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            DiagnosticLog.Write($"Fatal error: {args.ExceptionObject.GetType().Name}.");
        DiagnosticLog.Write("Startup began.");

        _singleInstanceMutex = new Mutex(true, "Local\\KeyFix.Desktop.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            DiagnosticLog.Write("Startup stopped because another instance owns the mutex.");
            SingleInstanceWindowMessenger.RequestShowExistingWindow();
            Shutdown();
            return;
        }

        var settingsStore = new SettingsStore();
        var settings = settingsStore.Load();
        DiagnosticLog.Write("Settings loaded.");
        var keyboardInput = new KeyboardInputService();
        var foregroundWindows = new ForegroundWindowService();
        var keyboardLayouts = new KeyboardLayoutService();
        var converter = new Arabic101LayoutConverter();
        var correctionService = new TextCorrectionService(
            converter,
            foregroundWindows,
            keyboardInput,
            new ClipboardTextService(keyboardInput),
            keyboardLayouts,
            new PasswordFieldGuard());

        var viewModel = new MainViewModel(
            settings,
            settingsStore,
            new StartupService(),
            converter,
            foregroundWindows,
            keyboardLayouts);
        DiagnosticLog.Write("Services and view model created.");

        var window = new MainWindow(
            viewModel,
            correctionService,
            new TypingMistakeDetector(converter),
            new GlobalHotkeyService(),
            new GlobalMouseHookService(),
            new GlobalKeyboardHookService());
        DiagnosticLog.Write("Main window constructed.");
        MainWindow = window;
        window.Show();
        if (e.Args.Any(argument => string.Equals(argument, "--minimized", StringComparison.OrdinalIgnoreCase)))
        {
            window.Hide();
        }
        DiagnosticLog.Write("Main window shown.");
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        DiagnosticLog.Write($"Unhandled UI error: {e.Exception.GetType().Name}.");
        System.Windows.MessageBox.Show(
            "صار خطأ غير متوقع. تم تسجيل معلومات تقنية بدون حفظ النصوص التي تكتبها.",
            "KeyFix",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_singleInstanceMutex is not null)
        {
            try
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The mutex was not owned because another instance won startup.
            }

            _singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
    }
}
