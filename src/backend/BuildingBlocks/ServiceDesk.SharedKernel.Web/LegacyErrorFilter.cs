using Microsoft.AspNetCore.Http;
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
/// Bộ lọc chỉ biết các ngoại lệ trong SharedKernel nên module mới không phải sửa lớp này (Open/Closed).
/// </summary>
public sealed class LegacyErrorFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try { return await next(context); }
        catch (UnauthorizedAccessException ex) { return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (ConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
        catch (ServiceUnavailableException ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (RetryLaterException ex)
        {
            context.HttpContext.Response.Headers.RetryAfter = ex.RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }
}
