using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpsGuardian.Infrastructure.Persistence;

namespace OpsGuardian.Web.Tests;

public sealed class OpsGuardianWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        $"opsguardian-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("BackgroundJobs:Enabled", "false");
        builder.UseSetting("ConnectionStrings:OpsGuardian", $"Data Source={databasePath};Pooling=False");
        builder.UseSetting("Database:Initialize", "false");
        builder.UseSetting("Database:Provider", "Sqlite");
    }

    public async Task InitializeAsync()
    {
        _ = Server;
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OpsGuardianDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }
}
