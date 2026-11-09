using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using ServiceDesk.Modules.Knowledge.Domain;
using ServiceDesk.SharedKernel.Web;
using Xunit;

namespace ServiceDesk.IntegrationTests;

/// <summary>
/// Bộ lọc lỗi giữ mã HTTP cũ nhưng chỉ trả thông điệp của lỗi do mã ServiceDesk ném ra;
/// lỗi cùng loại từ .NET hoặc thư viện trả thông điệp chung.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ApiErrorFilterTests
{
    private static async Task<(int? Status, string? Error)> RunAsync(Func<object?> endpoint)
    {
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext());
        var result = await new ApiErrorFilter().InvokeAsync(context, _ => ValueTask.FromResult(endpoint()));
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode;
        var value = (result as IValueHttpResult)?.Value;
        if (value is null) return (status, null);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return (status, json.RootElement.GetProperty("error").GetString());
    }

    private static object? ThrowRule() => throw new ArgumentException("Tiêu đề không được để trống.");

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
        var (status, error) = await RunAsync(ThrowRule);
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal("Tiêu đề không được để trống.", error);
    }

    [Fact]
    public async Task LibraryArgumentErrorKeepsStatusButHidesMessage()
    {
        var (status, error) = await RunAsync(ThrowLibraryArgument);
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal(ApiErrorFilter.GenericBadRequestMessage, error);
    }

    [Fact]
    public async Task FrameworkGuardMessageIsHidden()
    {
        var (status, error) = await RunAsync(ThrowGuard);
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal(ApiErrorFilter.GenericBadRequestMessage, error);
    }

    [Fact]
    public async Task ApplicationAccessDeniedMessageIsReturnedWith403()
    {
        var (status, error) = await RunAsync(() => throw new UnauthorizedAccessException("Chỉ quản trị viên mới được làm."));
        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal("Chỉ quản trị viên mới được làm.", error);
    }

    [Fact]
    public async Task MissingResourceIs404WithoutBody()
    {
        var (status, error) = await RunAsync(() => throw new KeyNotFoundException("chi tiết nội bộ"));
        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Null(error);
    }

    [Fact]
    public void DomainRuleIsAttributedToItsModuleDomain()
    {
        var article = Article.CreateDraft(Guid.NewGuid(), "Valid Title", "Valid Content at least 10 chars", null, DateTime.UtcNow);
        var rule = Assert.Throws<InvalidOperationException>(() => article.Archive(DateTime.UtcNow));

        Assert.True(ExceptionOrigin.IsRaisedIn(rule, "ServiceDesk.Modules.Knowledge.Domain"));
        Assert.False(ExceptionOrigin.IsRaisedIn(rule, "ServiceDesk.Modules.Knowledge.Infrastructure"));
    }

    [Fact]
    public void LibraryInvalidOperationIsNotAttributedToTheDomain()
    {
        var library = Assert.Throws<InvalidOperationException>(() => new List<int>().First());

        Assert.False(ExceptionOrigin.IsRaisedIn(library, "ServiceDesk.Modules.Knowledge.Domain"));
        Assert.False(ExceptionOrigin.IsRaisedByApplication(library));
    }
}
