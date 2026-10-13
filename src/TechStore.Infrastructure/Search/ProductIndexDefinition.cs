using System.Text.Json;
using System.Text.Json.Nodes;

namespace TechStore.Infrastructure.Search;

/// <summary>
/// Settings, analyzers and mappings for the products index.
///
/// Analyzer note: the spec's single chain (… → word_delimiter_graph → synonym_graph → stemmer)
/// is rejected by ES ("Token filter [word_delimiter_graph] cannot be used to parse synonyms"), and
/// running word_delimiter_graph AFTER synonym_graph breaks the synonym graph (it is not graph-aware):
/// measured at 500k docs, "graphics card" then matched 64% of the catalogue. So:
///   product_text        (index):  standard → lowercase → asciifolding → word_delimiter_graph → flatten_graph → english
///   product_text_search (search): standard → lowercase → asciifolding → synonym_graph → english
///   searchText.joined:  index adjacent token pairs glued ("RTX 4070" → "rtx4070"), searched as-is
/// Index-time WDG makes "rtx 4090" find "RTX4090"; the joined subfield makes "rtx4070" find "RTX 4070";
/// "16GB" ⇄ "16 GB" works both ways the same way. Synonyms stay search-time only (no reindex to edit).
///
/// "dym" (did-you-mean) collects name + brand + category via copy_to and is shingled for the
/// phrase suggester, so "lenvo labtop" can be corrected even though laptop names rarely say "laptop".
/// "searchText" (name + brand + category + cpu/gpu) lets a query span fields, e.g. "lenovo laptop":
/// best_fields alone requires every term inside ONE field.
/// </summary>
public static class ProductIndexDefinition
{
    public static JsonObject Build(ElasticsearchOptions o, IReadOnlyList<string>? inlineSynonyms, bool bulkLoad)
    {
        var synonymFilter = new JsonObject { ["type"] = "synonym_graph", ["updateable"] = true };
        if (inlineSynonyms is not null)
            synonymFilter["synonyms"] = new JsonArray(inlineSynonyms.Select(s => (JsonNode)s!).ToArray());
        else
            synonymFilter["synonyms_path"] = o.SynonymsPath;

        var settings = JsonNode.Parse($$"""
        {
          "number_of_shards": {{o.NumberOfShards}},
          "number_of_replicas": {{(bulkLoad ? 0 : o.NumberOfReplicas)}},
          "refresh_interval": "{{(bulkLoad ? "-1" : o.RefreshInterval)}}",
          "max_result_window": 10000,
          "analysis": {
            "filter": {
              "product_wdg": {
                "type": "word_delimiter_graph",
                "preserve_original": true,
                "split_on_case_change": false,
                "split_on_numerics": true,
                "generate_word_parts": true,
                "generate_number_parts": true,
                "stem_english_possessive": true
              },
              "english_stemmer": { "type": "stemmer", "language": "english" },
              "name_shingle": { "type": "shingle", "min_shingle_size": 2, "max_shingle_size": 3, "output_unigrams": true },
              "joined_pairs": { "type": "shingle", "min_shingle_size": 2, "max_shingle_size": 2, "output_unigrams": true, "token_separator": "" }
            },
            "normalizer": {
              "lowercase_norm": { "type": "custom", "filter": ["lowercase", "asciifolding"] }
            },
            "analyzer": {
              "product_text": {
                "type": "custom", "tokenizer": "standard",
                "filter": ["lowercase", "asciifolding", "product_wdg", "flatten_graph", "english_stemmer"]
              },
              "product_text_search": {
                "type": "custom", "tokenizer": "standard",
                "filter": ["lowercase", "asciifolding", "product_synonyms", "english_stemmer"]
              },
              "joined_index": {
                "type": "custom", "tokenizer": "standard",
                "filter": ["lowercase", "asciifolding", "joined_pairs"]
              },
              "autocomplete_analyzer": {
                "type": "custom", "tokenizer": "standard",
                "filter": ["lowercase", "asciifolding"]
              },
              "shingle_analyzer": {
                "type": "custom", "tokenizer": "standard",
                "filter": ["lowercase", "asciifolding", "name_shingle"]
              }
            }
          }
        }
        """)!.AsObject();
        settings["analysis"]!["filter"]!["product_synonyms"] = synonymFilter;

        return new JsonObject
        {
            ["settings"] = new JsonObject { ["index"] = settings },
            ["mappings"] = Mappings(),
        };
    }

