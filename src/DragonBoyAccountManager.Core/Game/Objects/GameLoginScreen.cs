using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game;

public class GameLoginScreen(MonoReflector reflector, IntPtr pointer = default) : GameScreen(reflector)
{
    public override string ClassName => "LoginScr";

    private readonly IntPtr _pointer = pointer;

    public override IntPtr Pointer
    {
        get
        {
            if (_pointer != IntPtr.Zero) return _pointer;

            IntPtr cur = ReadStatic<IntPtr>("GameCanvas", "currentScreen");
            if (cur != IntPtr.Zero && Reflector.GetClassName(cur) == ClassName)
                return cur;

            IntPtr ptr = ReadStatic<IntPtr>("GameCanvas", "loginScr");
            if (ptr != IntPtr.Zero) return ptr;

            return IntPtr.Zero;
        }
    }

    public IntPtr TfUserPointer => ReadFieldPtr("tfUser");
    public IntPtr TfPassPointer => ReadFieldPtr("tfPass");

    public GameTField? TfUser => TfUserPointer != IntPtr.Zero ? new GameTField(Reflector, TfUserPointer) : null;
    public GameTField? TfPass => TfPassPointer != IntPtr.Zero ? new GameTField(Reflector, TfPassPointer) : null;

    public GameTField? UserTextField => TfUser;
    public GameTField? PasswordTextField => TfPass;

    public void DoLogin() => CallMethod("doLogin");

    public void SwitchToMe() => CallMethod("switchToMe");

    public bool SetCredentials(string username, string password)
    {
        username ??= string.Empty;
        password ??= string.Empty;

        var userField = UserTextField;
        var passField = PasswordTextField;
        if (userField == null || passField == null) return false;

        userField.SetText(username);
        passField.SetText(password);

        // Lưu RMS trực tiếp để đảm bảo RMS lưu đúng tài khoản kể cả khi perform(2008) đọc RMS
        try
        {
            Reflector.CallMethod("Rms", "saveRMSString", IntPtr.Zero, "acc", username.Trim());
            Reflector.CallMethod("Rms", "saveRMSString", IntPtr.Zero, "pass", password.Trim());
        }
        catch { }

        // Kiểm tra xác nhận cả hai trường đã nhận đúng giá trị
        string currentUsername = userField.GetText() ?? userField.Text;
        string currentPassword = passField.GetText() ?? passField.Text;

        return string.Equals(currentUsername, username, StringComparison.Ordinal)
            && string.Equals(currentPassword, password, StringComparison.Ordinal);
    }
}
