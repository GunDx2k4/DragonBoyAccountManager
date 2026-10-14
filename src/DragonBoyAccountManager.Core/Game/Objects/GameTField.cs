using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game;

public class GameTField(MonoReflector reflector, IntPtr pointer) : GameObject(reflector)
{
    public override string ClassName => "TField";
    public override IntPtr Pointer => pointer;

    public string Text => ReadFieldString("text");
    public string PaintedText => ReadFieldString("paintedText");
    public string PasswordText => ReadFieldString("passwordText");
    public int CaretPos => ReadField<int>("caretPos");

    public string? GetText()
    {
        if (!IsAlive) return null;
        try
        {
            IntPtr res = CallMethod("getText");
            if (res != IntPtr.Zero)
            {
                string text = Reflector.ReadMonoString(res);
                if (!string.IsNullOrEmpty(text))
                    return text;
            }
        }
        catch { }

        return Text;
    }

    public void SetText(string text)
    {
        if (!IsAlive) return;

        text ??= string.Empty;

        // 1. Thử gọi hàm setText của game client
        try
        {
            CallMethod("setText", text);
        }
        catch { }

        // 2. Kiểm tra xem game đã nhận đúng text chưa
        string current = GetText() ?? string.Empty;
        if (string.Equals(current, text, StringComparison.Ordinal))
            return;

        // 3. Fallback: Nếu setText không ăn hoặc bị sai, ghi trực tiếp vào memory field của TField
        IntPtr monoStr = Reflector.CreateMonoString(text);
        if (monoStr != IntPtr.Zero)
        {
            WriteField("text", monoStr);
            WriteField("paintedText", monoStr);
            WriteField("caretPos", text.Length);

            string stars = new string('*', text.Length);
            IntPtr starStr = Reflector.CreateMonoString(stars);
            if (starStr != IntPtr.Zero)
            {
                WriteField("passwordText", starStr);
            }
        }
    }
}
