using Microsoft.AspNetCore.Http;
using ServiceDesk.Application.Common.Exceptions;

namespace ServiceDesk.Api.Endpoints;

public static class ApiErrors
{
    public static IResult ToProblem(Exception exception) => exception switch
    {
        ValidationException ex => Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Bad Request",
            detail: ex.Message),
        ForbiddenException ex => Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Forbidden",
            detail: ex.Message),
        NotFoundException ex => Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            detail: ex.Message),
        ConflictException ex => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Conflict",
            detail: ex.Message),
        _ => Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An unexpected error occurred.")
    };
}

public sealed class ApiExceptionEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (Exception ex) when (ex is ValidationException or ForbiddenException or NotFoundException or ConflictException)
        {
            return ApiErrors.ToProblem(ex);
        }
    }
}
