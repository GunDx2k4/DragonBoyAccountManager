using System;
using System.Windows;
using DragonBoyAccountManager.Core.Game;
using DragonBoyAccountManager.Core.Updater;
using DragonBoyAccountManager.GUI.Services.Dialog;
using DragonBoyAccountManager.GUI.Services.Feedback;
using DragonBoyAccountManager.GUI.Services.GameEngine;
using DragonBoyAccountManager.GUI.Services.Settings;
using DragonBoyAccountManager.GUI.Services.Storage;
using DragonBoyAccountManager.GUI.ViewModels;
using DragonBoyAccountManager.GUI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DragonBoyAccountManager.GUI;

public partial class App : Application
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
        services.AddSingleton<IGameClientValidator, GameClientValidator>();
        services.AddSingleton<IAppUpdaterService, AppUpdaterService>();
        services.AddSingleton<IAccountStorageService, AccountStorageService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IGameEngineService, CoreGameEngineAdapter>();
        services.AddSingleton<IFeedbackService, DiscordFeedbackService>();

        services.AddSingleton<AccountsPanelViewModel>();
        services.AddSingleton<AccountFormViewModel>();
        services.AddSingleton<AppStatusBarViewModel>();
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
