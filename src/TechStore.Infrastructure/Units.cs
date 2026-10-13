namespace TechStore.Infrastructure;

public static class Units
{
    /// <summary>Capacities are shown the way the trade labels them: 1000 GB is "1 TB", 960 GB stays "960 GB".</summary>
    public static (int Amount, string Unit) Storage(int gb) =>
        gb >= 1000 && gb % 1000 == 0 ? (gb / 1000, "TB") : (gb, "GB");

    /// <summary>Compact form used in product names and spec lines: "1TB", "512GB".</summary>
    public static string StorageLabel(int gb)
    {
        var (amount, unit) = Storage(gb);
        return $"{amount}{unit}";
    }

    /// <summary>Spaced form used in facet labels: "1 TB", "512 GB".</summary>
    public static string StorageDisplay(int gb)
    {
        var (amount, unit) = Storage(gb);
        return $"{amount} {unit}";
    }
}
