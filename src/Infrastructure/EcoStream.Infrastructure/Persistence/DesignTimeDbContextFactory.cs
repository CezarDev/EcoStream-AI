using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EcoStream.Infrastructure.Persistence;

/// <summary>Lets `dotnet ef` work against this project alone, without booting a host.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<EcoStreamDbContext>
{
    public EcoStreamDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=ecostream;Username=ecostream;Password=ecostream";

        var options = new DbContextOptionsBuilder<EcoStreamDbContext>();
        DependencyInjection.ConfigureNpgsql(options, connectionString);
        return new EcoStreamDbContext(options.Options);
    }
}
