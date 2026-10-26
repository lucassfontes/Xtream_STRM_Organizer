using System.Text;
using System.Text.RegularExpressions;

namespace XtreamStrmOrganizer;

public static partial class PathRules
{
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    public static string SafeName(string? text, string fallback = "Sem nome")
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            // Proibidos tanto no Linux como no Windows, e caracteres de controle.
            if (ch is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|' || char.IsControl(ch)) sb.Append(' ');
            else sb.Append(ch);
        }
        var cleaned = Whitespace().Replace(sb.ToString(), " ").Trim(' ', '.');
        if (cleaned is "" or "." or "..") return fallback;
        if (IsWindowsDeviceName(cleaned)) cleaned += "_";
        if (cleaned.Length > 130) cleaned = cleaned[..130].TrimEnd(' ', '.');
        return cleaned.Length == 0 ? fallback : cleaned;
    }

    private static bool IsWindowsDeviceName(string text)
    {
        var part = text.Split('.')[0].ToUpperInvariant();
        return part is "CON" or "PRN" or "AUX" or "NUL"
            or "COM1" or "COM2" or "COM3" or "COM4" or "COM5" or "COM6" or "COM7" or "COM8" or "COM9"
            or "LPT1" or "LPT2" or "LPT3" or "LPT4" or "LPT5" or "LPT6" or "LPT7" or "LPT8" or "LPT9";
    }
}
