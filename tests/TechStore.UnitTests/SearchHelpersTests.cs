using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using TechStore.Application.Search;
using TechStore.Infrastructure;
using TechStore.Infrastructure.Search;

namespace TechStore.UnitTests;

public class SearchHelpersTests
{
    [Theory]
    [InlineData("lenvo labtop", "lenovo laptops", true)]
    [InlineData("assus rog", "asus rog", true)]
    [InlineData("iphone 16 pro 256", "iphone 16 pro 216", false)]
    [InlineData("rtx 4070", "rtx 4070", false)]
    [InlineData("b650 wifi", "b650 wi fi", false)]
    public void Did_you_mean_ignores_number_only_rewrites(string query, string suggestion, bool expected) =>
        ElasticProductSearchService.IsUsefulCorrection(query, suggestion).Should().Be(expected);

    [Theory]
    [InlineData("cors", "corsair", true)]
    [InlineData("corsiar", "corsair", true)]   // transposition
    [InlineData("logitec", "logitech", true)]
    [InlineData("xorsair", "corsair", false)]  // first letter must match
    [InlineData("ms", "msi", true)]
    [InlineData("mx", "msi", false)]           // too short for fuzziness
    public void Brand_matching_tolerates_typos_like_es_auto_fuzziness(string token, string word, bool expected) =>
        ReferenceData.PrefixMatches(token, word).Should().Be(expected);

    private static CursorCodec Codec(string key = "test-key") =>
        new(Options.Create(new SearchOptions { CursorSigningKey = key }));

    [Fact]
    public void Cursor_round_trips()
    {
        var q = new SearchQuery { Q = "ssd", Sort = SortOptions.PriceAsc };
        var token = Codec().Encode(new CursorCodec.Cursor(new JsonArray(89.99, 17), "pit", TextMode.Fuzzy, 3, CursorCodec.Fingerprint(q)));

        var c = Codec().Decode(token, q);

        c.PitId.Should().Be("pit");
        c.Mode.Should().Be(TextMode.Fuzzy);
        c.Page.Should().Be(3);
        c.SortValues.ToJsonString().Should().Be("[89.99,17]");
    }

    [Fact]
    public void Cursor_signed_with_another_key_is_rejected()
    {
        var q = new SearchQuery { Q = "ssd" };
        var token = Codec("attacker").Encode(new CursorCodec.Cursor(new JsonArray(1), null, TextMode.Exact, 2, CursorCodec.Fingerprint(q)));

        var act = () => Codec().Decode(token, q);

        act.Should().Throw<InvalidCursorException>().WithMessage("*signature*");
    }

    [Fact]
    public void Cursor_cannot_be_replayed_against_different_filters()
    {
        var original = new SearchQuery { Q = "ssd", Brands = ["samsung"] };
        var token = Codec().Encode(new CursorCodec.Cursor(new JsonArray(1), null, TextMode.Exact, 2, CursorCodec.Fingerprint(original)));

        var act = () => Codec().Decode(token, original with { Brands = ["kingston"] });

        act.Should().Throw<InvalidCursorException>();
    }
}
