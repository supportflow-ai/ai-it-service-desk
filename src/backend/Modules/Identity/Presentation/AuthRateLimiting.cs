using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.Identity.Presentation;

/// <summary>
/// Cấu hình giới hạn tần suất cho endpoint xác thực (mục <c>RateLimiting:Auth</c>). Các con số mặc định là điểm xuất phát
/// để đo trên staging sau reverse proxy, chưa phải ngưỡng đã chốt.
/// </summary>
public sealed class AuthRateLimitOptions
{
    public const string SectionName = "RateLimiting:Auth";

    /// <summary>Bật/tắt mọi giới hạn. Mặc định bật.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Số request tối đa của một địa chỉ IP trong mỗi cửa sổ, tính chung cho đăng ký, đăng nhập, quên và đặt lại mật khẩu.
    /// Nới rộng vì nhân viên trong văn phòng thường dùng chung một IP công cộng.
    /// </summary>
    public int PermitLimit { get; set; } = 60;

    /// <summary>Độ dài cửa sổ theo IP, tính bằng giây.</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>Ngân sách riêng theo IP cho làm mới phiên, để đăng nhập hay quên mật khẩu không làm hết lượt refresh.</summary>
    public int RefreshPermitLimit { get; set; } = 120;

    /// <summary>Số lần tối đa cho mỗi email đã chuẩn hóa và mỗi loại thao tác (đăng nhập, quên, đặt lại mật khẩu).</summary>
    public int EmailPermitLimit { get; set; } = 10;

    /// <summary>Độ dài cửa sổ theo email, tính bằng giây (mặc định 15 phút).</summary>
    public int EmailWindowSeconds { get; set; } = 900;
}

/// <summary>Thao tác xác thực có ngân sách riêng theo email.</summary>
public enum AuthEmailOperation
{
    Register,
    Login,
    ForgotPassword,
    ResetPassword
}

/// <summary>
/// Giới hạn tần suất hai lớp cho endpoint xác thực:
/// <list type="bullet">
/// <item>Theo IP: <see cref="IpPolicy"/> cho đăng ký, đăng nhập, quên và đặt lại mật khẩu; <see cref="RefreshPolicy"/> riêng cho làm mới phiên.</item>
/// <item>Theo email đã chuẩn hóa và loại thao tác (<see cref="RequireEmailRateLimit{TRequest}"/>): khóa giới hạn là chuỗi email gửi lên,
/// không phụ thuộc email có tồn tại, nên phản hồi 429 giống nhau cho mọi email.</item>
/// </list>
/// Vượt giới hạn trả 429 kèm <c>Retry-After</c> (giây). Request không có địa chỉ IP (ví dụ TestServer) không bị giới hạn theo IP;
/// sau reverse proxy, địa chỉ IP lấy từ Forwarded Headers đã cấu hình. Bộ đếm nằm trong bộ nhớ của từng instance API.
/// </summary>
public static class AuthRateLimiting
{
    public const string IpPolicy = "auth-ip";
    public const string RefreshPolicy = "auth-refresh";

