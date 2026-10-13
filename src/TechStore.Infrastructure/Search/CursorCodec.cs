using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Buffers.Text;
using Microsoft.Extensions.Options;
using TechStore.Application.Search;

namespace TechStore.Infrastructure.Search;

public sealed class InvalidCursorException(string message) : Exception(message);

/// <summary>
/// Opaque, HMAC-signed search_after cursors. The cursor carries the last hit's sort values,
/// the point-in-time id (once one is opened), the text mode used, and a fingerprint of the
/// query so a cursor cannot be replayed against different filters.
/// </summary>
public sealed class CursorCodec(IOptions<SearchOptions> options)
{
    public sealed record Cursor(JsonArray SortValues, string? PitId, TextMode Mode, int Page, string Fingerprint);

    private readonly byte[] _key = Encoding.UTF8.GetBytes(options.Value.CursorSigningKey);

    public string Encode(Cursor c)
    {
        var payload = new JsonObject
        {
            ["s"] = c.SortValues.DeepClone(),
            ["p"] = c.PitId,
            ["m"] = (int)c.Mode,
            ["n"] = c.Page,
            ["f"] = c.Fingerprint,
        };
        var bytes = Encoding.UTF8.GetBytes(payload.ToJsonString());
        return $"{Base64Url.EncodeToString(bytes)}.{Base64Url.EncodeToString(Sign(bytes))}";
    }

    public Cursor Decode(string token, SearchQuery query)
    {
        var parts = token.Split('.');
        if (parts.Length != 2) throw new InvalidCursorException("Malformed cursor.");
        byte[] bytes, sig;
        try
        {
            bytes = Base64Url.DecodeFromChars(parts[0]);
            sig = Base64Url.DecodeFromChars(parts[1]);
        }
        catch (FormatException) { throw new InvalidCursorException("Malformed cursor."); }

        if (!CryptographicOperations.FixedTimeEquals(sig, Sign(bytes)))
            throw new InvalidCursorException("Cursor signature is invalid.");

        var o = JsonNode.Parse(bytes)!.AsObject();
        var c = new Cursor(o["s"]!.AsArray(), (string?)o["p"], (TextMode)(int)o["m"]!, (int)o["n"]!, (string)o["f"]!);
        if (c.Fingerprint != Fingerprint(query))
            throw new InvalidCursorException("Cursor belongs to a different search. Start again from page 1.");
        return c;
    }

    /// <summary>Hash of everything that defines the result set (not page/cursor).</summary>
    public static string Fingerprint(SearchQuery q)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            q = q.Q?.Trim().ToLowerInvariant(),
            q.Category,
            b = q.Brands.Select(x => x.ToLowerInvariant()).Order(),
            q.MinPrice, q.MaxPrice, q.MinRating, q.InStock,
            s = q.Specs.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={string.Join(',', kv.Value.Order())}"),
            q.Sort,
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..16];
    }

    private byte[] Sign(byte[] data) => HMACSHA256.HashData(_key, data);
}
