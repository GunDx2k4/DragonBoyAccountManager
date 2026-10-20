using DragonBoyAccountManager.Core.Game.Schema;
using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game.Modules;

public interface IUIModule
{
    IntPtr CanvasPointer { get; }
    IntPtr CurrentScreenPointer { get; }
    string? CurrentScreenName { get; }
    bool IsGamePlayScreen { get; }
    bool IsDownloadingData { get; }
    int DownloadDataPercent { get; }
    bool TriggerDownloadData();

    IntPtr CurrentDialogPointer { get; }
    bool HasDialog { get; }
    bool IsWaitDialog { get; }
    string? DialogText { get; }
    void CloseDialog();

    IntPtr PanelPointer { get; }
    bool IsPanelOpen { get; }
    int PanelType { get; }
    bool IsZonePanelOpen { get; }
    void CloseZonePanel();
    void ClosePanel();

    int SelectedServerId { get; set; }
    void SelectServer(int serverId);
    void OpenLoginScreen();
    void LoginWithSavedCredentials();
    void SubmitLogin();
    void CancelLogin();
    void PerformScreenAction(string screenClassName, int actionId);
    bool SetLoginCredentials(string username, string password);

    void SwitchToLoginScreen();
    void SwitchToGameScreen();

    void KeyPressed(int keyCode);
    void KeyReleased(int keyCode);
}

public class UIModule(MonoReflector reflector) : IUIModule
{
    private readonly MonoReflector _reflector = reflector;

