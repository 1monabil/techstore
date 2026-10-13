namespace TechStore.Infrastructure.Seed;

/// <summary>
/// The deterministic curated catalog (~400 products): for each real product line, a spread of
/// variant combinations, with a fixed random seed so every environment gets identical data.
/// </summary>
public static class CuratedCatalog
{
    public static IReadOnlyList<(ProductDraft Draft, string Short, string Long)> Build(DateTime now)
    {
        var rng = new Random(20251013);
        var result = new List<(ProductDraft, string, string)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in CatalogTemplates.Lines)
        {
            var total = line.CombinationCount;
            var take = (int)Math.Min(line.CuratedCount, total);
            for (var i = 0; i < take; i++)
            {
                // Spread picks across the combination space instead of taking the first N.
                var index = take == 1 ? 0 : (long)Math.Round(i * (total - 1) / (double)(take - 1));
                var draft = line.Build(line.ChoiceAt(index));
                if (!seen.Add(draft.Name)) continue;
                DraftFinisher.Finish(draft, rng, now);
                var (s, l) = DraftFinisher.Describe(draft, rng);
                result.Add((draft, s, l));
            }
        }
        return result;
    }
}
