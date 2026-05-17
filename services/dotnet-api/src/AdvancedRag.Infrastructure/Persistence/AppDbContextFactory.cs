using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AdvancedRag.Infrastructure.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__AppDatabase")
            ?? "Host=localhost;Database=advanced_rag;Username=app_owner;Password=local";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema))
            .Options;

        return new AppDbContext(options);
    }
}
