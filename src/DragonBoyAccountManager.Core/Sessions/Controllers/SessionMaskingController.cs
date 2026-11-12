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
        ApplyEarlyMaskingGuard(IsMasked);
        SyncInGameCharacterNameMasking(IsMasked, isInGame);
    }

    public void OnTick(bool isInGame)
    {
        if (IsMasked || HasOriginalServerListCaption)
        {
            ApplyEarlyMaskingGuard(IsMasked);
        }

        if (isInGame)
        {
            SyncInGameCharacterNameMasking(IsMasked, true);
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

    public static void EnsureRmsAccKey(IGameRuntime runtime)
    {
        string curRmsAcc = runtime.ReadStaticString(GameSchema.Rms.ClassName, runtime.Layout.Rms.RMS_acc);
        if (curRmsAcc != GameSchema.Rms.KeyAcc)
        {
            runtime.WriteStaticString(GameSchema.Rms.ClassName, runtime.Layout.Rms.RMS_acc, GameSchema.Rms.KeyAcc);
        }
    }

    public static string? ApplyServerListCaptionMask(IGameRuntime runtime, string targetCaption)
    {
        IntPtr serverScreen = runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, runtime.Layout.GameCanvas.ServerScreen);
        if (serverScreen == IntPtr.Zero) return null;

        IntPtr cmdArr = runtime.ReadFieldPtr(serverScreen, runtime.Layout.ServerListScreen.Cmd);
        if (cmdArr == IntPtr.Zero) return null;

        IntPtr[] cmds = runtime.ReadPointerArray(cmdArr);
        if (cmds.Length == 0 || cmds[0] == IntPtr.Zero) return null;

        string caption = runtime.ReadStringField(cmds[0], runtime.Layout.Command.Caption) ?? string.Empty;
        if (caption == targetCaption) return null;

        runtime.WriteStringField(cmds[0], runtime.Layout.Command.Caption, targetCaption);
        return caption;
    }

    public static void EnforceLoginScreenPasswordFields(IGameRuntime runtime, IntPtr loginScr)
    {
        if (loginScr == IntPtr.Zero) return;

        var loginScrLayout = runtime.Layout.LoginScr;
        var tfieldLayout = runtime.Layout.TField;
        IntPtr tfUser = runtime.ReadFieldPtr(loginScr, loginScrLayout.TfUser);
        IntPtr tfPass = runtime.ReadFieldPtr(loginScr, loginScrLayout.TfPass);

        if (tfUser != IntPtr.Zero && runtime.ReadField<int>(tfUser, tfieldLayout.InputType) != GameSchema.TField.InputTypePassword)
        {
            runtime.WriteField(tfUser, tfieldLayout.InputType, GameSchema.TField.InputTypePassword);
        }

        if (tfPass != IntPtr.Zero && runtime.ReadField<int>(tfPass, tfieldLayout.InputType) != GameSchema.TField.InputTypePassword)
        {
            runtime.WriteField(tfPass, tfieldLayout.InputType, GameSchema.TField.InputTypePassword);
        }
    }

    public void ApplyEarlyMaskingGuard(bool isMasked)
    {
        try
        {
            var gameCanvasLayout = _runtime.Layout.GameCanvas;
            var loginScrLayout = _runtime.Layout.LoginScr;
            var tfieldLayout = _runtime.Layout.TField;

            EnsureRmsAccKey(_runtime);

            if (isMasked)
            {
                string? prevCaption = ApplyServerListCaptionMask(_runtime, "DragonBoy Account Manager");
                if (prevCaption != null)
                {
                    OriginalServerListCaption ??= prevCaption;
                }

                IntPtr loginScr = _runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, gameCanvasLayout.LoginScr);
                EnforceLoginScreenPasswordFields(_runtime, loginScr);
            }
            else
            {
                if (!string.IsNullOrEmpty(OriginalServerListCaption))
                {
                    _ = ApplyServerListCaptionMask(_runtime, OriginalServerListCaption);
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

    public void SyncInGameCharacterNameMasking(bool isMasked, bool isInGame)
    {
        if (!isInGame) return;

        try
        {
            var charLayout = _runtime.Layout.Char;

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
        }
        catch
        {
        }
    }
}
