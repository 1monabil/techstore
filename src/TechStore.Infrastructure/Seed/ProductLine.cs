namespace TechStore.Infrastructure.Seed;

/// <summary>One choice on a variant axis: a name fragment, a price delta and the specs it sets.</summary>
public sealed record VariantOption(string Label, decimal PriceDelta, Action<ProductDraft> Apply);

/// <summary>
/// A real product line (e.g. "ASUS ROG Strix G16") with its variant axes. The curated seed
/// enumerates a spread of combinations; the bulk generator samples combinations at random.
/// </summary>
/// <param name="Format">Name after the brand. "{0}", "{1}"… are replaced by axis labels in order;
/// labels for axes without a placeholder are appended.</param>
public sealed record ProductLine(
    string Category,
    string Brand,
    string Format,
    decimal BasePrice,
    Action<ProductDraft> Base,
    IReadOnlyList<VariantOption[]> Axes,
    int CuratedCount = 2)
{
    public long CombinationCount => Axes.Aggregate(1L, (n, axis) => n * axis.Length);

    public ProductDraft Build(IReadOnlyList<int> choice)
    {
        var d = new ProductDraft { CategorySlug = Category, Brand = Brand, Price = BasePrice };
        Base(d);
        var labels = new string[Axes.Count];
        for (var i = 0; i < Axes.Count; i++)
        {
            var opt = Axes[i][choice[i]];
            labels[i] = opt.Label;
            d.Price += opt.PriceDelta;
            opt.Apply(d);
        }

        var name = Format;
        var appended = new List<string>();
        for (var i = 0; i < labels.Length; i++)
        {
            var token = "{" + i + "}";
            if (name.Contains(token)) name = name.Replace(token, labels[i]);
            else if (labels[i].Length > 0) appended.Add(labels[i]);
        }
        d.Name = string.Join(' ', new[] { Brand, name }.Concat(appended).Where(s => s.Length > 0))
            .Replace("  ", " ").Trim();
        return d;
    }

    public int[] ChoiceAt(long index)
    {
        var choice = new int[Axes.Count];
        for (var i = Axes.Count - 1; i >= 0; i--)
        {
            choice[i] = (int)(index % Axes[i].Length);
            index /= Axes[i].Length;
        }
        return choice;
    }

    public int[] RandomChoice(Random rng) => Axes.Select(a => rng.Next(a.Length)).ToArray();
}
