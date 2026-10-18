using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using ServiceDesk.Application.AIAssistance.Interfaces;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Identity.Interfaces;
using ServiceDesk.Application.Ticketing.Interfaces;
using ServiceDesk.Infrastructure.AI;
using ServiceDesk.Infrastructure.Clock;
using ServiceDesk.Infrastructure.Identity;
using ServiceDesk.Infrastructure.Options;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Infrastructure.Storage;
using ServiceDesk.Infrastructure.Ticketing;

namespace ServiceDesk.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
        services.AddPersistence(configuration).AddIdentityInfrastructure(configuration).AddStorage(configuration).AddClock().AddAiAdapters();

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddDbContext<ApplicationDbContext>((sp, options) => options.UseNpgsql(
            sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));
        services.AddScoped<ITicketWriter, PostgresTicketWriter>();
        services.AddScoped<ITicketQueries, PostgresTicketQueries>();
        return services;
    }

    public static IServiceCollection AddIdentityStores(this IServiceCollection services)
    {
        services.AddDataProtection();
        services.AddIdentityCore<IdentityUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 8;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
        services.AddScoped<IUserAccountGateway, IdentityAccountGateway>();
        return services;
    }

    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentityStores();
        services.AddSingleton<IAccessTokenIssuer, JwtTokenGenerator>();
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        return services;
    }

    public static IServiceCollection AddClock(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        return services;
    }

    public static IServiceCollection AddAiAdapters(this IServiceCollection services)
    {
        services.AddSingleton<ITicketClassificationService, NoOpTicketClassificationService>();
        services.AddSingleton<IEmbeddingService, NoOpEmbeddingService>();
        return services;
    }

    public static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MinioOptions>().Bind(configuration.GetSection(MinioOptions.SectionName));
        services.AddSingleton<IMinioClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
            return new MinioClient().WithEndpoint(options.Endpoint).WithCredentials(options.AccessKey, options.SecretKey)
                .WithSSL(options.UseSsl).Build();
        });
        services.AddSingleton<IObjectStorageService, MinioObjectStorageService>();
        return services;
    }
}
