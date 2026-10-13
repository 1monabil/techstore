using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TechStore.Application;
using TechStore.Domain.Entities;
using TechStore.Infrastructure.Persistence;
using TechStore.Infrastructure.Search;

namespace TechStore.IntegrationTests;

[Collection(SearchCollection.Name)]
public class SyncTests(SearchFixture fx)
{
    private async Task<Product> AddProductAsync(string name)
    {
        using var scope = fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TechStoreDbContext>();
        var product = new Product
        {
            Name = name, Sku = "", Slug = "", ShortDescription = "Test product", Description = $"{name} for integration tests.",
            BrandId = 9, CategoryId = 42, Price = 49.99m, StockQuantity = 5, Rating = 4.1m, ReviewCount = 3, SalesCount = 1,
        };
        var entry = db.Products.Add(product); // HiLo assigns the id; init-only props go through the tracker
        entry.Property(p => p.Sku).CurrentValue = $"TST-{product.Id}";
        entry.Property(p => p.Slug).CurrentValue = $"test-{product.Id}";
        await db.SaveChangesAsync();
        return product;
    }

    [Fact]
    public async Task Writes_reach_the_outbox_in_the_same_transaction_and_sync_to_es()
    {
        var product = await AddProductAsync("Zyxolite Trackball Mouse");

        using (var scope = fx.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TechStoreDbContext>();
            (await db.OutboxMessages.AnyAsync(m => m.AggregateId == product.Id && m.ProcessedAt == null)).Should().BeTrue();
        }

        await fx.DrainOutboxAsync();
        (await fx.SearchAsync("q=zyxolite")).Items.Should().ContainSingle(p => p.Id == product.Id);

        using (var scope = fx.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TechStoreDbContext>();
            var tracked = await db.Products.SingleAsync(p => p.Id == product.Id);
            db.Entry(tracked).Property(p => p.Price).CurrentValue = 19.99m;
            await db.SaveChangesAsync();
        }
        await fx.DrainOutboxAsync();
        (await fx.SearchAsync("q=zyxolite")).Items.Single().Price.Should().Be(19.99m);

        using (var scope = fx.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TechStoreDbContext>();
            var tracked = await db.Products.SingleAsync(p => p.Id == product.Id);
            db.Entry(tracked).Property(p => p.IsActive).CurrentValue = false;
            await db.SaveChangesAsync();
        }
        await fx.DrainOutboxAsync();
        (await fx.SearchAsync("q=zyxolite")).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Highlights_cannot_carry_markup_from_product_data()
    {
        await AddProductAsync("Qwertzia <script>alert(1)</script> <b>Mouse</b>");
        await fx.DrainOutboxAsync();

        var hit = (await fx.SearchAsync("q=qwertzia")).Items.Single();

        hit.Highlights!.Name.Should().Contain("<mark>Qwertzia</mark>")
            .And.Contain("&lt;script&gt;")
            .And.NotContain("<script>")
            .And.NotContain("<b>");
    }

    [Fact]
    public async Task Full_reindex_swaps_the_alias_atomically_and_drops_the_old_index()
    {
        var indices = fx.Services.GetRequiredService<IndexManager>();
        var before = await indices.IndicesBehindAliasAsync(CancellationToken.None);

        var report = await fx.Services.GetRequiredService<IReindexService>().ReindexAsync(CancellationToken.None);

        var after = await indices.IndicesBehindAliasAsync(CancellationToken.None);
        after.Should().ContainSingle().Which.Should().Be(report.IndexName);
        after.Should().NotIntersectWith(before);
        report.Documents.Should().BeGreaterThan(300);
        (await fx.SearchAsync("q=rtx%204090&pageSize=1")).Total.Should().BeGreaterThan(0);
    }
}
