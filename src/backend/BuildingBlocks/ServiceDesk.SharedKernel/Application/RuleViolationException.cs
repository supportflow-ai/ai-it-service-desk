namespace ServiceDesk.SharedKernel.Application;

/// <summary>
/// Yêu cầu hợp lệ về hình thức nhưng vi phạm một quy tắc nghiệp vụ hoặc trạng thái hiện tại
/// (ví dụ chuyển trạng thái không cho phép). Mã HTTP do module ném lỗi chọn để giữ tương thích:
/// mặc định 400 (<see cref="BadRequestStatus"/>); Knowledge dùng 422 (<see cref="UnprocessableEntityStatus"/>).
/// </summary>
public sealed class RuleViolationException : DomainException
{
    /// <summary>HTTP 400, mặc định của Ticketing.</summary>
    public const int BadRequestStatus = 400;

    /// <summary>HTTP 422, hợp đồng hiện hành của Knowledge.</summary>
    public const int UnprocessableEntityStatus = 422;

    public RuleViolationException(string code, string message, int statusCode = BadRequestStatus)
        : base(code, message)
    {
        if (statusCode is < 400 or > 499)
            throw new ArgumentOutOfRangeException(nameof(statusCode), statusCode, "Lỗi quy tắc nghiệp vụ phải dùng mã HTTP 4xx.");
        StatusCode = statusCode;
    }

    public override int StatusCode { get; }
}
