using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game;

public class GameCanvas(MonoReflector reflector) : GameObject(reflector)
{
    public override string ClassName => "GameCanvas";

    private IntPtr _cachedPointer;
    public override IntPtr Pointer
    {
        get
        {
            if (_cachedPointer != IntPtr.Zero) return _cachedPointer;

            IntPtr ptr = ReadStatic<IntPtr>("instance");
            if (ptr == IntPtr.Zero)
                ptr = ReadStatic<IntPtr>("GameMidlet", "gameCanvas");
            if (ptr == IntPtr.Zero)
                ptr = ReadStatic<IntPtr>("gameCanvas");

            _cachedPointer = ptr;
            return ptr;
        }
    }

    public void InvalidatePointer() => _cachedPointer = IntPtr.Zero;

    public IntPtr CurrentScreen => ReadStatic<IntPtr>("currentScreen");
    public string? CurrentScreenName => Reflector.GetClassName(CurrentScreen);

    public IntPtr ServerScreen => ReadStatic<IntPtr>("serverScreen");
    public IntPtr ServerScr => ReadStatic<IntPtr>("serverScr");
    public IntPtr LoginScreen => ReadStatic<IntPtr>("loginScr");
    public IntPtr MenuPointer => ReadStatic<IntPtr>("menu");

    public IntPtr CurrentDialog => ReadStatic<IntPtr>("currentDialog");
    public bool HasDialog => CurrentDialog != IntPtr.Zero;

    public bool IsWaitDialog
    {
        get
        {
            IntPtr dlg = CurrentDialog;
            if (dlg == IntPtr.Zero) return false;
            try
            {
                return Reflector.ReadField<bool>("MsgDlg", "isWait", dlg);
            }
            catch
            {
                return false;
            }
        }
    }

    public string? GetDialogText()
    {
        IntPtr dlg = CurrentDialog;
        if (dlg == IntPtr.Zero) return null;
        try
        {
            IntPtr infoArr = Reflector.ReadFieldPtr("MsgDlg", "info", dlg);
            if (infoArr != IntPtr.Zero)
            {
                string[] lines = Reflector.ReadMonoStringArray(infoArr);
                if (lines.Length > 0)
                {
                    return string.Join(" ", lines.Where(s => !string.IsNullOrWhiteSpace(s)));
                }
            }
        }
        catch { }
        return null;
    }

    public void EndDlg() => CallStaticMethod("endDlg");

    public void StartWaitDlg(string info) => CallStaticMethod("startWaitDlg", info);

    public void Connect() => CallStaticMethod("connect");

    public void ClearKeyPressed() => CallStaticMethod("clearKeyPressed");

    public void ClearKeyHold() => CallStaticMethod("clearKeyHold");

    public void SetMenuSelectedItem(int index)
    {
        IntPtr menu = MenuPointer;
        if (menu != IntPtr.Zero)
            WriteField("Menu", "menuSelectedItem", menu, index);
    }

    public void KeyPressedz(int keyCode)
    {
        if (IsAlive)
            CallMethod("keyPressedz", keyCode);
    }

    public void KeyReleasedz(int keyCode)
    {
        if (IsAlive)
            CallMethod("keyReleasedz", keyCode);
    }
}
