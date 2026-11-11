using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using ServiceDesk.Modules.Knowledge.Domain;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Web;
using Xunit;

namespace ServiceDesk.IntegrationTests;

/// <summary>
/// Bộ lọc lỗi giữ mã HTTP cũ, chỉ trả thông điệp của <see cref="DomainException"/> (lỗi do mã ServiceDesk chủ động ném);
/// lỗi cùng loại từ .NET hoặc thư viện trả thông điệp chung. Body giữ <c>error</c> và bổ sung <c>code</c>, <c>type</c>, <c>title</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ApiErrorFilterTests
{
    private sealed record Outcome(int? Status, JsonElement? Body, string? RetryAfter)
    {
        public string? Error => Body?.GetProperty("error").GetString();
        public string? Code => Body?.GetProperty("code").GetString();
    }

    private static async Task<Outcome> RunAsync(Func<object?> endpoint)
    {
        var http = new DefaultHttpContext();
        var context = new DefaultEndpointFilterInvocationContext(http);
        var result = await new ApiErrorFilter().InvokeAsync(context, _ => ValueTask.FromResult(endpoint()));
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode;
        var retryAfter = http.Response.Headers.RetryAfter.ToString();
        var value = (result as IValueHttpResult)?.Value;
        if (value is null) return new Outcome(status, null, retryAfter.Length == 0 ? null : retryAfter);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return new Outcome(status, json.RootElement.Clone(), retryAfter.Length == 0 ? null : retryAfter);
    }

    private static object? ThrowRule() => throw new InvalidInputException("title_required", "Tiêu đề không được để trống.");

    private static object? ThrowLibraryArgument()
    {
        var map = new Dictionary<string, int> { ["a"] = 1 };
        map.Add("a", 2);
        return null;
    }

    private static object? ThrowGuard()
    {
        string? missing = null;
        ArgumentNullException.ThrowIfNull(missing);
        return null;
    }

    [Fact]
    public async Task BusinessRuleMessageIsReturnedWith400()
    {
        var outcome = await RunAsync(ThrowRule);
        Assert.Equal(StatusCodes.Status400BadRequest, outcome.Status);
        Assert.Equal("Tiêu đề không được để trống.", outcome.Error);
    }

    [Fact]
    public async Task BodyKeepsErrorFieldAndAddsCodeTypeAndTitle()
    {
        var outcome = await RunAsync(ThrowRule);
        var body = outcome.Body!.Value;
        Assert.Equal("title_required", outcome.Code);
        Assert.Equal("urn:servicedesk:error:title_required", body.GetProperty("type").GetString());
        Assert.Equal("Yêu cầu không hợp lệ", body.GetProperty("title").GetString());
        // Không thêm trường nào khác, nhất là `errors` dạng object, để frontend không đổi cách đọc lỗi.
        Assert.Equal(["code", "error", "title", "type"], body.EnumerateObject().Select(p => p.Name).Order().ToArray());
    }

    [Fact]
    public async Task LibraryArgumentErrorKeepsStatusButHidesMessage()
    {
        var outcome = await RunAsync(ThrowLibraryArgument);
        Assert.Equal(StatusCodes.Status400BadRequest, outcome.Status);
        Assert.Equal(ApiErrorFilter.GenericBadRequestMessage, outcome.Error);
        Assert.Equal(ApiErrorFilter.InvalidRequestCode, outcome.Code);
    }

    [Fact]
    public async Task FrameworkGuardMessageIsHidden()
    {
        var outcome = await RunAsync(ThrowGuard);
        Assert.Equal(StatusCodes.Status400BadRequest, outcome.Status);
        Assert.Equal(ApiErrorFilter.GenericBadRequestMessage, outcome.Error);
    }

    [Fact]
    public async Task ApplicationAccessDeniedMessageIsReturnedWith403()
    {
        var outcome = await RunAsync(() => throw new ForbiddenException("admin_required", "Chỉ quản trị viên mới được làm."));
        Assert.Equal(StatusCodes.Status403Forbidden, outcome.Status);
        Assert.Equal("Chỉ quản trị viên mới được làm.", outcome.Error);
        Assert.Equal("admin_required", outcome.Code);
    }

    [Fact]
    public async Task LibraryAccessErrorKeepsStatusButHidesMessage()
    {
        var outcome = await RunAsync(() => throw new UnauthorizedAccessException("chi tiết nội bộ"));
        Assert.Equal(StatusCodes.Status403Forbidden, outcome.Status);
        Assert.Equal(ApiErrorFilter.GenericForbiddenMessage, outcome.Error);
        Assert.Equal(ApiErrorFilter.ForbiddenCode, outcome.Code);
    }

    [Fact]
    public async Task MissingResourceIs404WithoutBody()
    {
        var outcome = await RunAsync(() => throw new NotFoundException("ticket_not_found", "chi tiết nội bộ"));
        Assert.Equal(StatusCodes.Status404NotFound, outcome.Status);
        Assert.Null(outcome.Body);
    }

    [Fact]
    public async Task LegacyKeyNotFoundIsStill404WithoutBody()
    {
        var outcome = await RunAsync(() => throw new KeyNotFoundException("chi tiết nội bộ"));
        Assert.Equal(StatusCodes.Status404NotFound, outcome.Status);
        Assert.Null(outcome.Body);
    }

    [Fact]
    public async Task ConflictIs409WithItsStableCode()
    {
        var outcome = await RunAsync(() => throw new ConcurrencyConflictException());
        Assert.Equal(StatusCodes.Status409Conflict, outcome.Status);
        Assert.Equal(ConcurrencyConflictException.ErrorCode, outcome.Code);
    }

    [Fact]
    public async Task RetryLaterIs503WithRetryAfter()
    {
        var outcome = await RunAsync(() => throw new Modules.Ticketing.Application.Idempotency.IdempotencyInProgressException(new TimeoutException()));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, outcome.Status);
        Assert.Equal("1", outcome.RetryAfter);
        Assert.Equal("idempotency_in_progress", outcome.Code);
    }

    [Fact]
    public async Task ServiceUnavailableIs503WithoutRetryAfter()
    {
        var outcome = await RunAsync(() => throw new ServiceUnavailableException("Dịch vụ lưu trữ tệp hiện không khả dụng."));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, outcome.Status);
        Assert.Null(outcome.RetryAfter);
        Assert.Equal(ServiceUnavailableException.DefaultCode, outcome.Code);
    }

    [Fact]
    public async Task KnowledgeStateRuleKeeps422WithMessage()
    {
        var article = Article.CreateDraft(Guid.NewGuid(), "Valid Title", "Valid Content at least 10 chars", null, DateTime.UtcNow);

        var outcome = await RunAsync(() => { article.Archive(DateTime.UtcNow); return null; });

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, outcome.Status);
        Assert.Equal("article_archive_requires_published", outcome.Code);
        Assert.Equal("Chỉ có thể lưu trữ bài viết đang ở trạng thái đã xuất bản.", outcome.Error);
    }

    [Fact]
    public async Task LibraryInvalidOperationIsNotMappedAndStaysAServerError()
    {
        // Không thuộc DomainException nên không được biến thành 4xx kèm thông điệp: để khung xử lý trả 500 chung.
        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(() => new List<int>().First()));
    }
}
