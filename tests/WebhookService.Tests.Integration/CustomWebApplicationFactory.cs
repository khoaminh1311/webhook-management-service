using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebhookService.Infrastructure.Data;

namespace WebhookService.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory that replaces SQL Server with an in-memory database
/// so integration tests can run without a real database server.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove all DbContext-related registrations (options, context, and provider services)
            var descriptorsToRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<WebhookServiceDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true)
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            // Add an in-memory database for testing
            services.AddDbContext<WebhookServiceDbContext>(options =>
                options.UseInMemoryDatabase("IntegrationTestDb"));
        });

        builder.UseEnvironment("Development");
    }
}
