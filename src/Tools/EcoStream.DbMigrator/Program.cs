// Applies EF Core migrations and loads geospatial seed data.
//   dotnet run --project src/Tools/EcoStream.DbMigrator                      -> migrate + seed infra/seed/porto-alegre
//   dotnet run --project src/Tools/EcoStream.DbMigrator -- --seed <folder>   -> custom seed folder
//   dotnet run --project src/Tools/EcoStream.DbMigrator -- --no-seed         -> migrations only
using EcoStream.Infrastructure;
using EcoStream.Infrastructure.Persistence;
using EcoStream.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddPersistence(builder.Configuration);
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DbMigrator");
await using var scope = host.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<EcoStreamDbContext>();

logger.LogInformation("Applying migrations...");
await db.Database.MigrateAsync();

if (!args.Contains("--no-seed"))
{
    var seedIndex = Array.IndexOf(args, "--seed");
    var folder = seedIndex >= 0 && seedIndex + 1 < args.Length
        ? args[seedIndex + 1]
        : FindRepoFolder(Path.Combine("infra", "seed", "porto-alegre"));

    await scope.ServiceProvider.GetRequiredService<GeoJsonSeeder>().SeedAsync(Path.GetFullPath(folder), CancellationToken.None);
}

logger.LogInformation("Database ready.");

static string FindRepoFolder(string relative)
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
    {
        var candidate = Path.Combine(dir.FullName, relative);
        if (Directory.Exists(candidate)) return candidate;
    }
    return relative; // works when running from the repo root / inside a container with the folder mounted
}