    public IntPtr CanvasPointer
    {
        get
        {
            IntPtr ptr = _reflector.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticInstance);
            if (ptr == IntPtr.Zero)
                ptr = _reflector.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticGameCanvas);
            return ptr;
        }
    }

    public IntPtr CurrentScreenPointer => _reflector.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticCurrentScreen);

    public string? CurrentScreenName
    {
        get
        {
            IntPtr scr = CurrentScreenPointer;
            return scr != IntPtr.Zero ? _reflector.GetClassName(scr) : null;
        }
    }

    public bool IsGamePlayScreen => CurrentScreenName == GameSchema.GameScr.ClassName;

    public bool IsDownloadingData
    {
        get
        {
            try
            {
                bool isControllerLoading = _reflector.ReadStatic<bool>(GameSchema.Controller.ClassName, GameSchema.Controller.StaticIsLoadingData);
                if (isControllerLoading) return true;

                string? curScr = CurrentScreenName;
                if (curScr == GameSchema.SplashScr.ClassName)
                {
                    int nData = _reflector.ReadStatic<int>(GameSchema.SplashScr.ClassName, GameSchema.SplashScr.StaticNData);
                    if (nData != -1) return true;
                }

                if (curScr is GameSchema.ServerListScreen.ClassName or GameSchema.ServerListScreen.ClassNameAlt)
                {
                    bool bigOk = _reflector.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticBigOk);
                    if (bigOk) return false;

                    bool loadScreen = _reflector.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticLoadScreen);
                    if (loadScreen) return false;

                    return true;



                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }

    public int DownloadDataPercent
    {
        get
        {
            try
            {
                string? curScr = CurrentScreenName;
                if (curScr == GameSchema.SplashScr.ClassName)
                {
                    int nData = _reflector.ReadStatic<int>(GameSchema.SplashScr.ClassName, GameSchema.SplashScr.StaticNData);
                    int maxData = _reflector.ReadStatic<int>(GameSchema.SplashScr.ClassName, GameSchema.SplashScr.StaticMaxData);
                    if (nData != -1 && maxData > 0)
                    {
                        return Math.Clamp(nData * 100 / maxData, 0, 100);
                    }
                }

                if (curScr is GameSchema.ServerListScreen.ClassName or GameSchema.ServerListScreen.ClassNameAlt)
                {
                    return _reflector.ReadStatic<int>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticPercent);
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    public bool TriggerDownloadData()
    {
        try
        {
            string? curScr = CurrentScreenName;
            if (curScr is GameSchema.ServerListScreen.ClassName or GameSchema.ServerListScreen.ClassNameAlt)
            {
                bool bigOk = _reflector.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticBigOk);
                bool loadScreen = _reflector.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticLoadScreen);
                if (!bigOk && !loadScreen)
                {
                    bool isGetData = _reflector.ReadStatic<bool>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticIsGetData);
                    if (!isGetData)
                    {
                        IntPtr cmdDownloadPtr = _reflector.ReadStatic<IntPtr>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticCmdDownload);
                        if (cmdDownloadPtr != IntPtr.Zero)
                        {
                            _reflector.CallMethod(GameSchema.Command.ClassName, GameSchema.Command.MethodPerformAction, cmdDownloadPtr);
                            return true;
                        }
                    }
                }
            }
        }
        catch { }
        return false;
    }

    public IntPtr CurrentDialogPointer => _reflector.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticCurrentDialog);

    public bool HasDialog => CurrentDialogPointer != IntPtr.Zero;

    public bool IsWaitDialog
    {
        get
        {
            IntPtr dlg = CurrentDialogPointer;
            if (dlg == IntPtr.Zero) return false;
            try
            {
                return _reflector.ReadField<bool>(GameSchema.MsgDlg.ClassName, GameSchema.MsgDlg.FieldIsWait, dlg);
            }
            catch
            {
                return false;
            }
        }
    }

    public string? DialogText
    {
        get
        {
            IntPtr dlg = CurrentDialogPointer;
            if (dlg == IntPtr.Zero) return null;
            try
            {
                IntPtr infoPtr = _reflector.ReadFieldPtr(GameSchema.MsgDlg.ClassName, GameSchema.MsgDlg.FieldInfo, dlg);
                return infoPtr != IntPtr.Zero ? _reflector.ReadMonoString(infoPtr) : null;
            }
            catch
            {
                return null;
            }
        }
    }

    public void CloseDialog()
    {
        try
        {
            _reflector.CallMethod(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.MethodEndDlg, IntPtr.Zero);
        }
        catch { }
    }

    public IntPtr PanelPointer
    {
        get
        {
            try
            {
                return _reflector.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticPanel);
            }
            catch
            {
                return IntPtr.Zero;
            }
        }
    }

    public bool IsPanelOpen
    {
        get
        {
            IntPtr panel = PanelPointer;
            if (panel == IntPtr.Zero) return false;
            try
            {
                return _reflector.ReadField<bool>(GameSchema.Panel.ClassName, GameSchema.Panel.FieldIsShow, panel);
            }
            catch
            {
                return false;
            }
        }
    }

    public int PanelType
    {
        get
        {
            IntPtr panel = PanelPointer;
            if (panel == IntPtr.Zero) return -1;
            try
            {
                return _reflector.ReadField<int>(GameSchema.Panel.ClassName, GameSchema.Panel.FieldType, panel);
            }
            catch
            {
                return -1;
            }
        }
    }

    public bool IsZonePanelOpen
    {
        get
        {
            try
            {
                return IsPanelOpen && PanelType == GameSchema.Panel.TypeZone;
            }
            catch
            {
                return false;
            }
        }
    }

    public void CloseZonePanel()
    {
        IntPtr panel = PanelPointer;
        if (panel == IntPtr.Zero) return;

        try
        {
            if (IsZonePanelOpen)
            {
                _reflector.WriteField(GameSchema.Panel.ClassName, GameSchema.Panel.FieldIsShow, panel, false);
                _reflector.WriteField(GameSchema.Panel.ClassName, GameSchema.Panel.FieldTimeShow, panel, 0);
                _reflector.WriteField(GameSchema.Panel.ClassName, GameSchema.Panel.FieldIsClose, panel, false);
            }
        }
        catch { }
    }

    public void ClosePanel()
    {
        IntPtr panel = PanelPointer;
        if (panel == IntPtr.Zero) return;

        try
        {
            _reflector.WriteField(GameSchema.Panel.ClassName, GameSchema.Panel.FieldIsShow, panel, false);
            _reflector.WriteField(GameSchema.Panel.ClassName, GameSchema.Panel.FieldTimeShow, panel, 0);
            _reflector.WriteField(GameSchema.Panel.ClassName, GameSchema.Panel.FieldIsClose, panel, false);
        }
        catch { }
    }

    public int SelectedServerId
    {
        get
        {
            try { return _reflector.ReadStatic<int>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticIpSelect); }
            catch { return -1; }
        }
        set
        {
            try { _reflector.WriteStatic(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticIpSelect, value); }
            catch { }
        }
    }

    public void SelectServer(int serverId)
    {
        try
        {
            _reflector.CallMethod(
                GameSchema.ServerListScreen.ClassName,
                GameSchema.ServerListScreen.MethodSetIpSelect,
                IntPtr.Zero,
                serverId,
                true);

            IntPtr serverScreen = _reflector.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticServerScreen);
            if (serverScreen == IntPtr.Zero)
            {
                serverScreen = CurrentScreenPointer;
            }
            if (serverScreen != IntPtr.Zero)
            {
                _reflector.CallMethod(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.MethodSelectServer, serverScreen);
            }
        }
        catch
        {
            SelectedServerId = serverId;
        }
    }

    public void OpenLoginScreen()
    {
        try
        {
            IntPtr serverScreen = _reflector.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticServerScreen);
            if (serverScreen != IntPtr.Zero)
            {
                _reflector.CallMethod(GameSchema.ServerListScreen.ClassName, GameSchema.Screen.MethodPerform, serverScreen, GameSchema.ServerListScreen.ActionOpenLogin, null);
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
            IntPtr serverScreen = _reflector.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticServerScreen);
            if (serverScreen != IntPtr.Zero)
            {
                _reflector.CallMethod(GameSchema.ServerListScreen.ClassName, GameSchema.Screen.MethodPerform, serverScreen, GameSchema.ServerListScreen.ActionLoginSaved, null);
                return;
            }
        }
        catch { }
        PerformScreenAction(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.ActionLoginSaved);
    }

    public void SubmitLogin()
    {
        IntPtr loginScr = LoginScreenPointer;
        if (loginScr != IntPtr.Zero)
        {
            try
            {
                _reflector.CallMethod(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.MethodDoLogin, loginScr);
                return;
            }
            catch { }
        }
        PerformScreenAction(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.ActionSubmit);
    }

    public void CancelLogin() => PerformScreenAction(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.ActionCancel);

    public void PerformScreenAction(string screenClassName, int actionId)
    {
        try
        {
            IntPtr scr = CurrentScreenPointer;
            string actualClass = scr != IntPtr.Zero ? (_reflector.GetClassName(scr) ?? screenClassName) : screenClassName;
            _reflector.CallMethod(actualClass, GameSchema.Screen.MethodPerform, scr, actionId, null);
        }
        catch { }
    }

    public bool SetLoginCredentials(string username, string password)
    {
        username ??= string.Empty;
        password ??= string.Empty;

        IntPtr loginScr = LoginScreenPointer;
        if (loginScr == IntPtr.Zero) return false;

        IntPtr tfUser = _reflector.ReadFieldPtr(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldTfUser, loginScr);
        IntPtr tfPass = _reflector.ReadFieldPtr(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldTfPass, loginScr);
        if (tfUser == IntPtr.Zero || tfPass == IntPtr.Zero) return false;

        SetTFieldText(tfUser, username);
        SetTFieldText(tfPass, password);

        try
        {
            _reflector.CallMethod(GameSchema.Rms.ClassName, GameSchema.Rms.MethodSaveRMSString, IntPtr.Zero, GameSchema.Rms.KeyAcc, username.Trim());
            _reflector.CallMethod(GameSchema.Rms.ClassName, GameSchema.Rms.MethodSaveRMSString, IntPtr.Zero, GameSchema.Rms.KeyPass, password.Trim());
        }
        catch { }

        string curUser = GetTFieldText(tfUser) ?? string.Empty;
        string curPass = GetTFieldText(tfPass) ?? string.Empty;

        return string.Equals(curUser, username, StringComparison.Ordinal)
            && string.Equals(curPass, password, StringComparison.Ordinal);
    }

    private IntPtr LoginScreenPointer
    {
        get
        {
            try
            {
                IntPtr cur = CurrentScreenPointer;
                if (cur != IntPtr.Zero && _reflector.GetClassName(cur) == GameSchema.LoginScr.ClassName)
                    return cur;

                return _reflector.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticLoginScr);
            }
            catch { return IntPtr.Zero; }
        }
    }

    private void SetTFieldText(IntPtr tfieldPtr, string text)
    {
        if (tfieldPtr == IntPtr.Zero) return;
        text ??= string.Empty;

        try { _reflector.CallMethod(GameSchema.TField.ClassName, GameSchema.TField.MethodSetText, tfieldPtr, text); }
        catch { }

        string cur = GetTFieldText(tfieldPtr) ?? string.Empty;
        if (string.Equals(cur, text, StringComparison.Ordinal)) return;

        IntPtr monoStr = _reflector.CreateMonoString(text);
        if (monoStr != IntPtr.Zero)
        {
            _reflector.WriteField(GameSchema.TField.ClassName, GameSchema.TField.FieldText, tfieldPtr, monoStr);
            _reflector.WriteField(GameSchema.TField.ClassName, GameSchema.TField.FieldPaintedText, tfieldPtr, monoStr);
            _reflector.WriteField(GameSchema.TField.ClassName, GameSchema.TField.FieldCaretPos, tfieldPtr, text.Length);

            string stars = new string('*', text.Length);
            IntPtr starStr = _reflector.CreateMonoString(stars);
            if (starStr != IntPtr.Zero)
            {
                _reflector.WriteField(GameSchema.TField.ClassName, GameSchema.TField.FieldPasswordText, tfieldPtr, starStr);
            }
        }
    }

    private string? GetTFieldText(IntPtr tfieldPtr)
    {
        if (tfieldPtr == IntPtr.Zero) return null;
        try
        {
            IntPtr res = _reflector.CallMethod(GameSchema.TField.ClassName, GameSchema.TField.MethodGetText, tfieldPtr);
            if (res != IntPtr.Zero)
            {
                string text = _reflector.ReadMonoString(res);
                if (!string.IsNullOrEmpty(text))
                    return text;
            }
        }
        catch { }

        try { return _reflector.ReadStringField(GameSchema.TField.ClassName, GameSchema.TField.FieldText, tfieldPtr); }
        catch { return null; }
    }

    public void SwitchToLoginScreen()
    {
        try
        {
            IntPtr loginScr = _reflector.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticLoginScr);
            if (loginScr == IntPtr.Zero)
            {
                loginScr = _reflector.CreateInstance(GameSchema.LoginScr.ClassName);
                if (loginScr != IntPtr.Zero)
                {
                    _reflector.WriteStatic(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticLoginScr, loginScr);
                }
            }
            if (loginScr != IntPtr.Zero)
            {
                _reflector.CallMethod(GameSchema.LoginScr.ClassName, GameSchema.Screen.MethodSwitchToMe, loginScr);
            }
        }
        catch { }
    }

    public void SwitchToGameScreen()
    {
        try
        {
            IntPtr gameScr = _reflector.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticInstance);
            if (gameScr == IntPtr.Zero)
            {
                gameScr = _reflector.CallMethod(GameSchema.GameScr.ClassName, GameSchema.GameScr.MethodGI, IntPtr.Zero);
            }
            if (gameScr != IntPtr.Zero)
            {
                _reflector.CallMethod(GameSchema.GameScr.ClassName, GameSchema.Screen.MethodSwitchToMe, gameScr);
            }
            CloseDialog();
        }
        catch { }
    }

    public void KeyPressed(int keyCode)
    {
        IntPtr canvas = CanvasPointer;
        if (canvas != IntPtr.Zero)
            _reflector.CallMethod(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.MethodKeyPressedz, canvas, keyCode);
    }

    public void KeyReleased(int keyCode)
    {
        IntPtr canvas = CanvasPointer;
        if (canvas != IntPtr.Zero)
            _reflector.CallMethod(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.MethodKeyReleasedz, canvas, keyCode);
    }
}
