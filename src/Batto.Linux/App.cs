using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Batto.Linux.UI;

namespace Batto.Linux;

public sealed class App : Application
{
    private AppServices? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }

        // The tray keeps the app alive, so closing the window does not exit. Exit only happens through Quit.
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // The view model lives as long as the app; the window is built on open and destroyed on close,
        // so its visual tree can be collected while only the tray runs.
        var viewModel = new MainViewModel();
        MainWindow? window = null;

        void Show()
        {
            if (window == null)
            {
                var created = new MainWindow(viewModel);
                created.Closed += (_, _) =>
                {
                    if (window == created) window = null;
                    if (desktop.MainWindow == created) desktop.MainWindow = null;

                    // With only the tray left the app allocates almost nothing, so the GC would take a long time
                    // to run on its own. One compacting collection once the UI is idle returns the window's memory.
                    Dispatcher.UIThread.Post(() =>
                    {
                        System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
                        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
                    }, DispatcherPriority.Background);
                };
                window = created;
                desktop.MainWindow = created;
            }

            if (!window.IsVisible) window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        }

        void Hide() => window?.Close();

        void Quit() => desktop.Shutdown();

        desktop.Exit += (_, _) => _services?.Dispose();

        if (!Program.StartMinimized) Show();

        _ = StartServicesAsync(viewModel, () => Dispatcher.UIThread.Post(Show), () => Dispatcher.UIThread.Post(Hide), () => Dispatcher.UIThread.Post(Quit));

        base.OnFrameworkInitializationCompleted();
    }

    private async Task StartServicesAsync(MainViewModel viewModel, Action show, Action hide, Action quit)
    {
        try
        {
            _services = await AppServices.CreateAsync(Program.SessionConnection!, show, quit);
            viewModel.Attach(new ShellActions(_services, hide, quit), _services.Settings.Settings);
            _services.StateChanged += () => viewModel.Load(_services.Monitor.ObservableDevices.ToList());
            viewModel.Load(_services.Monitor.ObservableDevices.ToList());
        }
        catch (Exception ex)
        {
            CrashLogger.Log("Linux services startup", ex);
            viewModel.StatusMessage = $"Não foi possível iniciar a leitura Bluetooth: {ex.Message}";
        }
    }
}
