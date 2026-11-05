using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;

namespace DragonBoyAccountManager.Core.Sessions.Controllers;

public sealed class SessionMaskingController(IGameRuntime runtime)
{
    private readonly IGameRuntime _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

    public bool IsMasked { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string? OriginalCharacterName { get; private set; }
    public string? OriginalServerListCaption { get; private set; }

    public bool HasOriginalServerListCaption => !string.IsNullOrEmpty(OriginalServerListCaption);

    public void Configure(bool isMasked, string username, bool isInGame)
    {
        IsMasked = isMasked;
        Username = username ?? string.Empty;
        OnTick(isInGame);
    }

    public void SetMasked(bool isMasked, bool isInGame)
    {
        IsMasked = isMasked;
        ApplyEarlyMaskingGuard(IsMasked, isInGame, Username);
        SyncInGameCharacterNameMasking(IsMasked, isInGame, Username);
    }

    public void OnTick(bool isInGame)
    {
        if (IsMasked || HasOriginalServerListCaption)
        {
            ApplyEarlyMaskingGuard(IsMasked, isInGame, Username);
        }

        if (isInGame)
        {
            SyncInGameCharacterNameMasking(IsMasked, true, Username);
        }
        else
        {
            ResetCharacterName();
        }
    }

    public void ResetCharacterName()
    {
        OriginalCharacterName = null;
    }

    public void ApplyEarlyMaskingGuard(bool isMasked, bool isInGame, string username)
    {
        try
        {
            var rmsLayout = _runtime.Layout.Rms;
            var gameCanvasLayout = _runtime.Layout.GameCanvas;
            var serverListLayout = _runtime.Layout.ServerListScreen;
            var commandLayout = _runtime.Layout.Command;
            var loginScrLayout = _runtime.Layout.LoginScr;
            var tfieldLayout = _runtime.Layout.TField;

            if (isMasked)
            {
                string curRmsAcc = _runtime.ReadStaticString(GameSchema.Rms.ClassName, rmsLayout.RMS_acc);
                if (curRmsAcc != "DBAM")
                {
                    _runtime.WriteStaticString(GameSchema.Rms.ClassName, rmsLayout.RMS_acc, "DBAM");
                }

                IntPtr serverScreen = _runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, gameCanvasLayout.ServerScreen);
                if (serverScreen != IntPtr.Zero)
                {
                    IntPtr cmdArr = _runtime.ReadFieldPtr(serverScreen, serverListLayout.Cmd);
                    if (cmdArr != IntPtr.Zero)
                    {
                        IntPtr[] cmds = _runtime.ReadPointerArray(cmdArr);
                        if (cmds.Length > 0 && cmds[0] != IntPtr.Zero)
                        {
                            string caption = _runtime.ReadStringField(cmds[0], commandLayout.Caption) ?? string.Empty;
                            if (caption != "DragonBoy Account Manager")
                            {
                                OriginalServerListCaption ??= caption;
                                _runtime.WriteStringField(cmds[0], commandLayout.Caption, "DragonBoy Account Manager");
                            }
                        }
                    }
                }

                IntPtr loginScr = _runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, gameCanvasLayout.LoginScr);
                if (loginScr != IntPtr.Zero)
                {
                    IntPtr tfUser = _runtime.ReadFieldPtr(loginScr, loginScrLayout.TfUser);
                    IntPtr tfPass = _runtime.ReadFieldPtr(loginScr, loginScrLayout.TfPass);

                    if (tfUser != IntPtr.Zero)
                    {
                        int inputType = _runtime.ReadField<int>(tfUser, tfieldLayout.InputType);
                        if (inputType != GameSchema.TField.InputTypePassword)
                        {
                            _runtime.WriteField(tfUser, tfieldLayout.InputType, GameSchema.TField.InputTypePassword);
                        }

                        if (!isInGame)
                        {
                            string curText = _runtime.ReadStringField(tfUser, tfieldLayout.Text) ?? string.Empty;
                            if (!string.IsNullOrEmpty(curText) && !string.Equals(curText, username, StringComparison.Ordinal) && !curText.Contains('*'))
                            {
                                string masked = TextMaskHelper.MaskText(curText);
                                _runtime.WriteStringField(tfUser, tfieldLayout.Text, masked);
                                _runtime.WriteStringField(tfUser, tfieldLayout.PaintedText, masked);
                            }
                        }
                    }

                    if (tfPass != IntPtr.Zero)
                    {
                        int passInputType = _runtime.ReadField<int>(tfPass, tfieldLayout.InputType);
                        if (passInputType != GameSchema.TField.InputTypePassword)
                        {
                            _runtime.WriteField(tfPass, tfieldLayout.InputType, GameSchema.TField.InputTypePassword);
                        }
                    }
                }
            }
            else
            {
                string curRmsAcc = _runtime.ReadStaticString(GameSchema.Rms.ClassName, rmsLayout.RMS_acc);
                if (curRmsAcc != "acc")
                {
                    _runtime.WriteStaticString(GameSchema.Rms.ClassName, rmsLayout.RMS_acc, "acc");
                }

                if (!string.IsNullOrEmpty(OriginalServerListCaption))
                {
                    IntPtr serverScreen = _runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, gameCanvasLayout.ServerScreen);
                    if (serverScreen != IntPtr.Zero)
                    {
                        IntPtr cmdArr = _runtime.ReadFieldPtr(serverScreen, serverListLayout.Cmd);
                        if (cmdArr != IntPtr.Zero)
                        {
                            IntPtr[] cmds = _runtime.ReadPointerArray(cmdArr);
                            if (cmds.Length > 0 && cmds[0] != IntPtr.Zero)
                            {
                                _runtime.WriteStringField(cmds[0], commandLayout.Caption, OriginalServerListCaption);
                            }
                        }
                    }
                    OriginalServerListCaption = null;
                }

                IntPtr loginScr = _runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, gameCanvasLayout.LoginScr);
                if (loginScr != IntPtr.Zero)
                {
                    IntPtr tfUser = _runtime.ReadFieldPtr(loginScr, loginScrLayout.TfUser);
                    if (tfUser != IntPtr.Zero)
                    {
                        int inputType = _runtime.ReadField<int>(tfUser, tfieldLayout.InputType);
                        if (inputType == GameSchema.TField.InputTypePassword)
                        {
                            _runtime.WriteField(tfUser, tfieldLayout.InputType, 0);
                        }
                    }
                }
            }
        }
        catch
        {
        }
    }

    public void SyncInGameCharacterNameMasking(bool isMasked, bool isInGame, string username)
    {
        if (!isInGame) return;

        try
        {
            var charLayout = _runtime.Layout.Char;
            var gameCanvasLayout = _runtime.Layout.GameCanvas;
            var loginScrLayout = _runtime.Layout.LoginScr;
            var tfieldLayout = _runtime.Layout.TField;

            IntPtr myCharPtr = _runtime.ReadStatic<IntPtr>(GameSchema.Char.ClassName, charLayout.MyChar);
            if (myCharPtr != IntPtr.Zero)
            {
                string currentCName = _runtime.ReadStringField(myCharPtr, charLayout.Name) ?? string.Empty;

                if (isMasked)
                {
                    if (!string.IsNullOrWhiteSpace(currentCName) && !currentCName.Contains('*'))
                    {
                        OriginalCharacterName ??= currentCName;
                        string masked = TextMaskHelper.MaskText(currentCName);
                        _runtime.WriteStringField(myCharPtr, charLayout.Name, masked);
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(OriginalCharacterName) && currentCName != OriginalCharacterName)
                    {
                        _runtime.WriteStringField(myCharPtr, charLayout.Name, OriginalCharacterName);
                        OriginalCharacterName = null;
                    }
                }
            }

            IntPtr loginScr = _runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, gameCanvasLayout.LoginScr);
            if (loginScr != IntPtr.Zero)
            {
                IntPtr tfUser = _runtime.ReadFieldPtr(loginScr, loginScrLayout.TfUser);
                if (tfUser != IntPtr.Zero)
                {
                    string currentTfUser = _runtime.ReadStringField(tfUser, tfieldLayout.Text) ?? string.Empty;
                    if (isMasked)
                    {
                        string maskedUser = TextMaskHelper.MaskText(username);
                        if (!string.IsNullOrEmpty(maskedUser) && currentTfUser != maskedUser)
                        {
                            _runtime.WriteStringField(tfUser, tfieldLayout.Text, maskedUser);
                            _runtime.WriteStringField(tfUser, tfieldLayout.PaintedText, maskedUser);
                            _runtime.WriteField(tfUser, tfieldLayout.InputType, GameSchema.TField.InputTypePassword);
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(username) && currentTfUser != username)
                        {
                            _runtime.WriteStringField(tfUser, tfieldLayout.Text, username);
                            _runtime.WriteStringField(tfUser, tfieldLayout.PaintedText, username);
                        }
                    }
                }
            }
        }
        catch
        {
        }
    }
}
