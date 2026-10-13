using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechStore.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// PAGE compression on the large tables (available in every edition since SQL Server 2016 SP1,
    /// Express included). Product rows are text-heavy and repetitive, so this roughly halves their
    /// size, which matters under Express's 10 GB per-database cap.
    /// </summary>
    public partial class PageCompression : Migration
    {
        private static readonly string[] Tables = ["Products", "ProductAttributes", "ProductImages"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var t in Tables)
                migrationBuilder.Sql($"ALTER INDEX ALL ON [{t}] REBUILD WITH (DATA_COMPRESSION = PAGE);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var t in Tables)
                migrationBuilder.Sql($"ALTER INDEX ALL ON [{t}] REBUILD WITH (DATA_COMPRESSION = NONE);");
        }
    }
}
