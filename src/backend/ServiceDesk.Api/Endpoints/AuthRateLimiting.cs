using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace ServiceDesk.Api.Endpoints;

/// <summary>Cấu hình giới hạn tần suất cho các endpoint xác thực nhạy cảm (mục <c>RateLimiting:Auth</c>).</summary>
public sealed class AuthRateLimitOptions
{
    public const string SectionName = "RateLimiting:Auth";

    /// <summary>Bật/tắt giới hạn. Mặc định bật.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Số request tối đa của một địa chỉ IP trong mỗi cửa sổ.</summary>
    public int PermitLimit { get; set; } = 10;

    /// <summary>Độ dài cửa sổ tính bằng giây.</summary>
    public int WindowSeconds { get; set; } = 60;
}

/// <summary>
/// Giới hạn số request theo địa chỉ IP cho đăng ký, đăng nhập, làm mới token, quên và đặt lại mật khẩu.
/// Vượt giới hạn trả 429. Request không có địa chỉ IP (ví dụ TestServer) không bị giới hạn;
/// sau reverse proxy, địa chỉ IP lấy từ Forwarded Headers đã cấu hình.
/// </summary>
public static class AuthRateLimiting
{
    public const string PolicyName = "auth-sensitive";

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthRateLimitOptions>()
            .Bind(configuration.GetSection(AuthRateLimitOptions.SectionName))
            .Validate(o => o.PermitLimit > 0 && o.WindowSeconds > 0, "RateLimiting:Auth cần PermitLimit và WindowSeconds lớn hơn 0.")
            .ValidateOnStart();
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(PolicyName, context =>
            {
                var options = context.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
                var address = context.Connection.RemoteIpAddress;
                if (!options.Enabled || address is null)
                    return RateLimitPartition.GetNoLimiter("unlimited");
                return RateLimitPartition.GetFixedWindowLimiter(address.ToString(), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.PermitLimit,
                    Window = TimeSpan.FromSeconds(options.WindowSeconds),
                    QueueLimit = 0
                });
            });
        });
        return services;
    }
}
