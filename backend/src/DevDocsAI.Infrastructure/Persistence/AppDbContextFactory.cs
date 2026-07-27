using DevDocsAI.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DevDocsAI.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by `dotnet ef` to create the context for
/// migrations. The connection string need not point at a live database.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=devdocs;Username=devdocs;Password=devdocs";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .Options;

        return new AppDbContext(options, new SystemClock());
    }
}
