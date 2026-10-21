using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Interop.Mono;
using DragonBoyAccountManager.Core.Interop.Mono.Schema;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Behaviors.Lifecycle;

public sealed class AutoLoginOptions
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public int ServerId { get; init; }
    public string? ServerName { get; init; }
    public int MaxAttempts { get; init; } = 10;
}

public sealed class AutoLoginBehavior : AutomationBehavior<AutoLoginOptions>
{
    private readonly IMonoRuntime _mono;
    private int _attempts;

    public int Attempts => _attempts;

    public AutoLoginBehavior(AutoLoginOptions options, IMonoRuntime mono)
        : base("Auto Login", options, "AutoLogin")
    {
        _mono = mono ?? throw new ArgumentNullException(nameof(mono));
    }

    protected override void OnStarted(IProgress<string>? progress)
    {
        _attempts = 0;
        progress?.Report("[Auto Login] Bắt đầu quá trình đăng nhập...");
    }

    protected override async Task OnTickAsync(
        PlayerSnapshot player,
        WorldSnapshot world,
        MapSnapshot map,
        ICommandDispatcher dispatcher,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (player.IsInGame)
        {
            progress?.Report($"[Auto Login] Nhân vật đã vào game: {player.Name}");
            Stop();
            return;
        }

        if (IsDownloadingData())
        {
            TriggerDownloadData();

            while (IsDownloadingData() && !cancellationToken.IsCancellationRequested)
            {
                int pct = GetDownloadDataPercent();
                string pctText = pct > 0 ? $" ({pct}%)" : string.Empty;
                progress?.Report($"[Auto Login] Game đang tải dữ liệu{pctText}...");
                await Task.Delay(1000, cancellationToken);
            }

            progress?.Report("[Auto Login] Tải dữ liệu hoàn tất! Chờ game tự động chuyển giao diện...");

            var stabilizeDeadline = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < stabilizeDeadline && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(500, cancellationToken);

                bool isLoading = false;
                try { isLoading = _mono.ReadStatic<bool>(GameSchema.Controller.ClassName, GameSchema.Controller.StaticIsLoadingData); } catch { }
                if (!isLoading && !IsUpdatingGameData())
                {
                    string? s = GetCurrentScreenName();
                    if (s == GameSchema.ServerListScreen.ClassName || s == GameSchema.LoginScr.ClassName)
                    {
                        await Task.Delay(1000, cancellationToken);
                        break;
                    }
                }
            }

            progress?.Report("[Auto Login] Giao diện đã sẵn sàng, tiến hành đăng nhập...");
        }

        _attempts++;
        if (_attempts > Options.MaxAttempts)
        {
            progress?.Report($"[Auto Login] Đã thử đăng nhập {Options.MaxAttempts} lần không thành công.");
            Stop();
            return;
        }

        progress?.Report($"[Auto Login] [Lần {_attempts}/{Options.MaxAttempts}] Kiểm tra trạng thái game...");

        IntPtr currentDialog = GetCurrentDialogPointer();
        if (currentDialog != IntPtr.Zero)
        {
            bool isWait = IsWaitDialog(currentDialog);
            string? dlgText = GetDialogText(currentDialog);

            if (isWait)
            {
                progress?.Report($"[Auto Login] Chờ phản hồi: {dlgText ?? "Vui lòng chờ..."}");
                var waitDeadline = DateTime.UtcNow.AddSeconds(4);
                while (DateTime.UtcNow < waitDeadline && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(300, cancellationToken);
                    if (GetCurrentDialogPointer() == IntPtr.Zero || !IsWaitDialog(GetCurrentDialogPointer()))
                        break;
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Stop();
                return;
            }

            currentDialog = GetCurrentDialogPointer();
            if (currentDialog != IntPtr.Zero && !IsWaitDialog(currentDialog))
            {
                CloseDialog();
                await Task.Delay(400, cancellationToken);
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            Stop();
            return;
        }

        int targetServerId = ResolveTargetServerId();
        string? curScreen = GetCurrentScreenName();

        if (curScreen == GameSchema.SplashScr.ClassName || string.IsNullOrEmpty(curScreen))
        {
            progress?.Report("[Auto Login] Đang chờ game tải dữ liệu khởi động...");
            await Task.Delay(1000, cancellationToken);
            return;
        }

        if (curScreen == GameSchema.LoginScr.ClassName)
        {
            await HandleLoginScreenAsync(targetServerId, progress, cancellationToken);
        }
        else if (curScreen == GameSchema.ServerListScreen.ClassName)
        {
            bool loadScreen = false;
            try { loadScreen = _mono.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticLoadScreen); } catch { }
            bool bigOk = false;
            try { bigOk = _mono.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticBigOk); } catch { }

            int percent = 0;
            try { percent = _mono.ReadStatic<int>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticPercent); } catch { }

