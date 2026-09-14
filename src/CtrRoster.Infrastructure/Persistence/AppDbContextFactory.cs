using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CtrRoster.Infrastructure.Persistence;

/// <summary>
/// Usine de conception pour les outils EF Core (migrations CLI dotnet-ef).
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        // Utilise une chaîne SQLite factice pour la génération de migrations
        optionsBuilder.UseSqlite("Data Source=design_time_migration.db");

        return new AppDbContext(optionsBuilder.Options);
    }
}
