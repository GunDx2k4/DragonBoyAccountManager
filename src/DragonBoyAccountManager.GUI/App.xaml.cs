using System;
using System.Windows;

using Microsoft.Extensions.DependencyInjection;

namespace DragonBoyAccountManager.GUI;

public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IDispatcherService, WpfDispatcherService>();
        services.AddSingleton<IActiveSelectionService, ActiveSelectionStore>();
        services.AddSingleton<IWindowManagerService, WindowManagerService>();
        services.AddSingleton<IGameClientValidator, GameClientValidator>();
        services.AddSingleton<IGameEnvironmentService, GameEnvironmentService>();
        services.AddSingleton<IAppUpdaterService, AppUpdaterService>();
        services.AddSingleton<IAccountStorageService, AccountStorageService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IGameEngine, GameEngine>();
        services.AddSingleton<CoreGameEngineAdapter>();
        services.AddSingleton<IGameEngineService>(sp => sp.GetRequiredService<CoreGameEngineAdapter>());
        services.AddSingleton<ISessionManagementService>(sp => sp.GetRequiredService<CoreGameEngineAdapter>());
        services.AddSingleton<IGameWindowControlService>(sp => sp.GetRequiredService<CoreGameEngineAdapter>());
        services.AddSingleton<IGameActionService>(sp => sp.GetRequiredService<CoreGameEngineAdapter>());
        services.AddSingleton<IGameAutomationService>(sp => sp.GetRequiredService<CoreGameEngineAdapter>());
        services.AddSingleton<IGameInputService, GameInputService>();
        services.AddSingleton<IGameImageService, GameImageService>();
        services.AddSingleton<IFeedbackService, DiscordFeedbackService>();
        services.AddSingleton<IHotkeyRoutingService, HotkeyRoutingService>();

        services.AddSingleton<AccountsPanelViewModel>();
        services.AddSingleton<AccountFormViewModel>();
        services.AddSingleton<AppStatusBarViewModel>();
        services.AddSingleton<AppTitleBarViewModel>();
        services.AddSingleton<SettingsTabViewModel>();
        services.AddSingleton<GuideViewModel>();
        services.AddSingleton<UtilitiesTabViewModel>();
        services.AddSingleton<AutoTabViewModel>();
        services.AddSingleton<FeedbackViewModel>();
        services.AddSingleton<MainViewModel>();

        services.AddSingleton<MainWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var updater = Services.GetService<IAppUpdaterService>();
            updater?.CleanUpOld();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Lỗi dọn dẹp file cũ: {ex.Message}");
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                var gameEngine = Services.GetService<IGameEngineService>();
                gameEngine?.StopAll();
            }
            catch { }
        };

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            var gameEngine = Services.GetService<IGameEngineService>();
            gameEngine?.StopAll();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Lỗi khi dừng game engine lúc thoát: {ex.Message}");
        }

        base.OnExit(e);
    }
}