    /// <summary>Thông điệp khi vượt giới hạn.</summary>
    public const string RejectionMessage = "Bạn đã gửi quá nhiều yêu cầu trong thời gian ngắn. Vui lòng thử lại sau.";

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthRateLimitOptions>()
            .Bind(configuration.GetSection(AuthRateLimitOptions.SectionName))
            .Validate(o => o.PermitLimit > 0 && o.WindowSeconds > 0 && o.RefreshPermitLimit > 0 && o.EmailPermitLimit > 0 && o.EmailWindowSeconds > 0,
                "RateLimiting:Auth cần mọi giới hạn và cửa sổ lớn hơn 0.")
            .ValidateOnStart();
        services.AddSingleton<AuthEmailRateLimiter>();
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    SetRetryAfter(context.HttpContext.Response, retryAfter);
                await context.HttpContext.Response.WriteAsJsonAsync(new { error = RejectionMessage }, cancellationToken);
            };
            limiter.AddPolicy(IpPolicy, context => IpPartition(context, o => o.PermitLimit));
            limiter.AddPolicy(RefreshPolicy, context => IpPartition(context, o => o.RefreshPermitLimit));
        });
        return services;
    }

    /// <summary>Áp ngân sách theo email cho endpoint nhận <typeparamref name="TRequest"/>.</summary>
    public static RouteHandlerBuilder RequireEmailRateLimit<TRequest>(this RouteHandlerBuilder builder, AuthEmailOperation operation,
        Func<TRequest, string?> email)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(email);
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
            var limiter = context.HttpContext.RequestServices.GetRequiredService<AuthEmailRateLimiter>();
            if (request is not null && !limiter.TryAcquire(operation, email(request), out var retryAfter))
            {
                SetRetryAfter(context.HttpContext.Response, retryAfter);
                return Results.Json(new { error = RejectionMessage }, statusCode: StatusCodes.Status429TooManyRequests);
            }

            return await next(context);
        });
    }

    internal static void SetRetryAfter(HttpResponse response, TimeSpan retryAfter) =>
        response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

    private static RateLimitPartition<string> IpPartition(HttpContext context, Func<AuthRateLimitOptions, int> permitLimit)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
        var address = context.Connection.RemoteIpAddress;
        if (!options.Enabled || address is null)
            return RateLimitPartition.GetNoLimiter("unlimited");
        return RateLimitPartition.GetFixedWindowLimiter(address.ToString(), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit(options),
            Window = TimeSpan.FromSeconds(options.WindowSeconds),
            QueueLimit = 0
        });
    }
}

/// <summary>Bộ đếm theo email đã chuẩn hóa và loại thao tác (cửa sổ cố định, giới hạn số partition trong bộ nhớ).</summary>
public sealed class AuthEmailRateLimiter : IDisposable
{
    internal const int MaxPartitions = 4096;
    internal const int MaxEmailLength = 320;

    private readonly AuthRateLimitOptions options;
    private readonly IClock clock;
    private readonly object sync = new();
    private readonly Dictionary<string, WindowCounter> counters = new(StringComparer.Ordinal);

    public AuthEmailRateLimiter(IOptions<AuthRateLimitOptions> options, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        this.options = options.Value;
        this.clock = clock;
    }

    /// <summary>Email rỗng để use case từ chối; khóa quá dài hoặc hết chỗ nhận cùng phản hồi giới hạn.</summary>
    public bool TryAcquire(AuthEmailOperation operation, string? email, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        if (!options.Enabled || string.IsNullOrWhiteSpace(email)) return true;

        var window = TimeSpan.FromSeconds(options.EmailWindowSeconds);
        var trimmed = email.AsSpan().Trim();
        if (trimmed.Length > MaxEmailLength)
        {
            retryAfter = window;
            return false;
        }
        var key = operation + ":" + trimmed.ToString().ToLowerInvariant();

        lock (sync)
        {
            var now = clock.UtcNow;
            if (!counters.TryGetValue(key, out var counter))
            {
                if (counters.Count >= MaxPartitions)
                {
                    retryAfter = window;
                    foreach (var pair in counters)
                    {
                        var remaining = pair.Value.Start + window - now;
                        if (remaining <= TimeSpan.Zero)
                            counters.Remove(pair.Key);
                        else if (remaining < retryAfter)
                            retryAfter = remaining;
                    }
                    if (counters.Count >= MaxPartitions) return false;
                }
                counter = new WindowCounter(now, 0);
            }
            else if (now - counter.Start >= window)
            {
                counter = new WindowCounter(now, 0);
            }

            if (counter.Count >= options.EmailPermitLimit)
            {
                retryAfter = counter.Start + window - now;
                return false;
            }

            counters[key] = counter with { Count = counter.Count + 1 };
            retryAfter = TimeSpan.Zero;
            return true;
        }
    }

    public void Dispose()
    {
        lock (sync) counters.Clear();
    }

    private readonly record struct WindowCounter(DateTimeOffset Start, int Count);
}
