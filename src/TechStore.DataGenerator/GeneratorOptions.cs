using Microsoft.Data.SqlClient;

namespace TechStore.DataGenerator;

public sealed record GeneratorOptions(long Count, int Batch, int Seed, string ConnectionString)
{
    private const string DefaultConnection =
        @"Server=.\SQLEXPRESS;Database=TechStore;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";

    public static GeneratorOptions Parse(string[] args)
    {
        string? Arg(string name)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        var count = long.Parse(Arg("--count") ?? "500000");
        var batch = int.Parse(Arg("--batch") ?? "50000");
        var seed = int.Parse(Arg("--seed") ?? "42");
        var cs = Arg("--connection") ?? Environment.GetEnvironmentVariable("ConnectionStrings__Sql") ?? DefaultConnection;
        if (count <= 0 || batch <= 0) throw new ArgumentException("--count and --batch must be positive.");

        // Long bulk loads: no command timeout surprises on the connection itself.
        var builder = new SqlConnectionStringBuilder(cs) { ConnectTimeout = 30 };
        return new GeneratorOptions(count, batch, seed, builder.ConnectionString);
    }
}
