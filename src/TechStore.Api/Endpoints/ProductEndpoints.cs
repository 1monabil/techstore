using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using TechStore.Api.Infrastructure;
using TechStore.Application;
using TechStore.Application.Catalog;
using TechStore.Application.Search;
using TechStore.Infrastructure.Search;

namespace TechStore.Api.Endpoints;

public static class ProductEndpoints
{
    public static void MapTechStoreApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/products/search", SearchAsync)
            .WithName("SearchProducts")
            .WithSummary("Full-text product search with filters, facets, typo tolerance and highlighting.")
            .WithDescription("""
                Example: /api/products/search?q=rtx 4070&category=laptops&brands=asus,msi&ramGb=16,32&minPrice=800&maxPrice=2000&sort=relevance&page=1&pageSize=24
                Unknown query keys are rejected. Pages are capped at 50; pass the returned nextCursor for deeper scrolling.
                """)
            .RequireRateLimiting(RateLimits.Search)
            .Produces<SearchResult>()
            .ProducesValidationProblem();

        api.MapGet("/products/autocomplete", AutocompleteAsync)
            .WithName("Autocomplete")
            .WithSummary("Grouped suggestions as the user types (min. 2 characters). Example: /api/products/autocomplete?q=rog str")
            .RequireRateLimiting(RateLimits.Autocomplete)
            .CacheOutput(OutputCachePolicies.Autocomplete)
            .Produces<AutocompleteResult>();

        api.MapGet("/products/{slug}", GetProductAsync)
            .WithName("GetProduct")
            .WithSummary("Product detail from SQL Server (the source of truth).")
            .Produces<ProductDetail>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/categories", (CatalogService catalog, CancellationToken ct) => catalog.GetTreeAsync(ct))
            .WithName("GetCategories")
            .WithSummary("Category tree with product counts (counts include descendants).")
            .CacheOutput(OutputCachePolicies.Catalog);

        api.MapGet("/home", (CatalogService catalog, CancellationToken ct) => catalog.GetHomeAsync(ct))
            .WithName("GetHome")
            .WithSummary("Featured categories, best sellers and new arrivals.")
            .CacheOutput(OutputCachePolicies.Catalog);

        api.MapPost("/admin/reindex", ReindexAsync)
            .WithName("Reindex")
            .WithSummary("Queues a zero-downtime full reindex (alias swap). Requires the X-Api-Key header.")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static async Task<Results<Ok<SearchResult>, ValidationProblem, ProblemHttpResult>> SearchAsync(
        HttpRequest request, IProductSearchService search, IValidator<SearchQuery> validator,
        IHostEnvironment env, CancellationToken ct)
    {
        var (query, errors) = SearchQueryParser.Parse(request.Query.Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value.ToString())));
        if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

        var validation = await validator.ValidateAsync(query, ct);
        if (!validation.IsValid) return TypedResults.ValidationProblem(validation.ToDictionary());

        // Score explanations are a debugging aid; never expose them outside Development.
        if (query.Explain && !env.IsDevelopment()) query = query with { Explain = false };

        try
        {
            return TypedResults.Ok(await search.SearchAsync(query, ct));
        }
        catch (InvalidCursorException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Invalid cursor");
        }
    }

    private static async Task<Results<Ok<AutocompleteResult>, ValidationProblem>> AutocompleteAsync(
        string? q, string? category, IProductSearchService search, IValidator<AutocompleteQuery> validator, CancellationToken ct)
    {
        var query = new AutocompleteQuery { Q = q?.Trim() ?? "", Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim().ToLowerInvariant() };
        if (query.Q.Length < AutocompleteQuery.MinLength)
            return TypedResults.Ok(new AutocompleteResult([], [], [], []));

        var validation = await validator.ValidateAsync(query, ct);
        if (!validation.IsValid) return TypedResults.ValidationProblem(validation.ToDictionary());
        return TypedResults.Ok(await search.AutocompleteAsync(query, ct));
    }

    private static async Task<Results<Ok<ProductDetail>, ProblemHttpResult>> GetProductAsync(
        string slug, IProductRepository products, CancellationToken ct)
    {
        var product = await products.GetBySlugAsync(slug, ct);
        return product is null
            ? TypedResults.Problem($"No product with slug '{slug}'.", statusCode: StatusCodes.Status404NotFound, title: "Product not found")
            : TypedResults.Ok(product);
    }

    private static async Task<Results<Accepted<ReindexAccepted>, ProblemHttpResult>> ReindexAsync(
        HttpRequest request, IConfiguration config, IReindexRequestQueue queue, CancellationToken ct)
    {
        var expected = config["Admin:ApiKey"];
        var provided = request.Headers["X-Api-Key"].ToString();
        if (string.IsNullOrEmpty(expected) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected)))
        {
            return TypedResults.Problem("A valid X-Api-Key header is required.", statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized");
        }

        var id = await queue.EnqueueAsync(ct);
        return TypedResults.Accepted((string?)null, new ReindexAccepted(id, "Reindex queued. The indexer worker builds a new index and swaps the alias when done."));
    }
}

public sealed record ReindexAccepted(long RequestId, string Message);
