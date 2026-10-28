using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using ServiceDesk.Application.AIAssistance.Ports;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Identity.Ports;
using ServiceDesk.Application.Ticketing.Ports;
using ServiceDesk.Infrastructure.AI;
using ServiceDesk.Infrastructure.Clock;
using ServiceDesk.Infrastructure.Identity;
using ServiceDesk.Infrastructure.Options;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Infrastructure.Storage;
using ServiceDesk.Infrastructure.Ticketing;

namespace ServiceDesk.Infrastructure;

/// <summary>
/// Đăng ký các dịch vụ thuộc tầng hạ tầng (Infrastructure) vào DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Đăng ký toàn bộ hạ tầng: Persistence, Identity, Storage, Clock, AI Adapters.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
        services.AddPersistence(configuration).AddIdentityInfrastructure(configuration).AddStorage(configuration).AddClock().AddAiAdapters();

    /// <summary>Cấu hình cơ sở dữ liệu PostgreSQL (EF Core) và các cổng truy vấn/ghi dữ liệu ticket.</summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddDbContext<ApplicationDbContext>((sp, options) => options.UseNpgsql(
            sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));
        services.AddScoped<ITicketWriter, PostgresTicketWriter>();
        services.AddScoped<ITicketQueries, PostgresTicketQueries>();
        services.AddSingleton<ITicketTimelineStore, InMemoryTicketTimelineStore>();
        return services;
    }

    /// <summary>Đăng ký các kho lưu trữ Identity (User, Role, DataProtection) và Gateway quản lý tài khoản.</summary>
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

    /// <summary>Cấu hình hạ tầng danh tính và cấp phát mã JWT Bearer Token.</summary>
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentityStores();
        services.AddSingleton<IAccessTokenIssuer, JwtTokenGenerator>();
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        return services;
    }

    /// <summary>Đăng ký dịch vụ thời gian hệ thống.</summary>
    public static IServiceCollection AddClock(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        return services;
    }

    /// <summary>Đăng ký kiểm tra sức khỏe hệ thống (Health Check) với PostgreSQL.</summary>
    public static IServiceCollection AddBackendHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddNpgSql(
            sp => sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString,
            name: "postgresql", tags: ["db", "ready"]);
        return services;
    }

    /// <summary>Đăng ký các bộ chuyển đổi AI tạm thời (NoOp) khi chưa tích hợp AI Service thật.</summary>
    public static IServiceCollection AddAiAdapters(this IServiceCollection services)
    {
        services.AddSingleton<ITicketClassificationService, NoOpTicketClassificationService>();
        services.AddSingleton<IEmbeddingService, NoOpEmbeddingService>();
        return services;
    }

    /// <summary>Đăng ký client MinIO Object Storage (tương thích AWS S3).</summary>
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