            if (!loadScreen && !bigOk && percent < 100)
            {
                progress?.Report("[Auto Login] Chờ tải xong tài nguyên game trước khi tiếp tục...");
                await Task.Delay(1000, cancellationToken);
                return;
            }

            int curSelectedServer = GetSelectedServerId();
            if (curSelectedServer >= 0 && curSelectedServer != targetServerId)
            {
                progress?.Report($"[Auto Login] Chọn máy chủ #{targetServerId}...");
                SelectServer(targetServerId);
                await Task.Delay(500, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(Options.Username) && !string.IsNullOrWhiteSpace(Options.Password))
            {
                progress?.Report("[Auto Login] Mở màn hình đăng nhập...");
                OpenLoginScreen();

                var waitLoginScrDeadline = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < waitLoginScrDeadline && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(300, cancellationToken);
                    if (GetCurrentScreenName() == GameSchema.LoginScr.ClassName)
                        break;
                }
                await Task.Delay(400, cancellationToken);

                if (GetCurrentScreenName() == GameSchema.LoginScr.ClassName)
                {
                    await HandleLoginScreenAsync(targetServerId, progress, cancellationToken);
                }
            }
            else
            {
                progress?.Report("[Auto Login] Mở màn hình đăng nhập...");
                LoginWithSavedCredentials();
                await Task.Delay(600, cancellationToken);
            }
        }
        else if (curScreen == "SelectCharScr")
        {
            progress?.Report("[Auto Login] Đang ở màn hình chọn nhân vật, tiến hành vào game...");
            PerformScreenAction("SelectCharScr", 100);
            await Task.Delay(800, cancellationToken);
        }
        else if (curScreen == "ChooseCharScr")
        {
            progress?.Report("[Auto Login] Đang ở màn hình chọn nhân vật, tiến hành vào game...");
            PerformScreenAction("ChooseCharScr", 1);
            await Task.Delay(800, cancellationToken);
        }
        else
        {
            SwitchToLoginScreen();
            await Task.Delay(800, cancellationToken);
        }
    }

    private async Task HandleLoginScreenAsync(
        int targetServerId,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        int curSelectedServer = GetSelectedServerId();
        if (curSelectedServer >= 0 && curSelectedServer != targetServerId)
        {
            progress?.Report("[Auto Login] Sai máy chủ, quay lại chọn máy chủ...");
            CancelLogin();
            await Task.Delay(500, cancellationToken);
            return;
        }

        progress?.Report("[Auto Login] Điền thông tin tài khoản...");
        bool credsSet = false;
        for (int i = 0; i < 5 && !cancellationToken.IsCancellationRequested; i++)
        {
            if (SetLoginCredentials(Options.Username, Options.Password))
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
        SubmitLogin();

        var loginWaitTimeout = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < loginWaitTimeout && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(300, cancellationToken);

            if (IsUpdatingGameData())
            {
                progress?.Report("[Auto Login] Máy chủ yêu cầu cập nhật dữ liệu (map, skill, item)... Đang tải...");
                var updateDeadline = DateTime.UtcNow.AddSeconds(90);
                while (IsUpdatingGameData() && DateTime.UtcNow < updateDeadline && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, cancellationToken);
                }
                progress?.Report("[Auto Login] Cập nhật dữ liệu hoàn tất! Chờ ổn định...");
                await Task.Delay(1500, cancellationToken);
                loginWaitTimeout = DateTime.UtcNow.AddSeconds(15);
                continue;
            }

            bool waitToLogin = false;
            try { waitToLogin = _mono.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticWaitToLogin); } catch { }
            if (waitToLogin)
            {
                loginWaitTimeout = DateTime.UtcNow.AddSeconds(15);
                await Task.Delay(400, cancellationToken);
                continue;
            }

            string? checkScreen = GetCurrentScreenName();
            if (checkScreen == GameSchema.GameScr.ClassName)
            {
                Stop();
                return;
            }

            if (checkScreen == "SelectCharScr")
            {
                progress?.Report("[Auto Login] Chọn nhân vật vào game...");
                await Task.Delay(500, cancellationToken);
                PerformScreenAction("SelectCharScr", 100);
                loginWaitTimeout = DateTime.UtcNow.AddSeconds(15);
                continue;
            }

            if (checkScreen == "ChooseCharScr")
            {
                progress?.Report("[Auto Login] Chọn nhân vật vào game...");
                await Task.Delay(500, cancellationToken);
                PerformScreenAction("ChooseCharScr", 1);
                loginWaitTimeout = DateTime.UtcNow.AddSeconds(15);
                continue;
            }

            IntPtr dlg = GetCurrentDialogPointer();
            if (dlg != IntPtr.Zero && !IsWaitDialog(dlg))
                break;

            if (checkScreen == GameSchema.ServerListScreen.ClassName)
            {
                if (IsDownloadingData())
                {
                    progress?.Report("[Auto Login] Máy chủ yêu cầu tải dữ liệu...");
                    var dlDeadline = DateTime.UtcNow.AddSeconds(90);
                    while (IsDownloadingData() && DateTime.UtcNow < dlDeadline && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                    await Task.Delay(1500, cancellationToken);
                }

                await Task.Delay(300, cancellationToken);
                progress?.Report("[Auto Login] Xác thực hoàn tất, đang vào game...");
                LoginWithSavedCredentials();
                loginWaitTimeout = DateTime.UtcNow.AddSeconds(15);
            }
        }
    }

    private string? GetCurrentScreenName()
    {
        try
        {
            IntPtr scr = _mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticCurrentScreen);
            return scr != IntPtr.Zero ? _mono.GetClassName(scr) : null;
        }
        catch { return null; }
    }

    private IntPtr GetCurrentDialogPointer()
    {
        try { return _mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticCurrentDialog); }
        catch { return IntPtr.Zero; }
    }

    private bool IsWaitDialog(IntPtr dlg)
    {
        if (dlg == IntPtr.Zero) return false;
        try { return _mono.ReadField<bool>(GameSchema.MsgDlg.ClassName, GameSchema.MsgDlg.FieldIsWait, dlg); }
        catch { return false; }
    }

    private string? GetDialogText(IntPtr dlg)
    {
        if (dlg == IntPtr.Zero) return null;
        try { return _mono.ReadStringField(GameSchema.MsgDlg.ClassName, GameSchema.MsgDlg.FieldInfo, dlg); }
        catch { return null; }
    }

    private void CloseDialog()
    {
        try
        {
            IntPtr canvas = _mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticInstance);
            if (canvas == IntPtr.Zero)
                canvas = _mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticGameCanvas);
            if (canvas != IntPtr.Zero)
            {
                _mono.CallMethod(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.MethodEndDlg, canvas);
            }
        }
        catch { }
    }

    private bool IsUpdatingGameData()
    {
        try
        {
            if (_mono.ReadStatic<bool>(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.StaticIsUpdateAll)) return true;
            if (_mono.ReadStatic<bool>(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.StaticIsUpdateData)) return true;
            if (_mono.ReadStatic<bool>(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.StaticIsUpdateMap)) return true;
            if (_mono.ReadStatic<bool>(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.StaticIsUpdateSkill)) return true;
            if (_mono.ReadStatic<bool>(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.StaticIsUpdateItem)) return true;
            return false;
        }
        catch { return false; }
    }

    private bool IsDownloadingData()
    {
        try
        {
            if (_mono.ReadStatic<bool>(GameSchema.Controller.ClassName, GameSchema.Controller.StaticIsLoadingData))
                return true;

            if (IsUpdatingGameData())
                return true;

            string? curScr = GetCurrentScreenName();
            if (curScr == GameSchema.SplashScr.ClassName)
            {
                int nData = _mono.ReadStatic<int>(GameSchema.SplashScr.ClassName, GameSchema.SplashScr.StaticNData);
                if (nData != -1) return true;
            }

            if (curScr == GameSchema.ServerListScreen.ClassName)
            {
                bool isGetData = false;
                try { isGetData = _mono.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticIsGetData); } catch { }
                if (isGetData) return true;

                bool loadScreen = false;
                try { loadScreen = _mono.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticLoadScreen); } catch { }
                bool bigOk = false;
                try { bigOk = _mono.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticBigOk); } catch { }

                int percent = 0;
                try { percent = _mono.ReadStatic<int>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticPercent); } catch { }

                if (!loadScreen && !bigOk && percent < 100)
                    return true;
            }

            return false;
        }
        catch { return false; }
    }

    private int GetDownloadDataPercent()
    {
        try
        {
            string? curScr = GetCurrentScreenName();
            if (curScr == GameSchema.SplashScr.ClassName)
            {
                int nData = _mono.ReadStatic<int>(GameSchema.SplashScr.ClassName, GameSchema.SplashScr.StaticNData);
                int maxData = _mono.ReadStatic<int>(GameSchema.SplashScr.ClassName, GameSchema.SplashScr.StaticMaxData);
                if (nData != -1 && maxData > 0)
                {
                    return Math.Clamp(nData * 100 / maxData, 0, 100);
                }
            }

            if (curScr == GameSchema.ServerListScreen.ClassName)
            {
                return _mono.ReadStatic<int>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticPercent);
            }

            return 0;
        }
        catch { return 0; }
    }

    private bool TriggerDownloadData()
    {
        try
        {
            string? curScr = GetCurrentScreenName();
            if (curScr == GameSchema.ServerListScreen.ClassName)
            {
                bool isGetData = false;
                try { isGetData = _mono.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticIsGetData); } catch { }
                if (isGetData)
                {
                    return false;
                }

                bool loadScreen = false;
                try { loadScreen = _mono.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticLoadScreen); } catch { }
                bool bigOk = false;
                try { bigOk = _mono.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticBigOk); } catch { }

                int percent = 0;
                try { percent = _mono.ReadStatic<int>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticPercent); } catch { }

                if (!loadScreen && !bigOk && percent < 100)
                {
                    PerformScreenAction(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.ActionDownload);
                    return true;
                }
            }
            return false;
        }
        catch { return false; }
    }

    private int GetSelectedServerId()
    {
        try { return _mono.ReadStatic<int>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticIpSelect); }
        catch { return -1; }
    }

    private void SelectServer(int serverId)
    {
        try
        {
            IntPtr serverScreen = _mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticServerScreen);
            if (serverScreen != IntPtr.Zero)
            {
                _mono.WriteStatic(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticIpSelect, serverId);
                _mono.CallMethod(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.MethodSelectServer, serverScreen);
                return;
            }
            _mono.CallMethod(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.MethodSetIpSelect, IntPtr.Zero, serverId, false);
            _mono.WriteStatic(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticIpSelect, serverId);
        }
        catch { }
    }

    private void OpenLoginScreen()
    {
        try
        {
            IntPtr serverScreen = _mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticServerScreen);
            if (serverScreen != IntPtr.Zero)
            {
                _mono.CallMethod(GameSchema.ServerListScreen.ClassName, GameSchema.Screen.MethodPerform, serverScreen, GameSchema.ServerListScreen.ActionOpenLogin, null);
                return;
            }
        }
        catch { }
        PerformScreenAction(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.ActionOpenLogin);
    }

    private void LoginWithSavedCredentials()
    {
        try
        {
            IntPtr serverScreen = _mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticServerScreen);
            if (serverScreen != IntPtr.Zero)
            {
                _mono.CallMethod(GameSchema.ServerListScreen.ClassName, GameSchema.Screen.MethodPerform, serverScreen, GameSchema.ServerListScreen.ActionLoginSaved, null);
                return;
            }
        }
        catch { }
        PerformScreenAction(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.ActionLoginSaved);
    }

    private void CancelLogin() => PerformScreenAction(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.ActionCancel);

    private void SubmitLogin()
    {
        IntPtr loginScr = GetLoginScreenPointer();
        if (loginScr != IntPtr.Zero)
        {
            try
            {
                _mono.CallMethod(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.MethodDoLogin, loginScr);
                return;
            }
            catch { }
        }
        PerformScreenAction(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.ActionSubmit);
    }

    private IntPtr GetLoginScreenPointer()
    {
        try
        {
            IntPtr cur = _mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticCurrentScreen);
            if (cur != IntPtr.Zero && _mono.GetClassName(cur) == GameSchema.LoginScr.ClassName)
                return cur;

            return _mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticLoginScr);
        }
        catch { return IntPtr.Zero; }
    }

    private bool SetLoginCredentials(string username, string password)
    {
        username ??= string.Empty;
        password ??= string.Empty;

        IntPtr loginScr = GetLoginScreenPointer();
        if (loginScr == IntPtr.Zero) return false;

        IntPtr tfUser = _mono.ReadFieldPtr(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldTfUser, loginScr);
        IntPtr tfPass = _mono.ReadFieldPtr(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldTfPass, loginScr);
        if (tfUser == IntPtr.Zero || tfPass == IntPtr.Zero) return false;

        SetTFieldText(tfUser, username);
        SetTFieldText(tfPass, password);

        try
        {
            _mono.CallMethod(GameSchema.Rms.ClassName, GameSchema.Rms.MethodSaveRMSString, IntPtr.Zero, GameSchema.Rms.KeyAcc, username.Trim());
            _mono.CallMethod(GameSchema.Rms.ClassName, GameSchema.Rms.MethodSaveRMSString, IntPtr.Zero, GameSchema.Rms.KeyPass, password.Trim());
        }
        catch { }

        string curUser = GetTFieldText(tfUser) ?? string.Empty;
        string curPass = GetTFieldText(tfPass) ?? string.Empty;

        return string.Equals(curUser.Trim(), username.Trim(), StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrEmpty(curUser) && !string.IsNullOrEmpty(curPass));
    }

    private void SetTFieldText(IntPtr tfieldPtr, string text)
    {
        if (tfieldPtr == IntPtr.Zero) return;
        text ??= string.Empty;

        try { _mono.CallMethod(GameSchema.TField.ClassName, GameSchema.TField.MethodSetText, tfieldPtr, text); }
        catch { }

        string cur = GetTFieldText(tfieldPtr) ?? string.Empty;
        if (string.Equals(cur, text, StringComparison.Ordinal)) return;

        IntPtr monoStr = _mono.CreateMonoString(text);
        if (monoStr != IntPtr.Zero)
        {
            _mono.WriteField(GameSchema.TField.ClassName, GameSchema.TField.FieldText, tfieldPtr, monoStr);
            _mono.WriteField(GameSchema.TField.ClassName, GameSchema.TField.FieldPaintedText, tfieldPtr, monoStr);
            _mono.WriteField(GameSchema.TField.ClassName, GameSchema.TField.FieldCaretPos, tfieldPtr, text.Length);

            string stars = new string('*', text.Length);
            IntPtr starStr = _mono.CreateMonoString(stars);
            if (starStr != IntPtr.Zero)
            {
                _mono.WriteField(GameSchema.TField.ClassName, GameSchema.TField.FieldPasswordText, tfieldPtr, starStr);
            }
        }
    }

    private string? GetTFieldText(IntPtr tfieldPtr)
    {
        if (tfieldPtr == IntPtr.Zero) return null;
        try
        {
            IntPtr res = _mono.CallMethod(GameSchema.TField.ClassName, GameSchema.TField.MethodGetText, tfieldPtr);
            if (res != IntPtr.Zero)
            {
                string text = _mono.ReadMonoString(res);
                if (!string.IsNullOrEmpty(text))
                    return text;
            }
        }
        catch { }

        try { return _mono.ReadStringField(GameSchema.TField.ClassName, GameSchema.TField.FieldText, tfieldPtr); }
        catch { return null; }
    }

    private void SwitchToLoginScreen()
    {
        try
        {
            IntPtr loginScr = _mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticLoginScr);
            if (loginScr == IntPtr.Zero)
            {
                loginScr = _mono.CreateInstance(GameSchema.LoginScr.ClassName);
                if (loginScr != IntPtr.Zero)
                {
                    _mono.WriteStatic(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticLoginScr, loginScr);
                }
            }
            if (loginScr != IntPtr.Zero)
            {
                _mono.CallMethod(GameSchema.LoginScr.ClassName, GameSchema.Screen.MethodSwitchToMe, loginScr);
            }
        }
        catch { }
    }

    private void PerformScreenAction(string screenClassName, int actionId)
    {
        try
        {
            IntPtr scr = _mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticCurrentScreen);
            string actualClass = scr != IntPtr.Zero ? (_mono.GetClassName(scr) ?? screenClassName) : screenClassName;
            _mono.CallMethod(actualClass, GameSchema.Screen.MethodPerform, scr, actionId, null);
        }
        catch { }
    }

    private int ResolveTargetServerId()
    {
        if (!string.IsNullOrWhiteSpace(Options.ServerName))
        {
            try
            {
                IntPtr serverNamesPtr = _mono.ReadStatic<IntPtr>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticNameServer);
                if (serverNamesPtr != IntPtr.Zero)
                {
                    string[] names = _mono.ReadMonoStringArray(serverNamesPtr);
                    if (names != null && names.Length > 0)
                    {
                        for (int i = 0; i < names.Length; i++)
                        {
                            if (string.Equals(names[i], Options.ServerName, StringComparison.OrdinalIgnoreCase))
                            {
                                return i;
                            }
                        }
                    }
                }
            }
            catch { }
        }
        return Options.ServerId;
    }
}
