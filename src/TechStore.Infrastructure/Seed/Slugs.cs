using System.Globalization;
using System.Text;

namespace TechStore.Infrastructure.Seed;

public static class Slugs
{
    public static string From(string text, int max = 280)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        var dash = false;
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
                dash = false;
            }
            else if (!dash && sb.Length > 0)
            {
                sb.Append('-');
                dash = true;
            }
            if (sb.Length >= max) break;
        }
        return sb.ToString().Trim('-');
    }

    public static string Sku(string brand, string categorySlug, long id)
    {
        var b = new string(brand.Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant().PadRight(3, 'X');
        var c = new string(categorySlug.Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant();
        return $"{b}-{c}-{id:D8}";
    }
}
