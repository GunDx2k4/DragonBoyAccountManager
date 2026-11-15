using DragonBoyAccountManager.Core.Engine.Models;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Sessions.Controllers;

namespace DragonBoyAccountManager.Core.Sessions;

public sealed class LoginOrchestrator(AccountLaunchOptions options, LoginScreenController loginScreen, bool isMasked)
{
    private const int MaxSplashWaitTicks = 60;
    private const int TickDelayMs = 100;

    private readonly AccountLaunchOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly LoginScreenController _loginScreen = loginScreen ?? throw new ArgumentNullException(nameof(loginScreen));
    private readonly bool _isMasked = isMasked;
    private readonly int _maxAttempts = options.AutoLogin ? 15 : 3;

    private int _attempts;
    private int _splashWaitTicks;

    public async Task<bool> ExecuteAsync(
        ISessionContext context,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _attempts = 0;
        _splashWaitTicks = 0;
        progress?.Report("[Auto Login] Bắt đầu quá trình đăng nhập...");

        while (!cancellationToken.IsCancellationRequested)
        {
            var outcome = await ExecuteStepAsync(context, progress, cancellationToken);
            if (outcome.HasValue)
            {
                return outcome.Value;
            }

            await Task.Delay(TickDelayMs, cancellationToken);
        }

        return false;
    }

    private async Task<bool?> ExecuteStepAsync(
        ISessionContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var player = context.CurrentPlayer;
        if (player.IsInGame)
        {
            if (_isMasked)
            {
                _loginScreen.ClearSensitiveInputs();
            }
            progress?.Report($"[Auto Login] Nhân vật đã vào game: {player.Name}");
            return true;
        }

        string? curScreen = _loginScreen.GetCurrentScreenName();
        if (curScreen == GameSchema.SplashScr.ClassName || string.IsNullOrEmpty(curScreen))
        {
            _splashWaitTicks++;
            if (_splashWaitTicks > MaxSplashWaitTicks)
            {
                progress?.Report("[Auto Login] Quá thời gian chờ tải dữ liệu khởi động game.");
                return false;
            }
            progress?.Report($"[Auto Login] Đang chờ game tải dữ liệu khởi động ({_splashWaitTicks}s)...");
            await Task.Delay(1000, cancellationToken);
            return null;
        }

        if (_loginScreen.IsDownloadingData())
        {
            await WaitForInitialDownloadAsync(progress, cancellationToken);
        }

        _attempts++;
        if (_attempts > _maxAttempts)
        {
            progress?.Report($"[Auto Login] Đã thử đăng nhập {_maxAttempts} lần không thành công.");
            return false;
        }

        progress?.Report($"[Auto Login] [Lần {_attempts}/{_maxAttempts}] Kiểm tra trạng thái game...");

        if (!await HandleActiveDialogsAsync(progress, cancellationToken))
        {
            return false;
        }

        int targetServerId = _loginScreen.ResolveTargetServerId(_options.ServerId, _options.ServerName);

        if (curScreen == GameSchema.LoginScr.ClassName)
        {
            if (await HandleLoginScreenAsync(targetServerId, progress, cancellationToken))
            {
                return true;
            }
        }
        else if (curScreen == GameSchema.ServerListScreen.ClassName)
        {
            if (await HandleServerListScreenAsync(targetServerId, progress, cancellationToken))
            {
                return true;
            }
        }
        else if (curScreen == GameSchema.SelectCharScr.ClassName)
        {
            progress?.Report("[Auto Login] Đang ở màn hình chọn nhân vật, tiến hành vào game...");
            _loginScreen.PerformScreenAction(GameSchema.SelectCharScr.ClassName, GameSchema.SelectCharScr.ActionSelect);
            await Task.Delay(800, cancellationToken);
        }
        else if (curScreen == GameSchema.ChooseCharScr.ClassName)
        {
            progress?.Report("[Auto Login] Đang ở màn hình chọn nhân vật, tiến hành vào game...");
            _loginScreen.PerformScreenAction(GameSchema.ChooseCharScr.ClassName, GameSchema.ChooseCharScr.ActionSelect);
            await Task.Delay(800, cancellationToken);
        }
        else
        {
            _loginScreen.SwitchToLoginScreen(_isMasked);
            await Task.Delay(800, cancellationToken);
        }

        return null;
    }

