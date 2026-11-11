using Microsoft.AspNetCore.Http;

namespace ServiceDesk.SharedKernel.Web;

/// <summary>
/// Dựng body lỗi thống nhất theo tinh thần RFC 7807 nhưng giữ hợp đồng cũ để frontend không vỡ:
/// <c>error</c> (thông điệp, trường cũ) nằm cạnh các trường mới <c>code</c> (mã ổn định để client nhận diện),
/// <c>type</c> (<c>urn:servicedesk:error:{code}</c>) và <c>title</c> (tiêu đề ngắn theo mã HTTP).
/// Content-Type vẫn là <c>application/json</c>. Không thêm <c>errors</c> dạng object để không đổi cách frontend đọc lỗi.
/// </summary>
public static class ApiProblem
{
    /// <summary>Tiền tố của trường <c>type</c>.</summary>
    public const string TypePrefix = "urn:servicedesk:error:";

    /// <summary>Body lỗi dạng object để serialize.</summary>
    public static object Body(int statusCode, string code, string message) => new
    {
        error = message,
        code,
        type = TypePrefix + code,
        title = Title(statusCode)
    };

    /// <summary>Phản hồi JSON với mã HTTP và body lỗi thống nhất.</summary>
    public static IResult Result(int statusCode, string code, string message) =>
        Results.Json(Body(statusCode, code, message), statusCode: statusCode);

    /// <summary>Tiêu đề ngắn, ổn định theo mã HTTP.</summary>
    public static string Title(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Yêu cầu không hợp lệ",
        StatusCodes.Status401Unauthorized => "Chưa xác thực",
        StatusCodes.Status403Forbidden => "Không có quyền thực hiện",
        StatusCodes.Status404NotFound => "Không tìm thấy",
        StatusCodes.Status409Conflict => "Xung đột dữ liệu",
        StatusCodes.Status422UnprocessableEntity => "Không thể xử lý yêu cầu",
        StatusCodes.Status503ServiceUnavailable => "Dịch vụ tạm thời không khả dụng",
        _ => "Yêu cầu không thành công"
    };
}
