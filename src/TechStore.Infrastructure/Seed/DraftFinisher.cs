using System.Globalization;
using System.Text;

namespace TechStore.Infrastructure.Seed;

/// <summary>Fills in the market-side numbers (rating, stock, discount…) and writes descriptions.</summary>
public static class DraftFinisher
{
    private static readonly Dictionary<string, string[]> Openers = new()
    {
        ["laptops"] = ["Built for long sessions away from the desk,", "A portable machine that doesn't compromise,", "Designed for work and play,"],
        ["tablets"] = ["Thin, light and ready for the couch or the classroom,", "A tablet that doubles as a sketchbook and a second screen,"],
        ["smartphones"] = ["A flagship-grade phone in your pocket,", "Everyday speed with a camera worth carrying,"],
        ["graphics-cards"] = ["Push higher frame rates at higher resolutions:", "A graphics card built for modern ray-traced games and creative work:"],
        ["processors"] = ["The heart of a fast build:", "A desktop processor tuned for gaming and multitasking:"],
        ["motherboards"] = ["A solid foundation for your next build:", "Feature-rich board with the connectivity a modern build needs:"],
        ["ram"] = ["Faster memory for smoother multitasking:", "High-frequency memory tuned for current platforms:"],
        ["ssds"] = ["Boot in seconds and load games faster:", "Fast, reliable solid state storage:"],
        ["hdds"] = ["High-capacity spinning storage:", "Bulk storage you can count on:"],
        ["power-supplies"] = ["Clean, stable power for demanding builds:", "A quiet, efficient power supply:"],
        ["cases"] = ["Room for big GPUs and serious airflow:", "A clean-looking case that is easy to build in:"],
        ["monitors"] = ["Sharper, smoother visuals:", "A display that keeps up with fast games and long workdays:"],
        ["keyboards"] = ["Typing and gaming feel the way they should:", "A keyboard built to last millions of keystrokes:"],
        ["mice"] = ["Precise tracking in a comfortable shell:", "A mouse built for accuracy and long sessions:"],
        ["headsets"] = ["Hear every footstep and every note:", "Comfortable audio for long sessions:"],
        ["routers"] = ["Faster Wi-Fi for every room:", "Reliable coverage for busy homes:"],
        ["switches"] = ["Add wired ports in seconds:", "Plug-and-play wired networking:"],
    };

    public static void Finish(ProductDraft d, Random rng, DateTime now)
    {
        d.Price = Math.Max(9, Math.Round(d.Price, 0) - 0.01m);
        d.DiscountPrice = rng.NextDouble() < 0.22
            ? Math.Round(d.Price * (decimal)(0.80 + rng.NextDouble() * 0.15), 0) - 0.01m
            : null;
        d.Rating = Math.Round((decimal)(3.4 + Math.Pow(rng.NextDouble(), 0.6) * 1.6), 1);
        d.ReviewCount = (int)Math.Round(Math.Pow(rng.NextDouble(), 2.2) * 4800) + rng.Next(0, 40);
        d.SalesCount = (int)(d.ReviewCount * (6 + rng.NextDouble() * 18)) + rng.Next(0, 200);
        d.StockQuantity = rng.NextDouble() < 0.12 ? 0 : rng.Next(1, 250);
        d.CreatedAt = now.AddDays(-rng.Next(0, 720)).AddMinutes(-rng.Next(0, 1440));
    }

    public static (string Short, string Long) Describe(ProductDraft d, Random rng)
    {
        var specs = SpecLine(d);
        var sb = new StringBuilder();
        var openers = Openers.TryGetValue(d.CategorySlug, out var o) ? o : ["Quality hardware from a brand you know:"];
        sb.Append(openers[rng.Next(openers.Length)]).Append(' ');
        sb.Append(CultureInfo.InvariantCulture, $"the {d.Name} ");
        sb.Append(d.Highlights.Count > 0 ? $"pairs {string.Join(" and ", d.Highlights)}" : "delivers dependable performance");
        sb.Append(". ");
        if (specs.Length > 0) sb.Append(CultureInfo.InvariantCulture, $"Key specs: {specs}. ");
        foreach (var (k, v) in d.Attributes.Take(4)) sb.Append(CultureInfo.InvariantCulture, $"{k}: {v}. ");
        sb.Append(CultureInfo.InvariantCulture, $"Backed by the {d.Brand} warranty.");
        var shortText = specs.Length > 0 ? specs : string.Join(", ", d.Highlights);
        return (Trim(shortText, 480), Trim(sb.ToString(), 3900));
    }

    public static string SpecLine(ProductDraft d)
    {
        var parts = new List<string>();
        if (d.ScreenSizeInches is { } s) parts.Add($"{s.ToString("0.#", CultureInfo.InvariantCulture)}\"{(d.RefreshRateHz is { } hz ? $" {hz}Hz" : "")}");
        if (d.CpuModel is { } cpu && d.CategorySlug != "processors") parts.Add(cpu);
        if (d.GpuModel is { } gpu) parts.Add(gpu);
        if (d.RamGb is { } ram) parts.Add($"{ram}GB {(d.CategorySlug == "ram" ? "kit" : "RAM")}");
        if (d.StorageGb is { } st) parts.Add($"{Units.StorageLabel(st)} {d.StorageType}");
        if (d.Connectivity is { } c && d.CategorySlug is "routers" or "keyboards" or "mice" or "headsets") parts.Add(c);
        return string.Join(", ", parts);
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max];
}
