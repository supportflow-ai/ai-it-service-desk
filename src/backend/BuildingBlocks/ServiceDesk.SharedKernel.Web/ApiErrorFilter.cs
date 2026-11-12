using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceDesk.SharedKernel.Application;

namespace ServiceDesk.SharedKernel.Web;

/// <summary>
/// Nơi duy nhất chuyển lỗi của endpoint thành phản hồi HTTP, dùng chung cho mọi module.
/// <list type="bullet">
/// <item><see cref="DomainException"/> (lỗi do mã ServiceDesk chủ động ném): mã HTTP lấy từ
/// <see cref="DomainException.StatusCode"/> (400, 403, 404, 409, 422, 503); thông điệp được trả cho client;
/// <see cref="RetryLaterException"/> kèm <c>Retry-After</c>; 404 không có body.</item>
/// <item><see cref="UnauthorizedAccessException"/>, <see cref="ArgumentException"/> (kể cả
/// <see cref="ArgumentNullException"/>) và <see cref="KeyNotFoundException"/> thuần: chỉ đến từ .NET hoặc thư viện nên giữ mã
/// 403/400/404 như trước nhưng KHÔNG trả thông điệp gốc, chỉ ghi chi tiết vào log.</item>
/// </list>
/// Body lỗi giữ trường <c>error</c> như hợp đồng cũ và bổ sung <c>code</c>, <c>type</c>, <c>title</c> (xem <see cref="ApiProblem"/>).
/// Module mới chỉ cần ném <see cref="DomainException"/>; không phải sửa lớp này.
/// </summary>
public sealed class ApiErrorFilter : IEndpointFilter
{
    /// <summary>Thông điệp 400 khi lỗi tham số không đến từ quy tắc nghiệp vụ.</summary>
    public const string GenericBadRequestMessage = "Yêu cầu không hợp lệ.";

    /// <summary>Thông điệp 403 khi lỗi truy cập không đến từ quy tắc nghiệp vụ.</summary>
    public const string GenericForbiddenMessage = "Bạn không có quyền thực hiện thao tác này.";

    /// <summary>Mã lỗi của 400 chung (lỗi tham số từ .NET hoặc thư viện).</summary>
    public const string InvalidRequestCode = "invalid_request";

    /// <summary>Mã lỗi của 403 chung (lỗi truy cập từ .NET hoặc thư viện).</summary>
    public const string ForbiddenCode = "forbidden";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        try { return await next(context); }
        catch (DomainException ex) { return Map(context.HttpContext, ex); }
        catch (UnauthorizedAccessException ex)
        {
            LogHidden(context, ex);
            return ApiProblem.Result(StatusCodes.Status403Forbidden, ForbiddenCode, GenericForbiddenMessage);
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (ArgumentException ex)
        {
            LogHidden(context, ex);
            return ApiProblem.Result(StatusCodes.Status400BadRequest, InvalidRequestCode, GenericBadRequestMessage);
        }
    }

    private static IResult Map(HttpContext http, DomainException exception)
    {
        if (exception.StatusCode == StatusCodes.Status404NotFound) return Results.NotFound();
        if (exception is RetryLaterException retry)
            http.Response.Headers.RetryAfter = retry.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        return ApiProblem.Result(exception.StatusCode, exception.Code, exception.Message);
    }

    private static void LogHidden(EndpointFilterInvocationContext context, Exception exception) =>
        context.HttpContext.RequestServices?.GetService<ILoggerFactory>()?.CreateLogger<ApiErrorFilter>()
            .LogWarning(exception, "Ngoại lệ {ExceptionType} từ thư viện được trả về dạng thông điệp chung.", exception.GetType().Name);
}
