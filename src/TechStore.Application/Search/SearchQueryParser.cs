using System.Globalization;

namespace TechStore.Application.Search;

/// <summary>
/// Turns raw query-string pairs into a <see cref="SearchQuery"/>. Unknown keys and malformed
/// values become errors (keyed by parameter) rather than being silently ignored.
/// </summary>
public static class SearchQueryParser
{
    private const int MaxValuesPerKey = 20;

    public static (SearchQuery Query, IDictionary<string, string[]> Errors) Parse(
        IEnumerable<KeyValuePair<string, string?>> raw)
    {
        var errors = new Dictionary<string, string[]>();
        var specs = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var q = new SearchQuery();

        foreach (var (key, value) in raw)
        {
            if (!SearchFields.IsAllowed(key))
            {
                errors[key] = [$"Unknown filter '{key}'."];
                continue;
            }
            if (string.IsNullOrWhiteSpace(value)) continue;
            var v = value.Trim();

            switch (key.ToLowerInvariant())
            {
                case "q": q = q with { Q = v }; break;
                case "category": q = q with { Category = v.ToLowerInvariant() }; break;
                case "brands": q = q with { Brands = SplitList(v) }; break;
                case "minprice": q = q with { MinPrice = ParseDecimal(key, v, errors) }; break;
                case "maxprice": q = q with { MaxPrice = ParseDecimal(key, v, errors) }; break;
                case "minrating": q = q with { MinRating = ParseDecimal(key, v, errors) }; break;
                case "instock": q = q with { InStock = ParseBool(key, v, errors) }; break;
                case "sort": q = q with { Sort = v.ToLowerInvariant() }; break;
                case "page": q = q with { Page = ParseInt(key, v, errors) ?? 1 }; break;
                case "pagesize": q = q with { PageSize = ParseInt(key, v, errors) ?? 24 }; break;
                case "cursor": q = q with { Cursor = v }; break;
                case "explain": q = q with { Explain = ParseBool(key, v, errors) ?? false }; break;
                default:
                    var spec = SearchFields.Specs[key];
                    var values = SplitList(v);
                    if (spec.Kind != SpecFieldKind.Keyword &&
                        values.Any(x => !decimal.TryParse(x, NumberStyles.Number, CultureInfo.InvariantCulture, out _)))
                    {
                        errors[spec.Key] = [$"'{spec.Key}' accepts numbers only."];
                        break;
                    }
                    specs[spec.Key] = values;
                    break;
            }
        }

        foreach (var (k, list) in specs.Concat([new KeyValuePair<string, IReadOnlyList<string>>("brands", q.Brands)]))
        {
            if (list.Count > MaxValuesPerKey) errors[k] = [$"At most {MaxValuesPerKey} values allowed."];
        }

        return (q with { Specs = specs }, errors);
    }

    private static IReadOnlyList<string> SplitList(string v) =>
        v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
         .Distinct(StringComparer.OrdinalIgnoreCase)
         .ToArray();

    private static decimal? ParseDecimal(string key, string v, Dictionary<string, string[]> errors)
    {
        if (decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) return d;
        errors[key] = [$"'{key}' must be a number."];
        return null;
    }

    private static int? ParseInt(string key, string v, Dictionary<string, string[]> errors)
    {
        if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;
        errors[key] = [$"'{key}' must be a whole number."];
        return null;
    }

    private static bool? ParseBool(string key, string v, Dictionary<string, string[]> errors)
    {
        if (bool.TryParse(v, out var b)) return b;
        if (v == "1") return true;
        if (v == "0") return false;
        errors[key] = [$"'{key}' must be true or false."];
        return null;
    }
}