    private static JsonNode Mappings() => JsonNode.Parse("""
    {
      "dynamic": "strict",
      "properties": {
        "id":               { "type": "long" },
        "sku":              { "type": "keyword", "normalizer": "lowercase_norm" },
        "name": {
          "type": "text", "analyzer": "product_text", "search_analyzer": "product_text_search",
          "copy_to": ["dym", "searchText"],
          "index_options": "offsets",
          "fields": {
            "keyword": { "type": "keyword", "normalizer": "lowercase_norm", "ignore_above": 256 },
            "suggest": { "type": "search_as_you_type", "analyzer": "autocomplete_analyzer", "max_shingle_size": 3 }
          }
        },
        "dym":              { "type": "text", "analyzer": "shingle_analyzer" },
        "searchText": {
          "type": "text", "analyzer": "product_text", "search_analyzer": "product_text_search",
          "fields": { "joined": { "type": "text", "analyzer": "joined_index", "search_analyzer": "autocomplete_analyzer" } }
        },
        "slug":             { "type": "keyword", "index": false, "doc_values": false },
        "shortDescription": { "type": "text", "index": false },
        "description": {
          "type": "text", "analyzer": "product_text", "search_analyzer": "product_text_search",
          "index_options": "offsets"
        },
        "brand": {
          "type": "keyword", "copy_to": ["dym", "searchText"],
          "fields": { "text": { "type": "text", "analyzer": "product_text", "search_analyzer": "product_text_search" } }
        },
        "brandSlug":        { "type": "keyword" },
        "brandFacet":       { "type": "keyword" },
        "category": {
          "type": "keyword", "copy_to": ["dym", "searchText"],
          "fields": { "text": { "type": "text", "analyzer": "product_text", "search_analyzer": "product_text_search" } }
        },
        "categorySlug":     { "type": "keyword" },
        "categoryFacet":    { "type": "keyword" },
        "categoryPath":     { "type": "keyword" },
        "price":            { "type": "scaled_float", "scaling_factor": 100 },
        "listPrice":        { "type": "scaled_float", "scaling_factor": 100, "index": false },
        "discountPrice":    { "type": "scaled_float", "scaling_factor": 100, "index": false },
        "rating":           { "type": "half_float" },
        "reviewCount":      { "type": "integer" },
        "stock":            { "type": "integer" },
        "inStock":          { "type": "boolean" },
        "salesCount":       { "type": "integer" },
        "popularity":       { "type": "rank_feature" },
        "ratingBoost":      { "type": "rank_feature" },
        "createdAt":        { "type": "date" },
        "ramGb":            { "type": "short" },
        "storageGb":        { "type": "integer" },
        "storageType":      { "type": "keyword" },
        "cpuBrand":         { "type": "keyword" },
        "cpuModel": {
          "type": "text", "analyzer": "product_text", "search_analyzer": "product_text_search", "copy_to": "searchText",
          "fields": { "keyword": { "type": "keyword" } }
        },
        "gpuBrand":         { "type": "keyword" },
        "gpuModel": {
          "type": "text", "analyzer": "product_text", "search_analyzer": "product_text_search", "copy_to": "searchText",
          "fields": { "keyword": { "type": "keyword" } }
        },
        "screenSize":       { "type": "scaled_float", "scaling_factor": 10 },
        "refreshRate":      { "type": "short" },
        "color":            { "type": "keyword" },
        "connectivity":     { "type": "keyword" },
        "attrs":            { "type": "flattened" },
        "imageUrl":         { "type": "keyword", "index": false, "doc_values": false },
        "specLine":         { "type": "keyword", "index": false, "doc_values": false },
        "suggest": {
          "type": "completion",
          "analyzer": "autocomplete_analyzer",
          "contexts": [ { "name": "category", "type": "category" } ]
        }
      }
    }
    """)!;

    public static string ToJson(JsonObject o) => o.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
}
