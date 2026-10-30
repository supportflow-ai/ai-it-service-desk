using ServiceDesk.Application.Common;
using ServiceDesk.Application.Ticketing.Idempotency;

namespace ServiceDesk.Api.Endpoints;

/// <summary>
/// Bộ lọc xử lý ngoại lệ nghiệp vụ tại tầng Endpoint:
/// - UnauthorizedAccessException -> 403 Forbidden
/// - KeyNotFoundException -> 404 Not Found
/// - ConcurrencyConflictException -> 409 Conflict
/// - IdempotencyConflictException -> 409 Conflict
/// - ServiceUnavailableException -> 503 Service Unavailable
/// - IdempotencyInProgressException -> 503 Service Unavailable (kèm Retry-After)
/// - ArgumentException -> 400 Bad Request
/// </summary>
public sealed class LegacyErrorFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try { return await next(context); }
        catch (UnauthorizedAccessException ex) { return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (ConcurrencyConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
        catch (IdempotencyConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
        catch (ServiceUnavailableException ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (IdempotencyInProgressException ex)
        {
            context.HttpContext.Response.Headers.RetryAfter = "1";
            return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }
}
