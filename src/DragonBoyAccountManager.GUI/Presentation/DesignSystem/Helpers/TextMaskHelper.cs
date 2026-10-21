namespace DragonBoyAccountManager.GUI.Presentation.DesignSystem.Helpers;

public static class TextMaskHelper
{
    public static string MaskText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string trimmed = text.Trim();

        if (IsEmail(trimmed))
            return MaskEmail(trimmed);

        if (IsPhoneNumber(trimmed))
            return MaskPhoneNumber(trimmed);

        return MaskGeneralText(trimmed);
    }

    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return string.Empty;

        int atIndex = email.IndexOf('@');
        if (atIndex <= 0 || atIndex >= email.Length - 1)
            return MaskGeneralText(email);

        string localPart = email[..atIndex];
        string domainPart = email[(atIndex + 1)..];

        string maskedLocal = MaskLocalPart(localPart);

        int dotIndex = domainPart.IndexOf('.');
        string maskedDomain = dotIndex > 0
            ? $"***{domainPart[dotIndex..]}"
            : "***";

        return $"{maskedLocal}@{maskedDomain}";
    }

    public static string MaskPhoneNumber(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;

        string trimmed = phone.Trim();

        if (trimmed.Length >= 6)
        {
            return $"{trimmed[..2]}***{trimmed[^2..]}";
        }

        return MaskGeneralText(trimmed);
    }

    private static string MaskLocalPart(string localPart)
    {
        return localPart.Length switch
        {
            0 => string.Empty,
            1 => "*",
            2 => $"{localPart[0]}*",
            3 => $"{localPart[0]}*{localPart[2]}",
            4 => $"{localPart[0]}**{localPart[3]}",
            _ => $"{localPart[0]}***{localPart[^1]}"
        };
    }

    private static string MaskGeneralText(string text)
    {
        return text.Length switch
        {
            0 => string.Empty,
            1 => "*",
            2 => $"{text[0]}*",
            3 => $"{text[0]}*{text[2]}",
            4 => $"{text[0]}**{text[3]}",
            5 => $"{text[0]}***{text[4]}",
            _ => $"{text[..2]}***{text[^2..]}"
        };
    }

    private static bool IsEmail(string text)
    {
        int atIndex = text.IndexOf('@');
        return atIndex > 0 && atIndex == text.LastIndexOf('@') && atIndex < text.Length - 1;
    }

    private static bool IsPhoneNumber(string text)
    {
        int startIndex = text.StartsWith('+') ? 1 : 0;
        if (text.Length - startIndex < 7 || text.Length - startIndex > 15)
            return false;

        for (int i = startIndex; i < text.Length; i++)
        {
            if (!char.IsAsciiDigit(text[i]))
                return false;
        }

        return true;
    }
}
