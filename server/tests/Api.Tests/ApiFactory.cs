using Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Tests;

/// <summary>
/// Boots the real app against a throwaway SQLite file, one per factory, so
/// tests never share state and never need an external database. Agents run
/// unattended and cannot start a service for you — keep it that way.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"iac-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<GameDbContext>>();
            services.RemoveAll<GameDbContext>();

            services.AddDbContext<GameDbContext>(options =>
                options.UseSqlite($"Data Source={_dbPath}"));

            using var scope = services.BuildServiceProvider().CreateScope();
            scope.ServiceProvider.GetRequiredService<GameDbContext>().Database.Migrate();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch (IOException)
            {
                // A held SQLite handle is not worth failing a test run over.
            }
        }
    }
}
