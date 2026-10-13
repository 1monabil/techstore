using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TechStore.Infrastructure.Persistence;

/// <summary>Used by `dotnet ef` only; the connection string is never opened for `migrations add`.</summary>
internal sealed class DesignTimeFactory : IDesignTimeDbContextFactory<TechStoreDbContext>
{
    public TechStoreDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Sql")
                 ?? @"Server=.\SQLEXPRESS;Database=TechStore;Integrated Security=True;TrustServerCertificate=True";
        return new TechStoreDbContext(new DbContextOptionsBuilder<TechStoreDbContext>().UseSqlServer(cs).Options);
    }
}
