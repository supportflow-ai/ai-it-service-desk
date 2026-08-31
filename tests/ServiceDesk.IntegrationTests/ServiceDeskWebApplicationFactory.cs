using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace ServiceDesk.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory that uses a real PostgreSQL container via Testcontainers.
/// Shared across all integration tests via IClassFixture.
/// </summary>
public class ServiceDeskWebApplicationFactory : WebApplicationFactory<ServiceDesk.Api.Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("servicedesk_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (dbDescriptor != null)
                services.Remove(dbDescriptor);

            // Add DbContext using the Testcontainer connection string
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));

            // Remove MinIO registrations — not available in test environment
            var minioClientDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IMinioClient));

            if (minioClientDescriptor != null)
                services.Remove(minioClientDescriptor);

            var storageDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IObjectStorageService));

            if (storageDescriptor != null)
                services.Remove(storageDescriptor);

            // Register stub implementations for tests
            services.AddSingleton<IObjectStorageService, TestObjectStorageService>();
        });

        builder.UseEnvironment("Development");
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    /// Stub object storage for integration tests — no real MinIO needed.
    /// </summary>
    private sealed class TestObjectStorageService : IObjectStorageService
    {
        public Task<string> GetPresignedUploadUrlAsync(
            string bucketName, string objectName, int expiryInSeconds = 3600,
            CancellationToken cancellationToken = default)
            => Task.FromResult($"https://test-storage/{bucketName}/{objectName}");

        public Task<string> GetPresignedDownloadUrlAsync(
            string bucketName, string objectName, int expiryInSeconds = 3600,
            CancellationToken cancellationToken = default)
            => Task.FromResult($"https://test-storage/{bucketName}/{objectName}");

        public Task<bool> BucketExistsAsync(
            string bucketName, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task EnsureBucketExistsAsync(
            string bucketName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
