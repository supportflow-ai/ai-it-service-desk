using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceDesk.SharedKernel.Application;

namespace ServiceDesk.SharedKernel.Web;

/// <summary>
/// Bộ lọc xử lý ngoại lệ nghiệp vụ tại tầng Endpoint, dùng chung cho mọi module:
/// - UnauthorizedAccessException -> 403 Forbidden
/// - KeyNotFoundException -> 404 Not Found
/// - ConflictException (xung đột ghi đồng thời, Idempotency-Key tái sử dụng sai payload) -> 409 Conflict
/// - ServiceUnavailableException -> 503 Service Unavailable
/// - RetryLaterException (ví dụ request cùng Idempotency-Key đang xử lý) -> 503 Service Unavailable kèm Retry-After
/// - ArgumentException -> 400 Bad Request
/// Với UnauthorizedAccessException và ArgumentException, thông điệp chỉ được trả cho client khi ngoại lệ do mã
/// ServiceDesk ném ra (quy tắc nghiệp vụ). Ngoại lệ cùng loại đến từ .NET hoặc thư viện (ví dụ kiểm tra tham số
/// <c>ArgumentNullException.ThrowIfNull</c>, EF Core, Npgsql) vẫn giữ mã trạng thái nhưng trả thông điệp chung và
/// chỉ ghi chi tiết vào log, để không lộ thông tin nội bộ.
/// Bộ lọc chỉ biết các ngoại lệ trong SharedKernel nên module mới không phải sửa lớp này (Open/Closed).
/// </summary>
public sealed class ApiErrorFilter : IEndpointFilter
{
    /// <summary>Thông điệp 400 khi lỗi tham số không đến từ quy tắc nghiệp vụ.</summary>
    public const string GenericBadRequestMessage = "Yêu cầu không hợp lệ.";

    /// <summary>Thông điệp 403 khi lỗi truy cập không đến từ quy tắc nghiệp vụ.</summary>
    public const string GenericForbiddenMessage = "Bạn không có quyền thực hiện thao tác này.";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        try { return await next(context); }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Json(new { error = PublicMessage(context, ex, GenericForbiddenMessage) }, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (ConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
        catch (ServiceUnavailableException ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (RetryLaterException ex)
        {
            context.HttpContext.Response.Headers.RetryAfter = ex.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = PublicMessage(context, ex, GenericBadRequestMessage) }); }
    }

    private static string PublicMessage(EndpointFilterInvocationContext context, Exception exception, string fallback)
    {
        if (ExceptionOrigin.IsRaisedByApplication(exception)) return exception.Message;

        context.HttpContext.RequestServices?.GetService<ILoggerFactory>()?.CreateLogger<ApiErrorFilter>()
            .LogWarning(exception, "Ngoại lệ {ExceptionType} từ thư viện được trả về dạng thông điệp chung.", exception.GetType().Name);
        return fallback;
    }
}
