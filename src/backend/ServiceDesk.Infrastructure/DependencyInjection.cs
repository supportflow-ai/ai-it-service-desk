using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Minio;
using ServiceDesk.Infrastructure.Clock;
using ServiceDesk.Infrastructure.Options;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Infrastructure.Storage;
using ServiceDesk.Modules.Identity;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Infrastructure;

/// <summary>
/// Đăng ký hạ tầng kỹ thuật dùng chung vào DI container: Persistence (DbContext duy nhất), object storage và đồng hồ.
/// Không đăng ký module nào: host tự ghép từng module bằng <c>Add{Module}Module</c>; riêng kho ASP.NET Core Identity
/// cần kiểu DbContext cụ thể nên host gọi thêm <see cref="AddIdentityStores"/>.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Đăng ký toàn bộ hạ tầng dùng chung: Persistence, Storage, Clock.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
        services.AddPersistence(configuration).AddStorage(configuration).AddClock();

    /// <summary>
    /// Cấu hình PostgreSQL (EF Core) với <see cref="ApplicationDbContext"/> và đăng ký bí danh <see cref="DbContext"/>
    /// để adapter trong module dùng chung đúng một context theo scope mà không phụ thuộc kiểu cụ thể.
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddDbContext<ApplicationDbContext>((sp, options) => options.UseNpgsql(
            sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));
        services.TryAddScoped<DbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        return services;
    }

    /// <summary>
    /// Đăng ký phần lưu trữ của module Identity (kho người dùng/vai trò ASP.NET Core Identity, refresh token, OTP,
    /// danh bạ người dùng) trên <see cref="ApplicationDbContext"/>. Module không biết kiểu context cụ thể nên host truyền vào.
    /// </summary>
    public static IServiceCollection AddIdentityStores(this IServiceCollection services) =>
        services.AddIdentityPersistence<ApplicationDbContext>();

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
