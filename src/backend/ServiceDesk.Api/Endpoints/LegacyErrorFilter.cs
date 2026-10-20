using ServiceDesk.Application.Ticketing.Idempotency;

namespace ServiceDesk.Api.Endpoints;

public sealed class LegacyErrorFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try { return await next(context); }
        catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (IdempotencyConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
        catch (IdempotencyInProgressException ex)
        {
            context.HttpContext.Response.Headers.RetryAfter = "1";
            return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }
}
