using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;

namespace DragonBoyAccountManager.Core.Sessions.Controllers;

public sealed class LoginScreenController(IGameRuntime runtime)
{
    private readonly IGameRuntime _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

    public string? GetCurrentScreenName()
    {
        try
        {
            IntPtr scr = _runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, _runtime.Layout.GameCanvas.CurrentScreen);
            return scr != IntPtr.Zero ? _runtime.GetClassName(scr) : null;
        }
        catch { return null; }
    }

    public bool HasActiveDialog(out bool isWaitDialog, out string? dialogText)
    {
        isWaitDialog = false;
        dialogText = null;
        try
        {
            IntPtr dlg = _runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, _runtime.Layout.GameCanvas.CurrentDialog);
            if (dlg == IntPtr.Zero) return false;

            try { isWaitDialog = _runtime.ReadField<bool>(dlg, _runtime.Layout.MsgDlg.IsWait); } catch { }
            try { dialogText = _runtime.ReadStringField(dlg, _runtime.Layout.MsgDlg.Info); } catch { }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool IsNonWaitDialogPresent()
    {
        return HasActiveDialog(out bool isWait, out _) && !isWait;
    }

    public bool IsWaitDialogPresent()
    {
        return HasActiveDialog(out bool isWait, out _) && isWait;
    }

    public void CloseDialog()
    {
        try
        {
            IntPtr canvas = _runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, _runtime.Layout.GameCanvas.Instance);
            if (canvas == IntPtr.Zero)
                canvas = _runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, _runtime.Layout.GameCanvas.GameCanvas);
            if (canvas != IntPtr.Zero)
            {
                _runtime.CallMethod(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.MethodEndDlg, canvas);
            }
        }
        catch { }
    }

    public bool IsLoadingControllerData()
    {
        try { return _runtime.ReadStatic<bool>(GameSchema.Controller.ClassName, _runtime.Layout.Controller.IsLoadingData); }
        catch { return false; }
    }

    public bool IsWaitingToLogin()
    {
        try { return _runtime.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, _runtime.Layout.ServerListScreen.WaitToLogin); }
        catch { return false; }
    }

    public bool IsServerListReady()
    {
        bool loadScreen = false;
        try { loadScreen = _runtime.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, _runtime.Layout.ServerListScreen.LoadScreen); } catch { }
        bool bigOk = false;
        try { bigOk = _runtime.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, _runtime.Layout.ServerListScreen.BigOk); } catch { }
        int percent = 0;
        try { percent = _runtime.ReadStatic<int>(GameSchema.ServerListScreen.ClassName, _runtime.Layout.ServerListScreen.Percent); } catch { }

        return loadScreen || bigOk || percent >= 100;
    }

    public bool IsUpdatingGameData()
    {
        try
        {
            var loginScrLayout = _runtime.Layout.LoginScr;
            if (_runtime.ReadStatic<bool>(GameSchema.LoginScr.ClassName, loginScrLayout.IsUpdateAll)) return true;
            if (_runtime.ReadStatic<bool>(GameSchema.LoginScr.ClassName, loginScrLayout.IsUpdateData)) return true;
            if (_runtime.ReadStatic<bool>(GameSchema.LoginScr.ClassName, loginScrLayout.IsUpdateMap)) return true;
            if (_runtime.ReadStatic<bool>(GameSchema.LoginScr.ClassName, loginScrLayout.IsUpdateSkill)) return true;
            if (_runtime.ReadStatic<bool>(GameSchema.LoginScr.ClassName, loginScrLayout.IsUpdateItem)) return true;
            return false;
        }
        catch { return false; }
    }

    public bool IsDownloadingData()
    {
        try
        {
            if (_runtime.ReadStatic<bool>(GameSchema.Controller.ClassName, _runtime.Layout.Controller.IsLoadingData))
                return true;

            if (IsUpdatingGameData())
                return true;

            string? curScr = GetCurrentScreenName();
            if (curScr == GameSchema.SplashScr.ClassName)
            {
                int nData = _runtime.ReadStatic<int>(GameSchema.SplashScr.ClassName, _runtime.Layout.SplashScr.NData);
                if (nData != -1) return true;
            }

            if (curScr == GameSchema.ServerListScreen.ClassName)
            {
                bool isGetData = false;
                try { isGetData = _runtime.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, _runtime.Layout.ServerListScreen.IsGetData); } catch { }
                if (isGetData) return true;

                if (!IsServerListReady())
                    return true;
            }

            return false;
        }
        catch { return false; }
    }

    public int GetDownloadDataPercent()
    {
        try
        {
            string? curScr = GetCurrentScreenName();
            if (curScr == GameSchema.SplashScr.ClassName)
            {
                int nData = _runtime.ReadStatic<int>(GameSchema.SplashScr.ClassName, _runtime.Layout.SplashScr.NData);
                int maxData = _runtime.ReadStatic<int>(GameSchema.SplashScr.ClassName, _runtime.Layout.SplashScr.MaxData);
                if (nData != -1 && maxData > 0)
                {
                    return Math.Clamp(nData * 100 / maxData, 0, 100);
                }
            }

            if (curScr == GameSchema.ServerListScreen.ClassName)
            {
                return _runtime.ReadStatic<int>(GameSchema.ServerListScreen.ClassName, _runtime.Layout.ServerListScreen.Percent);
            }

            return 0;
        }
        catch { return 0; }
    }

    public bool TriggerDownloadData()
    {
        try
        {
            string? curScr = GetCurrentScreenName();
            if (curScr == GameSchema.ServerListScreen.ClassName)
            {
                bool isGetData = false;
                try { isGetData = _runtime.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, _runtime.Layout.ServerListScreen.IsGetData); } catch { }
                if (isGetData)
                {
                    return false;
                }

                if (!IsServerListReady())
                {
                    PerformScreenAction(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.ActionDownload);
                    return true;
                }
            }
            return false;
        }
        catch { return false; }
    }

    public int GetSelectedServerId()
    {
        try { return _runtime.ReadStatic<int>(GameSchema.ServerListScreen.ClassName, _runtime.Layout.ServerListScreen.IpSelect); }
        catch { return -1; }
    }

    public void SelectServer(int serverId)
    {
        try
        {
            IntPtr serverScreen = _runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, _runtime.Layout.GameCanvas.ServerScreen);
            if (serverScreen != IntPtr.Zero)
            {
                _runtime.WriteStatic(GameSchema.ServerListScreen.ClassName, _runtime.Layout.ServerListScreen.IpSelect, serverId);
                _runtime.CallMethod(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.MethodSelectServer, serverScreen);
                return;
            }
            _runtime.CallMethod(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.MethodSetIpSelect, IntPtr.Zero, serverId, false);
            _runtime.WriteStatic(GameSchema.ServerListScreen.ClassName, _runtime.Layout.ServerListScreen.IpSelect, serverId);
        }
        catch { }
    }

    public void OpenLoginScreen(bool isMasked, string username)
    {
        PreMaskLoginFields(isMasked, username);
        try
        {
            IntPtr serverScreen = _runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, _runtime.Layout.GameCanvas.ServerScreen);
            if (serverScreen != IntPtr.Zero)
            {
                _runtime.CallMethod(GameSchema.ServerListScreen.ClassName, GameSchema.Screen.MethodPerform, serverScreen, GameSchema.ServerListScreen.ActionOpenLogin, null);
                return;
            }
        }
        catch { }
        PerformScreenAction(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.ActionOpenLogin);
    }

    public void LoginWithSavedCredentials()
    {
        try
        {
            IntPtr serverScreen = _runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, _runtime.Layout.GameCanvas.ServerScreen);
            if (serverScreen != IntPtr.Zero)
            {
                _runtime.CallMethod(GameSchema.ServerListScreen.ClassName, GameSchema.Screen.MethodPerform, serverScreen, GameSchema.ServerListScreen.ActionLoginSaved, null);
                return;
            }
        }
        catch { }
        PerformScreenAction(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.ActionLoginSaved);
    }

    public void CancelLogin() => PerformScreenAction(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.ActionCancel);

    public void SubmitLogin(bool isMasked, string username, string password)
    {
        username = username?.Trim() ?? string.Empty;
        password = password?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            PerformScreenAction(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.ActionSubmit);
            return;
        }

        try
        {
            IntPtr loginScr = GetLoginScreenPointer();
            if (loginScr != IntPtr.Zero)
            {
                SetLoginCredentials(username, password, isMasked);
            }

            IntPtr serviceInst = _runtime.ReadStatic<IntPtr>(GameSchema.Service.ClassName, _runtime.Layout.Service.Instance);
            if (serviceInst == IntPtr.Zero)
            {
                serviceInst = _runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodGI, IntPtr.Zero);
            }

            if (serviceInst != IntPtr.Zero)
            {
                bool connected = false;
                try { connected = _runtime.ReadStatic<bool>(GameSchema.Session.ClassName, _runtime.Layout.Session.Connected); } catch { }
                if (!connected)
                {
                    try { _runtime.CallMethod(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.MethodConnect, IntPtr.Zero); } catch { }
                }

                string? version = _runtime.ReadStaticString(GameSchema.GameMidlet.ClassName, _runtime.Layout.GameMidlet.Version);
                if (string.IsNullOrWhiteSpace(version))
                {
                    version = Constants.Client.DefaultVersion;
                }

                _runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodLogin, serviceInst, username, password, version, (sbyte)0);
                return;
            }
        }
        catch { }

        PerformScreenAction(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.ActionSubmit);
    }

    public bool SetLoginCredentials(string username, string password, bool isMasked)
    {
        username ??= string.Empty;
        password ??= string.Empty;

        IntPtr loginScr = GetLoginScreenPointer();
        if (loginScr == IntPtr.Zero) return false;

        var loginScrLayout = _runtime.Layout.LoginScr;
        try
        {
            if (loginScrLayout.IsCheck >= 0)
            {
                _runtime.WriteField(loginScr, loginScrLayout.IsCheck, false);
            }
            if (loginScrLayout.IsLogin2 >= 0)
            {
                _runtime.WriteField(loginScr, loginScrLayout.IsLogin2, false);
            }
        }
        catch { }

        IntPtr tfUser = _runtime.ReadFieldPtr(loginScr, loginScrLayout.TfUser);
        IntPtr tfPass = _runtime.ReadFieldPtr(loginScr, loginScrLayout.TfPass);
        if (tfUser == IntPtr.Zero || tfPass == IntPtr.Zero) return false;

        var tfieldLayout = _runtime.Layout.TField;
        if (isMasked)
        {
            try
            {
                _runtime.WriteField(tfUser, tfieldLayout.InputType, GameSchema.TField.InputTypePassword);
                _runtime.WriteField(tfPass, tfieldLayout.InputType, GameSchema.TField.InputTypePassword);
            }
            catch { }
        }

        SetTFieldText(tfUser, username);
        SetTFieldText(tfPass, password);

        string curUser = GetTFieldText(tfUser) ?? string.Empty;
        string curPass = GetTFieldText(tfPass) ?? string.Empty;

        return string.Equals(curUser.Trim(), username.Trim(), StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrEmpty(curUser) && !string.IsNullOrEmpty(curPass));
    }

    public void PreMaskServerListScreen(bool isMasked)
    {
        try
        {
            string curRmsAcc = _runtime.ReadStaticString(GameSchema.Rms.ClassName, _runtime.Layout.Rms.RMS_acc);
            if (curRmsAcc != "acc")
            {
                _runtime.WriteStaticString(GameSchema.Rms.ClassName, _runtime.Layout.Rms.RMS_acc, "acc");
            }

            if (!isMasked) return;

            IntPtr serverScreen = _runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, _runtime.Layout.GameCanvas.ServerScreen);
            if (serverScreen != IntPtr.Zero)
            {
                IntPtr cmdArr = _runtime.ReadFieldPtr(serverScreen, _runtime.Layout.ServerListScreen.Cmd);
                if (cmdArr != IntPtr.Zero)
                {
                    IntPtr[] cmds = _runtime.ReadPointerArray(cmdArr);
                    if (cmds.Length > 0 && cmds[0] != IntPtr.Zero)
                    {
                        string caption = _runtime.ReadStringField(cmds[0], _runtime.Layout.Command.Caption) ?? string.Empty;
                        if (caption != "DragonBoy Account Manager")
                        {
                            _runtime.WriteStringField(cmds[0], _runtime.Layout.Command.Caption, "DragonBoy Account Manager");
                        }
                    }
                }
            }
        }
        catch { }
    }

    public void PreMaskLoginFields(bool isMasked, string username)
    {
        if (!isMasked) return;
        try
        {
            IntPtr loginScr = GetLoginScreenPointer();
            if (loginScr != IntPtr.Zero)
            {
                var loginScrLayout = _runtime.Layout.LoginScr;
                var tfieldLayout = _runtime.Layout.TField;
                IntPtr tfUser = _runtime.ReadFieldPtr(loginScr, loginScrLayout.TfUser);
                IntPtr tfPass = _runtime.ReadFieldPtr(loginScr, loginScrLayout.TfPass);
                if (tfUser != IntPtr.Zero)
                {
                    _runtime.WriteField(tfUser, tfieldLayout.InputType, GameSchema.TField.InputTypePassword);
                }
                if (tfPass != IntPtr.Zero)
                {
                    _runtime.WriteField(tfPass, tfieldLayout.InputType, GameSchema.TField.InputTypePassword);
                }
            }
        }
        catch { }
    }

    public void ClearSensitiveInputs(string username)
    {
        try
        {
            string curRmsAcc = _runtime.ReadStaticString(GameSchema.Rms.ClassName, _runtime.Layout.Rms.RMS_acc);
            if (curRmsAcc != "acc")
            {
                _runtime.WriteStaticString(GameSchema.Rms.ClassName, _runtime.Layout.Rms.RMS_acc, "acc");
            }

            IntPtr loginScr = GetLoginScreenPointer();
            if (loginScr != IntPtr.Zero)
            {
                var loginScrLayout = _runtime.Layout.LoginScr;
                var tfieldLayout = _runtime.Layout.TField;
                IntPtr tfUser = _runtime.ReadFieldPtr(loginScr, loginScrLayout.TfUser);
                IntPtr tfPass = _runtime.ReadFieldPtr(loginScr, loginScrLayout.TfPass);
                if (tfUser != IntPtr.Zero)
                {
                    _runtime.WriteField(tfUser, tfieldLayout.InputType, GameSchema.TField.InputTypePassword);
                }
                if (tfPass != IntPtr.Zero)
                {
                    _runtime.WriteField(tfPass, tfieldLayout.InputType, GameSchema.TField.InputTypePassword);
                }
            }
        }
        catch { }
    }

    public void SwitchToLoginScreen(bool isMasked, string username)
    {
        PreMaskLoginFields(isMasked, username);
        try
        {
            IntPtr loginScr = _runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, _runtime.Layout.GameCanvas.LoginScr);
            if (loginScr == IntPtr.Zero)
            {
                loginScr = _runtime.CreateInstance(GameSchema.LoginScr.ClassName);
                if (loginScr != IntPtr.Zero)
                {
                    _runtime.WriteStatic(GameSchema.GameCanvas.ClassName, _runtime.Layout.GameCanvas.LoginScr, loginScr);
                }
            }
            if (loginScr != IntPtr.Zero)
            {
                _runtime.CallMethod(GameSchema.LoginScr.ClassName, GameSchema.Screen.MethodSwitchToMe, loginScr);
            }
        }
        catch { }
    }

    public void PerformScreenAction(string screenClassName, int actionId)
    {
        try
        {
            IntPtr scr = _runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, _runtime.Layout.GameCanvas.CurrentScreen);
            string actualClass = scr != IntPtr.Zero ? (_runtime.GetClassName(scr) ?? screenClassName) : screenClassName;
            _runtime.CallMethod(actualClass, GameSchema.Screen.MethodPerform, scr, actionId, null);
        }
        catch { }
    }

    public int ResolveTargetServerId(int fallbackServerId, string? serverName)
    {
        if (!string.IsNullOrWhiteSpace(serverName))
        {
            try
            {
                IntPtr serverNamesPtr = _runtime.ReadStatic<IntPtr>(GameSchema.ServerListScreen.ClassName, _runtime.Layout.ServerListScreen.NameServer);
                if (serverNamesPtr != IntPtr.Zero)
                {
                    string[] names = _runtime.ReadStringArray(serverNamesPtr);
                    if (names != null && names.Length > 0)
                    {
                        for (int i = 0; i < names.Length; i++)
                        {
                            if (string.Equals(names[i], serverName, StringComparison.OrdinalIgnoreCase))
                            {
                                return i;
                            }
                        }
                    }
                }
            }
            catch { }
        }
        return fallbackServerId;
    }

    private IntPtr GetLoginScreenPointer()
    {
        try
        {
            IntPtr cur = _runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, _runtime.Layout.GameCanvas.CurrentScreen);
            if (cur != IntPtr.Zero && _runtime.GetClassName(cur) == GameSchema.LoginScr.ClassName)
                return cur;

            return _runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, _runtime.Layout.GameCanvas.LoginScr);
        }
        catch { return IntPtr.Zero; }
    }

    private void SetTFieldText(IntPtr tfieldPtr, string text)
    {
        if (tfieldPtr == IntPtr.Zero) return;
        text ??= string.Empty;

        IntPtr strPtr = _runtime.CreateString(text);
        if (strPtr != IntPtr.Zero)
        {
            var tfieldLayout = _runtime.Layout.TField;
            _runtime.WriteField(tfieldPtr, tfieldLayout.Text, strPtr);
            _runtime.WriteField(tfieldPtr, tfieldLayout.PaintedText, strPtr);
            _runtime.WriteField(tfieldPtr, tfieldLayout.CaretPos, text.Length);

            string stars = new('*', text.Length);
            IntPtr starStr = _runtime.CreateString(stars);
            if (starStr != IntPtr.Zero)
            {
                _runtime.WriteField(tfieldPtr, tfieldLayout.PasswordText, starStr);
            }
        }
    }

    private string? GetTFieldText(IntPtr tfieldPtr)
    {
        if (tfieldPtr == IntPtr.Zero) return null;
        try { return _runtime.ReadStringField(tfieldPtr, _runtime.Layout.TField.Text); }
        catch { return null; }
    }
}
