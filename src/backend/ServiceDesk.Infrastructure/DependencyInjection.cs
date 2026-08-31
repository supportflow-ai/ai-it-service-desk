using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using ServiceDesk.Application.AIAssistance.Interfaces;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Infrastructure.AI;
using ServiceDesk.Infrastructure.Clock;
using ServiceDesk.Infrastructure.Identity;
using ServiceDesk.Infrastructure.Options;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Infrastructure.Storage;

namespace ServiceDesk.Infrastructure;

/// <summary>
/// Registers Infrastructure layer services.
/// Called from the API/Worker composition root.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- Database ---
        var dbOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
            ?? new DatabaseOptions();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(dbOptions.ConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
            }));

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        // --- ASP.NET Core Identity ---
        services.AddIdentityCore<IdentityUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        // --- MinIO ---
        var minioOptions = configuration.GetSection(MinioOptions.SectionName).Get<MinioOptions>()
            ?? new MinioOptions();

        services.AddSingleton<IMinioClient>(_ =>
            new MinioClient()
                .WithEndpoint(minioOptions.Endpoint)
                .WithCredentials(minioOptions.AccessKey, minioOptions.SecretKey)
                .WithSSL(minioOptions.UseSsl)
                .Build());

        services.AddSingleton<IObjectStorageService, MinioObjectStorageService>();

        // --- Current User ---
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        // --- Clock ---
        services.AddSingleton<IClock, SystemClock>();

        // --- AI (NoOp for bootstrap) ---
        services.AddSingleton<ITicketClassificationService, NoOpTicketClassificationService>();
        services.AddSingleton<IEmbeddingService, NoOpEmbeddingService>();

        return services;
    }
}
