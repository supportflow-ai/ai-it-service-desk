using ServiceDesk.Application.Ticketing.Errors;
using ServiceDesk.Application.Ticketing.Idempotency;

namespace ServiceDesk.Api.Endpoints;

/// <summary>
/// Bộ lọc xử lý ngoại lệ nghiệp vụ tại tầng Endpoint:
/// - UnauthorizedAccessException -> 401 Unauthorized
/// - KeyNotFoundException -> 404 Not Found
/// - IdempotencyConflictException -> 409 Conflict
/// - TicketConcurrencyException -> 409 Conflict
/// - IdempotencyInProgressException -> 503 Service Unavailable (kèm Retry-After)
/// - ArgumentException -> 400 Bad Request
/// </summary>
public sealed class LegacyErrorFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try { return await next(context); }
        catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (TicketConcurrencyException ex) { return Results.Conflict(new { error = ex.Message }); }
        catch (IdempotencyConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
        catch (IdempotencyInProgressException ex)
        {
            context.HttpContext.Response.Headers.RetryAfter = "1";
            return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }
}
