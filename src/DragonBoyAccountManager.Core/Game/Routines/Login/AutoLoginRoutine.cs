using DragonBoyAccountManager.Core.Game.Actions;
using DragonBoyAccountManager.Core.Game.Context;
using DragonBoyAccountManager.Core.Game.Routines.Abstractions;
using DragonBoyAccountManager.Core.Game.Schema;

namespace DragonBoyAccountManager.Core.Game.Routines.Login;

public class AutoLoginRoutine(AutoLoginOptions options)
    : AutomationRoutine<AutoLoginOptions>("Auto Login", options, RoutinePriority.Critical, "AutoLogin")
{
    private int _attempts;

    public int Attempts => _attempts;

    public AutoLoginRoutine(
        string username,
        string password,
        int serverId = 0,
        string? serverName = null,
        int maxAttempts = 10)
        : this(new AutoLoginOptions(username, password, serverId, serverName, maxAttempts))
    { }

    protected override void OnStarted(IGameContext context, IProgress<string>? progress)
    {
        _attempts = 0;
        progress?.Report("[Auto Login] Bắt đầu quá trình đăng nhập...");
    }

    protected override async Task OnTickAsync(
        IGameContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (!context.IsRunning || cancellationToken.IsCancellationRequested)
        {
            Stop();
            return;
        }

        if (context.Player.IsInGame)
        {
            progress?.Report($"[Auto Login] Nhân vật đã vào game: {context.Player.Name}");
            Stop();
            return;
        }

        if (context.UI.IsDownloadingData)
        {
            while (context.UI.IsDownloadingData && !cancellationToken.IsCancellationRequested && context.IsRunning)
            {
                context.UI.TriggerDownloadData();
                int pct = context.UI.DownloadDataPercent;
                string pctText = pct > 0 ? $" ({pct}%)" : "";
                progress?.Report($"[Auto Login] Game đang tải dữ liệu{pctText}...");
                await Task.Delay(1000, cancellationToken);
            }
            await Task.Delay(1500, cancellationToken);
            return;
        }

        _attempts++;
        if (_attempts > Options.MaxAttempts)
        {
            progress?.Report($"[Auto Login] Đã thử đăng nhập {Options.MaxAttempts} lần không thành công.");
            Stop();
            return;
        }

        progress?.Report($"[Auto Login] [Lần {_attempts}/{Options.MaxAttempts}] Kiểm tra trạng thái game...");

        if (context.UI.HasDialog)
        {
            bool isWait = context.UI.IsWaitDialog;
            string? dlgText = context.UI.DialogText;

            if (isWait)
            {
                progress?.Report($"[Auto Login] Chờ phản hồi: {dlgText ?? "Vui lòng chờ..."}");
                var waitDeadline = DateTime.UtcNow.AddSeconds(4);
                while (DateTime.UtcNow < waitDeadline && !cancellationToken.IsCancellationRequested && context.IsRunning)
                {
                    await Task.Delay(300, cancellationToken);
                    if (context.Player.IsInGame)
                    {
                        context.UI.SwitchToGameScreen();
                        Stop();
                        return;
                    }
                    if (!context.UI.HasDialog || !context.UI.IsWaitDialog) break;
                }
            }

            if (!context.IsRunning || cancellationToken.IsCancellationRequested)
            {
                Stop();
                return;
            }

            if (context.UI.HasDialog && !context.UI.IsWaitDialog)
            {
                context.UI.CloseDialog();
                await Task.Delay(400, cancellationToken);
            }
        }

        if (!context.IsRunning || cancellationToken.IsCancellationRequested)
        {
            Stop();
            return;
        }

        if (context.Player.IsInGame)
        {
            Stop();
            return;
        }

        int targetServerId = ResolveTargetServerId(context);
        string? curScreen = context.UI.CurrentScreenName;

        if (curScreen == GameSchema.SplashScr.ClassName || string.IsNullOrEmpty(curScreen))
        {
            progress?.Report("[Auto Login] Đang chờ game tải dữ liệu khởi động...");
            await Task.Delay(1000, cancellationToken);
            return;
        }

        if (curScreen == GameSchema.LoginScr.ClassName)
        {
            if (context.UI.SelectedServerId != targetServerId)
            {
                progress?.Report("[Auto Login] Sai máy chủ, quay lại chọn máy chủ...");
                context.UI.CancelLogin();
                await Task.Delay(500, cancellationToken);
                return;
            }

            progress?.Report("[Auto Login] Điền thông tin tài khoản...");
            bool credsSet = false;
            for (int i = 0; i < 5 && !cancellationToken.IsCancellationRequested; i++)
            {
                if (context.UI.SetLoginCredentials(Options.Username, Options.Password))
                {
                    credsSet = true;
                    break;
                }
                await Task.Delay(200, cancellationToken);
            }

            if (!credsSet)
            {
                progress?.Report("[Auto Login] Không thể điền tài khoản, thử lại...");
                await Task.Delay(500, cancellationToken);
                return;
            }

            await Task.Delay(200, cancellationToken);
            progress?.Report("[Auto Login] Gửi thông tin đăng nhập...");
            context.UI.SubmitLogin();

            var loginWaitTimeout = DateTime.UtcNow.AddSeconds(12);
            while (DateTime.UtcNow < loginWaitTimeout && !cancellationToken.IsCancellationRequested && context.IsRunning)
            {
                await Task.Delay(300, cancellationToken);
                if (context.Player.IsInGame)
                {
                    progress?.Report($"[Auto Login] Đăng nhập thành công! Nhân vật: {context.Player.Name}");
                    Stop();
                    return;
                }

                if (context.UI.HasDialog && !context.UI.IsWaitDialog)
                    break;

                if (context.UI.CurrentScreenName == GameSchema.ServerListScreen.ClassName)
                {
                    await Task.Delay(300, cancellationToken);
                    progress?.Report("[Auto Login] Xác thực hoàn tất, đang vào game...");
                    context.UI.LoginWithSavedCredentials();
                    break;
                }
            }
        }
        else if (curScreen == GameSchema.ServerListScreen.ClassName)
        {
            if (context.UI.SelectedServerId != targetServerId)
            {
                progress?.Report($"[Auto Login] Chọn máy chủ #{targetServerId}...");
                context.UI.SelectServer(targetServerId);
                await Task.Delay(400, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(Options.Username) && !string.IsNullOrWhiteSpace(Options.Password))
            {
                progress?.Report("[Auto Login] Mở màn hình đăng nhập...");
                context.UI.OpenLoginScreen();
                await Task.Delay(600, cancellationToken);
            }
            else
            {
                progress?.Report("[Auto Login] Mở màn hình đăng nhập...");
                context.UI.LoginWithSavedCredentials();
                await Task.Delay(600, cancellationToken);
            }
        }
        else
        {
            context.UI.SwitchToLoginScreen();
            await Task.Delay(800, cancellationToken);
        }
    }

    private int ResolveTargetServerId(IGameContext context)
    {
        if (!string.IsNullOrWhiteSpace(Options.ServerName))
        {
            var liveNames = context.Network.ServerNames;
            if (liveNames != null && liveNames.Length > 0)
            {
                for (int i = 0; i < liveNames.Length; i++)
                {
                    if (string.Equals(liveNames[i], Options.ServerName, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }
        }
        return Options.ServerId;
    }
}
