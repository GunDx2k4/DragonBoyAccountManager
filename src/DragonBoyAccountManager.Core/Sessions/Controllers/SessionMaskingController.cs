using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Interop.Mono;
using DragonBoyAccountManager.Core.Interop.Mono.Schema;

namespace DragonBoyAccountManager.Core.Sessions.Controllers;

public sealed class SessionMaskingController(IMonoRuntime mono)
{
    private readonly IMonoRuntime _mono = mono ?? throw new ArgumentNullException(nameof(mono));

    public string? OriginalCharacterName { get; private set; }
    public string? OriginalServerListCaption { get; private set; }

    public bool HasOriginalServerListCaption => !string.IsNullOrEmpty(OriginalServerListCaption);

    public void ResetCharacterName()
    {
        OriginalCharacterName = null;
    }

    public void ApplyEarlyMaskingGuard(bool isMasked, bool isInGame, string username)
    {
        try
        {
            if (isMasked)
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
                                OriginalServerListCaption ??= caption;
                                _mono.WriteStringField(GameSchema.Command.ClassName, GameSchema.Command.FieldCaption, cmds[0], "DragonBoy Account Manager");
                            }
                        }
                    }
                }

                IntPtr loginScr = _mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticLoginScr);
                if (loginScr != IntPtr.Zero)
                {
                    IntPtr tfUser = _mono.ReadFieldPtr(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldTfUser, loginScr);
                    IntPtr tfPass = _mono.ReadFieldPtr(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldTfPass, loginScr);

                    if (tfUser != IntPtr.Zero)
                    {
                        int inputType = _mono.ReadField<int>(GameSchema.TField.ClassName, GameSchema.TField.FieldInputType, tfUser);
                        if (inputType != GameSchema.TField.InputTypePassword)
                        {
                            _mono.WriteField(GameSchema.TField.ClassName, GameSchema.TField.FieldInputType, tfUser, GameSchema.TField.InputTypePassword);
                        }

                        if (!isInGame)
                        {
                            string curText = _mono.ReadStringField(GameSchema.TField.ClassName, GameSchema.TField.FieldText, tfUser) ?? string.Empty;
                            if (!string.IsNullOrEmpty(curText) && !string.Equals(curText, username, StringComparison.Ordinal) && !curText.Contains('*'))
                            {
                                string masked = TextMaskHelper.MaskText(curText);
                                _mono.WriteStringField(GameSchema.TField.ClassName, GameSchema.TField.FieldText, tfUser, masked);
                                _mono.WriteStringField(GameSchema.TField.ClassName, GameSchema.TField.FieldPaintedText, tfUser, masked);
                            }
                        }
                    }

                    if (tfPass != IntPtr.Zero)
                    {
                        int passInputType = _mono.ReadField<int>(GameSchema.TField.ClassName, GameSchema.TField.FieldInputType, tfPass);
                        if (passInputType != GameSchema.TField.InputTypePassword)
                        {
                            _mono.WriteField(GameSchema.TField.ClassName, GameSchema.TField.FieldInputType, tfPass, GameSchema.TField.InputTypePassword);
                        }
                    }
                }
            }
            else
            {
                string curRmsAcc = _mono.ReadStaticString(GameSchema.Rms.ClassName, GameSchema.Rms.StaticRMS_acc);
                if (curRmsAcc != "acc")
                {
                    _mono.WriteStaticString(GameSchema.Rms.ClassName, GameSchema.Rms.StaticRMS_acc, "acc");
                }

                if (!string.IsNullOrEmpty(OriginalServerListCaption))
                {
                    IntPtr serverScreen = _mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticServerScreen);
                    if (serverScreen != IntPtr.Zero)
                    {
                        IntPtr cmdArr = _mono.ReadFieldPtr(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.FieldCmd, serverScreen);
                        if (cmdArr != IntPtr.Zero)
                        {
                            IntPtr[] cmds = _mono.ReadMonoPointerArray(cmdArr);
                            if (cmds.Length > 0 && cmds[0] != IntPtr.Zero)
                            {
                                _mono.WriteStringField(GameSchema.Command.ClassName, GameSchema.Command.FieldCaption, cmds[0], OriginalServerListCaption);
                            }
                        }
                    }
                    OriginalServerListCaption = null;
                }

                IntPtr loginScr = _mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticLoginScr);
                if (loginScr != IntPtr.Zero)
                {
                    IntPtr tfUser = _mono.ReadFieldPtr(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldTfUser, loginScr);
                    if (tfUser != IntPtr.Zero)
                    {
                        int inputType = _mono.ReadField<int>(GameSchema.TField.ClassName, GameSchema.TField.FieldInputType, tfUser);
                        if (inputType == GameSchema.TField.InputTypePassword)
                        {
                            _mono.WriteField(GameSchema.TField.ClassName, GameSchema.TField.FieldInputType, tfUser, 0);
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
            IntPtr myCharPtr = _mono.ReadStatic<IntPtr>(GameSchema.Char.ClassName, GameSchema.Char.StaticMyChar);
            if (myCharPtr != IntPtr.Zero)
            {
                string currentCName = _mono.ReadStringField(GameSchema.Char.ClassName, GameSchema.Char.FieldName, myCharPtr) ?? string.Empty;

                if (isMasked)
                {
                    if (!string.IsNullOrWhiteSpace(currentCName) && !currentCName.Contains('*'))
                    {
                        OriginalCharacterName ??= currentCName;
                        string masked = TextMaskHelper.MaskText(currentCName);
                        _mono.WriteStringField(GameSchema.Char.ClassName, GameSchema.Char.FieldName, myCharPtr, masked);
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(OriginalCharacterName) && currentCName != OriginalCharacterName)
                    {
                        _mono.WriteStringField(GameSchema.Char.ClassName, GameSchema.Char.FieldName, myCharPtr, OriginalCharacterName);
                        OriginalCharacterName = null;
                    }
                }
            }

            IntPtr loginScr = _mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticLoginScr);
            if (loginScr != IntPtr.Zero)
            {
                IntPtr tfUser = _mono.ReadFieldPtr(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldTfUser, loginScr);
                if (tfUser != IntPtr.Zero)
                {
                    string currentTfUser = _mono.ReadStringField(GameSchema.TField.ClassName, GameSchema.TField.FieldText, tfUser) ?? string.Empty;
                    if (isMasked)
                    {
                        string maskedUser = TextMaskHelper.MaskText(username);
                        if (!string.IsNullOrEmpty(maskedUser) && currentTfUser != maskedUser)
                        {
                            _mono.WriteStringField(GameSchema.TField.ClassName, GameSchema.TField.FieldText, tfUser, maskedUser);
                            _mono.WriteStringField(GameSchema.TField.ClassName, GameSchema.TField.FieldPaintedText, tfUser, maskedUser);
                            _mono.WriteField(GameSchema.TField.ClassName, GameSchema.TField.FieldInputType, tfUser, GameSchema.TField.InputTypePassword);
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(username) && currentTfUser != username)
                        {
                            _mono.WriteStringField(GameSchema.TField.ClassName, GameSchema.TField.FieldText, tfUser, username);
                            _mono.WriteStringField(GameSchema.TField.ClassName, GameSchema.TField.FieldPaintedText, tfUser, username);
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
