using ServiceDesk.Modules.Ticketing.Application.Idempotency;
using ServiceDesk.SharedKernel.Application;
using Xunit;

namespace ServiceDesk.Application.Tests.SharedKernel;

/// <summary>Hệ lỗi nghiệp vụ dùng chung: mã lỗi ổn định và mã HTTP tương thích cho từng loại.</summary>
public sealed class DomainExceptionTests
{
    public static TheoryData<DomainException, int> StatusByKind => new()
    {
        { new InvalidInputException("bad_input", "x"), 400 },
        { new RuleViolationException("bad_rule", "x"), 400 },
        { new RuleViolationException("bad_rule", "x", RuleViolationException.UnprocessableEntityStatus), 422 },
        { new ForbiddenException("no_access", "x"), 403 },
        { new NotFoundException("missing", "x"), 404 },
        { new ConcurrencyConflictException(), 409 },
        { new IdempotencyConflictException(), 409 },
        { new IdempotencyInProgressException(new TimeoutException()), 503 },
        { new ServiceUnavailableException("x"), 503 }
    };

    [Theory]
    [MemberData(nameof(StatusByKind))]
    public void EachKindKeepsItsCompatibleHttpStatus(DomainException error, int expected) =>
        Assert.Equal(expected, error.StatusCode);

    [Theory]
    [InlineData("a1", true)]
    [InlineData("ticket_not_found", true)]
    [InlineData("a_1_b", true)]
    [InlineData("", false)]
    [InlineData("a", false)]
    [InlineData("Ticket_not_found", false)]
    [InlineData("ticket-not-found", false)]
    [InlineData("1ticket", false)]
    [InlineData("_ticket", false)]
    [InlineData("tiêu_đề", false)]
    public void CodeMustBeStableSnakeCase(string code, bool valid)
    {
        Assert.Equal(valid, DomainException.IsValidCode(code));
        if (valid) Assert.Equal(code, new InvalidInputException(code, "x").Code);
        else Assert.Throws<ArgumentException>(() => new InvalidInputException(code, "x"));
    }

    [Fact]
    public void CodeLongerThan64CharactersIsRejected()
    {
        Assert.True(DomainException.IsValidCode(new string('a', 64)));
        Assert.False(DomainException.IsValidCode(new string('a', 65)));
    }

    [Theory]
    [InlineData(399)]
    [InlineData(500)]
    [InlineData(200)]
    public void RuleViolationRejectsNonClientErrorStatus(int status) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuleViolationException("bad_rule", "x", status));

    [Fact]
    public void ConflictAndRetryLaterCarryStableCodes()
    {
        Assert.Equal(ConcurrencyConflictException.ErrorCode, new ConcurrencyConflictException().Code);
        Assert.Equal("idempotency_key_reused", new IdempotencyConflictException().Code);
        Assert.Equal("idempotency_in_progress", new IdempotencyInProgressException(new TimeoutException()).Code);
        Assert.Equal(ServiceUnavailableException.DefaultCode, new ServiceUnavailableException("x").Code);
    }

    [Fact]
    public void InnerExceptionIsKeptForDiagnostics()
    {
        var inner = new TimeoutException();
        Assert.Same(inner, new IdempotencyInProgressException(inner).InnerException);
    }
}
