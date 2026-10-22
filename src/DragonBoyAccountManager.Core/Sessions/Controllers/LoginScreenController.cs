using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Interop.Mono;
using DragonBoyAccountManager.Core.Interop.Mono.Schema;

namespace DragonBoyAccountManager.Core.Sessions.Controllers;

public sealed class LoginScreenController(IMonoRuntime mono)
{
    private readonly IMonoRuntime _mono = mono ?? throw new ArgumentNullException(nameof(mono));

    public string? GetCurrentScreenName()
    {
        try
        {
            IntPtr scr = _mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticCurrentScreen);
            return scr != IntPtr.Zero ? _mono.GetClassName(scr) : null;
        }
        catch { return null; }
    }

    public bool HasActiveDialog(out bool isWaitDialog, out string? dialogText)
    {
        isWaitDialog = false;
        dialogText = null;
        try
        {
            IntPtr dlg = _mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticCurrentDialog);
            if (dlg == IntPtr.Zero) return false;

            try { isWaitDialog = _mono.ReadField<bool>(GameSchema.MsgDlg.ClassName, GameSchema.MsgDlg.FieldIsWait, dlg); } catch { }
            try { dialogText = _mono.ReadStringField(GameSchema.MsgDlg.ClassName, GameSchema.MsgDlg.FieldInfo, dlg); } catch { }
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

    public bool IsLoadingControllerData()
    {
        try { return _mono.ReadStatic<bool>(GameSchema.Controller.ClassName, GameSchema.Controller.StaticIsLoadingData); }
        catch { return false; }
    }

    public bool IsWaitingToLogin()
    {
        try { return _mono.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticWaitToLogin); }
        catch { return false; }
    }

    public bool IsServerListReady()
    {
        bool loadScreen = false;
        try { loadScreen = _mono.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticLoadScreen); } catch { }
        bool bigOk = false;
        try { bigOk = _mono.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticBigOk); } catch { }
        int percent = 0;
        try { percent = _mono.ReadStatic<int>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticPercent); } catch { }

        return loadScreen || bigOk || percent >= 100;
    }

    public bool IsUpdatingGameData()
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

    public bool IsDownloadingData()
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

    public bool TriggerDownloadData()
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
        try { return _mono.ReadStatic<int>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticIpSelect); }
        catch { return -1; }
    }

    public void SelectServer(int serverId)
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

    public void OpenLoginScreen(bool isMasked, string username)
    {
        PreMaskLoginFields(isMasked, username);
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

    public void LoginWithSavedCredentials()
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

    public void CancelLogin() => PerformScreenAction(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.ActionCancel);

    public void SubmitLogin(bool isMasked, string username, string password)
    {
        try
        {
            if (isMasked)
            {
                IntPtr serviceInst = _mono.ReadStatic<IntPtr>(GameSchema.Service.ClassName, GameSchema.Service.StaticInstance);
                if (serviceInst == IntPtr.Zero)
                {
                    serviceInst = _mono.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodGI, IntPtr.Zero);
                }

                if (serviceInst != IntPtr.Zero)
                {
                    bool connected = false;
                    try { connected = _mono.ReadStatic<bool>(GameSchema.Session.ClassName, GameSchema.Session.StaticConnected); } catch { }
                    if (!connected)
                    {
                        try { _mono.CallMethod(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.MethodConnect, IntPtr.Zero); } catch { }
                    }

                    string version = _mono.ReadStaticString(GameSchema.GameMidlet.ClassName, GameSchema.GameMidlet.StaticVersion) ?? "2.5.0";
                    _mono.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodLogin, serviceInst, username.Trim(), password.Trim(), version, (sbyte)0);
                    try { _mono.CallMethod(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.MethodStartWaitDlg, IntPtr.Zero); } catch { }
                    return;
                }
            }

            IntPtr loginScr = GetLoginScreenPointer();
            if (loginScr != IntPtr.Zero)
            {
                _mono.CallMethod(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.MethodDoLogin, loginScr);
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

        IntPtr tfUser = _mono.ReadFieldPtr(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldTfUser, loginScr);
        IntPtr tfPass = _mono.ReadFieldPtr(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldTfPass, loginScr);
        if (tfUser == IntPtr.Zero || tfPass == IntPtr.Zero) return false;

        if (isMasked)
        {
            try
            {
                _mono.WriteField(GameSchema.TField.ClassName, GameSchema.TField.FieldInputType, tfUser, GameSchema.TField.InputTypePassword);
                _mono.WriteField(GameSchema.TField.ClassName, GameSchema.TField.FieldInputType, tfPass, GameSchema.TField.InputTypePassword);
            }
            catch { }
        }

        SetTFieldText(tfUser, username);
        SetTFieldText(tfPass, password);

        if (!isMasked)
        {
            try
            {
                _mono.CallMethod(GameSchema.Rms.ClassName, GameSchema.Rms.MethodSaveRMSString, IntPtr.Zero, GameSchema.Rms.KeyAcc, username.Trim());
                _mono.CallMethod(GameSchema.Rms.ClassName, GameSchema.Rms.MethodSaveRMSString, IntPtr.Zero, GameSchema.Rms.KeyPass, password.Trim());
            }
            catch { }
        }

        string curUser = GetTFieldText(tfUser) ?? string.Empty;
        string curPass = GetTFieldText(tfPass) ?? string.Empty;

        return string.Equals(curUser.Trim(), username.Trim(), StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrEmpty(curUser) && !string.IsNullOrEmpty(curPass));
    }

    public void PreMaskServerListScreen(bool isMasked)
    {
        if (!isMasked) return;
        try
        {
            string curRmsAcc = _mono.ReadStaticString(GameSchema.Rms.ClassName, GameSchema.Rms.StaticRMS_acc);
            if (curRmsAcc != "DBAM")
            {
                _mono.WriteStaticString(GameSchema.Rms.ClassName, GameSchema.Rms.StaticRMS_acc, "DBAM");
            }

            IntPtr serverScreen = _mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticServerScreen);
            if (serverScreen != IntPtr.Zero)
            {
                IntPtr cmdArr = _mono.ReadFieldPtr(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.FieldCmd, serverScreen);
                if (cmdArr != IntPtr.Zero)
                {
                    IntPtr[] cmds = _mono.ReadMonoPointerArray(cmdArr);
                    if (cmds.Length > 0 && cmds[0] != IntPtr.Zero)
                    {
                        string caption = _mono.ReadStringField(GameSchema.Command.ClassName, GameSchema.Command.FieldCaption, cmds[0]) ?? string.Empty;
                        if (caption != "DragonBoy Account Manager")
                        {
                            _mono.WriteStringField(GameSchema.Command.ClassName, GameSchema.Command.FieldCaption, cmds[0], "DragonBoy Account Manager");
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
                IntPtr tfUser = _mono.ReadFieldPtr(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldTfUser, loginScr);
                IntPtr tfPass = _mono.ReadFieldPtr(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldTfPass, loginScr);
                if (tfUser != IntPtr.Zero)
                {
                    _mono.WriteField(GameSchema.TField.ClassName, GameSchema.TField.FieldInputType, tfUser, GameSchema.TField.InputTypePassword);
                    string curText = GetTFieldText(tfUser) ?? string.Empty;
                    if (!string.IsNullOrEmpty(curText) && !string.Equals(curText, username, StringComparison.Ordinal) && !curText.Contains('*'))
                    {
                        string masked = TextMaskHelper.MaskText(curText);
                        SetTFieldText(tfUser, masked);
                    }
                }
                if (tfPass != IntPtr.Zero)
                {
                    _mono.WriteField(GameSchema.TField.ClassName, GameSchema.TField.FieldInputType, tfPass, GameSchema.TField.InputTypePassword);
                    SetTFieldText(tfPass, string.Empty);
                }
            }
        }
        catch { }
    }

    public void ClearSensitiveInputs(string username)
    {
        try
        {
            IntPtr loginScr = GetLoginScreenPointer();
            if (loginScr != IntPtr.Zero)
            {
                IntPtr tfUser = _mono.ReadFieldPtr(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldTfUser, loginScr);
                IntPtr tfPass = _mono.ReadFieldPtr(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldTfPass, loginScr);
                if (tfUser != IntPtr.Zero)
                {
                    string masked = TextMaskHelper.MaskText(username);
                    SetTFieldText(tfUser, masked);
                    _mono.WriteField(GameSchema.TField.ClassName, GameSchema.TField.FieldInputType, tfUser, GameSchema.TField.InputTypePassword);
                }
                if (tfPass != IntPtr.Zero) SetTFieldText(tfPass, string.Empty);
            }

            string curRmsAcc = _mono.ReadStaticString(GameSchema.Rms.ClassName, GameSchema.Rms.StaticRMS_acc);
            if (curRmsAcc != "DBAM")
            {
                _mono.WriteStaticString(GameSchema.Rms.ClassName, GameSchema.Rms.StaticRMS_acc, "DBAM");
            }
        }
        catch { }
    }

    public void SwitchToLoginScreen(bool isMasked, string username)
    {
        PreMaskLoginFields(isMasked, username);
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

    public void PerformScreenAction(string screenClassName, int actionId)
    {
        try
        {
            IntPtr scr = _mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticCurrentScreen);
            string actualClass = scr != IntPtr.Zero ? (_mono.GetClassName(scr) ?? screenClassName) : screenClassName;
            _mono.CallMethod(actualClass, GameSchema.Screen.MethodPerform, scr, actionId, null);
        }
        catch { }
    }

    public int ResolveTargetServerId(int fallbackServerId, string? serverName)
    {
        if (!string.IsNullOrWhiteSpace(serverName))
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
            IntPtr cur = _mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticCurrentScreen);
            if (cur != IntPtr.Zero && _mono.GetClassName(cur) == GameSchema.LoginScr.ClassName)
                return cur;

            return _mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticLoginScr);
        }
        catch { return IntPtr.Zero; }
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

            string stars = new('*', text.Length);
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
}