    private async Task WaitForInitialDownloadAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        _loginScreen.TriggerDownloadData();

        while (_loginScreen.IsDownloadingData() && !cancellationToken.IsCancellationRequested)
        {
            int pct = _loginScreen.GetDownloadDataPercent();
            string pctText = pct > 0 ? $" ({pct}%)" : string.Empty;
            progress?.Report($"[Auto Login] Game đang tải dữ liệu{pctText}...");
            await Task.Delay(1000, cancellationToken);
        }

        progress?.Report("[Auto Login] Tải dữ liệu hoàn tất! Chờ game tự động chuyển giao diện...");

        var stabilizeDeadline = DateTime.UtcNow.AddSeconds(8);
        while (DateTime.UtcNow < stabilizeDeadline && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(500, cancellationToken);

            if (!_loginScreen.IsLoadingControllerData() && !_loginScreen.IsUpdatingGameData())
            {
                string? screen = _loginScreen.GetCurrentScreenName();
                if (screen == GameSchema.ServerListScreen.ClassName || screen == GameSchema.LoginScr.ClassName)
                {
                    await Task.Delay(1000, cancellationToken);
                    break;
                }
            }
        }

        progress?.Report("[Auto Login] Giao diện đã sẵn sàng, tiến hành đăng nhập...");
    }

    private async Task<bool> HandleActiveDialogsAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (_loginScreen.HasActiveDialog(out bool isWait, out string? dlgText))
        {
            if (isWait)
            {
                progress?.Report($"[Auto Login] Chờ phản hồi: {dlgText ?? "Vui lòng chờ..."}");
                var waitDeadline = DateTime.UtcNow.AddSeconds(4);
                while (DateTime.UtcNow < waitDeadline && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(300, cancellationToken);
                    if (!_loginScreen.IsWaitDialogPresent())
                        break;
                }
            }

            if (cancellationToken.IsCancellationRequested)
                return false;

            if (_loginScreen.IsNonWaitDialogPresent())
            {
                _loginScreen.CloseDialog();
                await Task.Delay(400, cancellationToken);
            }
        }

        return !cancellationToken.IsCancellationRequested;
    }

    private async Task<bool> HandleServerListScreenAsync(
        int targetServerId,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        _loginScreen.PreMaskServerListScreen(_isMasked);

        if (!_loginScreen.IsServerListReady())
        {
            progress?.Report("[Auto Login] Chờ tải xong tài nguyên game trước khi tiếp tục...");
            await Task.Delay(1000, cancellationToken);
            return false;
        }

        int curSelectedServer = _loginScreen.GetSelectedServerId();
        if (curSelectedServer >= 0 && curSelectedServer != targetServerId)
        {
            progress?.Report($"[Auto Login] Chọn máy chủ #{targetServerId}...");
            _loginScreen.SelectServer(targetServerId);
            await Task.Delay(500, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(_options.Username) && !string.IsNullOrWhiteSpace(_options.Password))
        {
            progress?.Report("[Auto Login] Mở màn hình đăng nhập...");
            _loginScreen.OpenLoginScreen(_isMasked);

            var waitLoginScrDeadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < waitLoginScrDeadline && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(300, cancellationToken);
                if (_loginScreen.GetCurrentScreenName() == GameSchema.LoginScr.ClassName)
                    break;
            }
            await Task.Delay(400, cancellationToken);

            if (_loginScreen.GetCurrentScreenName() != GameSchema.LoginScr.ClassName)
            {
                _loginScreen.SwitchToLoginScreen(_isMasked);
                await Task.Delay(500, cancellationToken);
            }

            if (_loginScreen.GetCurrentScreenName() == GameSchema.LoginScr.ClassName)
            {
                return await HandleLoginScreenAsync(targetServerId, progress, cancellationToken);
            }
        }
        else
        {
            progress?.Report("[Auto Login] Mở màn hình đăng nhập...");
            _loginScreen.LoginWithSavedCredentials();
            await Task.Delay(600, cancellationToken);
        }

        return false;
    }

    private async Task<bool> HandleLoginScreenAsync(
        int targetServerId,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        int curSelectedServer = _loginScreen.GetSelectedServerId();
        if (curSelectedServer >= 0 && curSelectedServer != targetServerId)
        {
            progress?.Report("[Auto Login] Sai máy chủ, quay lại chọn máy chủ...");
            _loginScreen.CancelLogin();
            await Task.Delay(500, cancellationToken);
            return false;
        }

        progress?.Report("[Auto Login] Điền thông tin tài khoản...");
        bool credsSet = false;
        for (int i = 0; i < 5 && !cancellationToken.IsCancellationRequested; i++)
        {
            if (_loginScreen.SetLoginCredentials(_options.Username, _options.Password, _isMasked))
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
            return false;
        }

        await Task.Delay(200, cancellationToken);
        progress?.Report("[Auto Login] Gửi thông tin đăng nhập...");
        _loginScreen.SubmitLogin(_isMasked, _options.Username, _options.Password);

        var loginWaitTimeout = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < loginWaitTimeout && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(300, cancellationToken);

            if (_loginScreen.IsUpdatingGameData())
            {
                progress?.Report("[Auto Login] Máy chủ yêu cầu cập nhật dữ liệu (map, skill, item)... Đang tải...");
                var updateDeadline = DateTime.UtcNow.AddSeconds(90);
                while (_loginScreen.IsUpdatingGameData() && DateTime.UtcNow < updateDeadline && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, cancellationToken);
                }
                progress?.Report("[Auto Login] Cập nhật dữ liệu hoàn tất! Chờ ổn định...");
                await Task.Delay(1500, cancellationToken);
                loginWaitTimeout = DateTime.UtcNow.AddSeconds(15);
                continue;
            }

            if (_loginScreen.IsWaitingToLogin())
            {
                loginWaitTimeout = DateTime.UtcNow.AddSeconds(15);
                await Task.Delay(400, cancellationToken);
                continue;
            }

            string? checkScreen = _loginScreen.GetCurrentScreenName();
            if (checkScreen == GameSchema.GameScr.ClassName)
            {
                return true;
            }

            if (checkScreen == GameSchema.SelectCharScr.ClassName)
            {
                progress?.Report("[Auto Login] Chọn nhân vật vào game...");
                await Task.Delay(500, cancellationToken);
                _loginScreen.PerformScreenAction(GameSchema.SelectCharScr.ClassName, GameSchema.SelectCharScr.ActionSelect);
                loginWaitTimeout = DateTime.UtcNow.AddSeconds(15);
                continue;
            }

            if (checkScreen == GameSchema.ChooseCharScr.ClassName)
            {
                progress?.Report("[Auto Login] Chọn nhân vật vào game...");
                await Task.Delay(500, cancellationToken);
                _loginScreen.PerformScreenAction(GameSchema.ChooseCharScr.ClassName, GameSchema.ChooseCharScr.ActionSelect);
                loginWaitTimeout = DateTime.UtcNow.AddSeconds(15);
                continue;
            }

            if (_loginScreen.IsNonWaitDialogPresent())
                break;

            if (checkScreen == GameSchema.ServerListScreen.ClassName)
            {
                if (_loginScreen.IsDownloadingData())
                {
                    progress?.Report("[Auto Login] Máy chủ yêu cầu tải dữ liệu...");
                    var dlDeadline = DateTime.UtcNow.AddSeconds(90);
                    while (_loginScreen.IsDownloadingData() && DateTime.UtcNow < dlDeadline && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                    await Task.Delay(1500, cancellationToken);
                }

                await Task.Delay(300, cancellationToken);
                progress?.Report("[Auto Login] Xác thực hoàn tất, đang vào game...");
                if (!string.IsNullOrWhiteSpace(_options.Username) && !string.IsNullOrWhiteSpace(_options.Password))
                {
                    _loginScreen.SubmitLogin(_isMasked, _options.Username, _options.Password);
                }
                else
                {
                    _loginScreen.LoginWithSavedCredentials();
                }
                loginWaitTimeout = DateTime.UtcNow.AddSeconds(15);
            }
        }

        return false;
    }
}
