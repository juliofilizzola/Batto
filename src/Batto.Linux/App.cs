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

        // The window is hidden, not closed, so the tray keeps the app alive. Exit only happens through Quit.
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var viewModel = new MainViewModel();
        var window = new MainWindow(viewModel);
        desktop.MainWindow = window;

        bool quitting = false;

        void Show()
        {
            if (!window.IsVisible) window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        }

        void Hide() => window.Hide();

        void Quit()
        {
            quitting = true;
            desktop.Shutdown();
        }

        window.Closing += (_, e) =>
        {
            if (quitting) return;
            e.Cancel = true;
            window.Hide();
        };
        desktop.Exit += (_, _) => _services?.Dispose();

        if (!Program.StartMinimized) window.Show();

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
