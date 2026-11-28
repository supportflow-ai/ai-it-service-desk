using System.Security.Claims;
using Microsoft.Extensions.Options;
using ServiceDesk.Modules.Identity.Infrastructure;
using ServiceDesk.Modules.Identity.Infrastructure.Options;
using Moq;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Identity.Application.AssignUserRoles;
using ServiceDesk.Modules.Identity.Application.ChangePassword;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.ForgotPassword;
using ServiceDesk.Modules.Identity.Application.GetDepartments;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Application.RefreshAuth;
using ServiceDesk.Modules.Identity.Application.ResetPassword;
using ServiceDesk.Modules.Identity.Application.RevokeToken;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Identity.Domain;
using Xunit;

namespace ServiceDesk.Application.Tests.Identity;

public sealed class ExtendedIdentityUseCaseTests
{
    private readonly Mock<IRefreshTokenStore> _refreshTokens = new();
    private readonly Mock<IPasswordResetJobStore> _jobs = new();
    private readonly Mock<IDepartmentQueries> _departmentQueries = new();
    private readonly Mock<IUserAccountGateway> _accounts = new();
    private readonly Mock<IAccessTokenIssuer> _tokens = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Mock<IPasswordResetGateway> _passwordReset = new();
    private readonly Mock<IOtpSender> _otpSender = new();
    private readonly IOtpCodeProtector _protector = new OtpCodeProtector(Options.Create(new OtpProtectionOptions
    {
        Key = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray())
    }));
    private readonly DateTimeOffset _now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    public ExtendedIdentityUseCaseTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _resetQueue.Setup(q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private RefreshToken StoredToken(string userId, DateTime? revokedAt = null, int expiresInDays = 1)
    {
        // Phát token sao cho thời điểm hết hạn là _now + expiresInDays.
        var token = RefreshToken.Issue(userId, "oldhash", _now.UtcDateTime.AddDays(expiresInDays) - RefreshToken.Lifetime);
        if (revokedAt is not null)
            token.Revoke(revokedAt.Value);
        return token;
    }

    private RefreshAuthUseCase Refresh() => new(_refreshTokens.Object, _accounts.Object, _tokens.Object, _clock.Object);

    [Fact]
    public async Task RefreshAuth_WithValidToken_RotatesAndReturnsNewTokens()
    {
        var userId = Guid.NewGuid().ToString();
        var user = new UserInfo { UserId = userId, Email = "user@test.com", Roles = [RoleNames.Requester] };
        var stored = StoredToken(userId);
        _refreshTokens.Setup(r => r.FindByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        _refreshTokens.Setup(r => r.TryRotateAsync(stored.Id, It.IsAny<RefreshToken>(), _now.UtcDateTime, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _accounts.Setup(a => a.FindByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _tokens.Setup(t => t.GenerateToken(user)).Returns("new-jwt-token");

        var result = await Refresh().ExecuteAsync(new RefreshAuthRequest("raw-refresh-token"));

        Assert.True(result.Succeeded);
        Assert.Equal("new-jwt-token", result.Token);
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        _refreshTokens.Verify(r => r.TryRotateAsync(stored.Id, It.Is<RefreshToken>(t => t.UserId == userId && t.FamilyId == stored.FamilyId &&
            t.Id != stored.Id && t.ExpiresAt == _now.UtcDateTime.AddDays(7)), _now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokens.Verify(r => r.RevokeFamilyAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAuth_WithUnknownToken_ReturnsFailed()
    {
        _refreshTokens.Setup(r => r.FindByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((RefreshToken?)null);

        var result = await Refresh().ExecuteAsync(new RefreshAuthRequest("invalid-token"));

        Assert.False(result.Succeeded);
        Assert.Equal("Invalid or expired refresh token.", Assert.Single(result.Errors));
    }

    [Fact]
    public async Task RefreshAuth_WithRevokedToken_TreatsAsReuseAndRevokesItsSessionFamily()
    {
        var userId = Guid.NewGuid().ToString();
        var replayed = StoredToken(userId, revokedAt: _now.UtcDateTime.AddMinutes(-1));
        _refreshTokens.Setup(r => r.FindByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(replayed);

        var result = await Refresh().ExecuteAsync(new RefreshAuthRequest("replayed-token"));

        Assert.False(result.Succeeded);
        _refreshTokens.Verify(r => r.RevokeFamilyAsync(userId, replayed.FamilyId, _now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokens.Verify(r => r.TryRotateAsync(It.IsAny<Guid>(), It.IsAny<RefreshToken>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAuth_WhenConcurrentRequestRevokedFirst_IssuesNothing()
    {
        var userId = Guid.NewGuid().ToString();
        var stored = StoredToken(userId);
        _refreshTokens.Setup(r => r.FindByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        _refreshTokens.Setup(r => r.TryRotateAsync(stored.Id, It.IsAny<RefreshToken>(), _now.UtcDateTime, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _accounts.Setup(a => a.FindByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserInfo { UserId = userId, Email = "user@test.com", Roles = [RoleNames.Requester] });

        var result = await Refresh().ExecuteAsync(new RefreshAuthRequest("raced-token"));

        Assert.False(result.Succeeded);
        _refreshTokens.Verify(r => r.RevokeFamilyAsync(userId, stored.FamilyId, _now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshAuth_WithExpiredToken_ReturnsFailedWithoutRotation()
    {
        var userId = Guid.NewGuid().ToString();
        _refreshTokens.Setup(r => r.FindByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StoredToken(userId, expiresInDays: -1));

        var result = await Refresh().ExecuteAsync(new RefreshAuthRequest("expired-token"));

        Assert.False(result.Succeeded);
        _refreshTokens.Verify(r => r.TryRotateAsync(It.IsAny<Guid>(), It.IsAny<RefreshToken>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RevokeToken_CallsStoreWithHashedToken()
    {
        var useCase = new RevokeTokenUseCase(_refreshTokens.Object, _clock.Object);
        await useCase.ExecuteAsync(new RevokeTokenRequest("token-to-revoke"));

        _refreshTokens.Verify(r => r.RevokeSessionByTokenAsync(It.IsAny<string>(), _now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDepartments_ReturnsListFromQueries()
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid().ToString());
        var expected = new List<DepartmentDto>
        {
            new(Guid.NewGuid(), "IT", "Information Technology", "IT Dept"),
            new(Guid.NewGuid(), "HR", "Human Resources", "HR Dept")
        };
        _departmentQueries.Setup(q => q.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var useCase = new GetDepartmentsUseCase(_departmentQueries.Object, _currentUser.Object);
        var result = await useCase.ExecuteAsync(new GetDepartmentsQuery());

        Assert.Equal(2, result.Count);
        Assert.Equal("IT", result[0].Code);
    }

    private ChangePasswordUseCase ChangePassword() => new(_accounts.Object, _currentUser.Object, _clock.Object);

    [Fact]
    public async Task ChangePassword_PassesClockTimeSoGatewayRevokesSessionsInSameTransaction()
    {
        var userId = Guid.NewGuid();
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(userId.ToString());
        _accounts.Setup(a => a.ChangePasswordAsync(userId.ToString(), "oldpass", "newpass", _now.UtcDateTime, It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Success());

        var result = await ChangePassword().ExecuteAsync(new ChangePasswordRequest("oldpass", "newpass"));

        Assert.True(result.Succeeded);
        _accounts.Verify(a => a.ChangePasswordAsync(userId.ToString(), "oldpass", "newpass", _now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePassword_WhenGatewayFails_ReturnsItsErrors()
    {
        var userId = Guid.NewGuid();
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(userId.ToString());
        _accounts.Setup(a => a.ChangePasswordAsync(userId.ToString(), "wrong", "newpass", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Failed("Incorrect password."));

        var result = await ChangePassword().ExecuteAsync(new ChangePasswordRequest("wrong", "newpass"));

        Assert.False(result.Succeeded);
        Assert.Equal("Incorrect password.", Assert.Single(result.Errors));
    }

    private readonly Mock<IPasswordResetQueue> _resetQueue = new();

    private ForgotPasswordUseCase ForgotPassword() => new(_resetQueue.Object);

    private PasswordResetCodeIssuer Issuer() => new(_accounts.Object, _jobs.Object, _otpSender.Object, _clock.Object, _protector);

    [Theory]
    [InlineData(" User@Test.com ")]
    [InlineData("nobody@test.com")]
    public async Task ForgotPassword_QueuesNormalizedEmailAndAlwaysSucceeds(string email)
    {
        var result = await ForgotPassword().ExecuteAsync(new ForgotPasswordRequest(email));

        Assert.True(result.Succeeded);
        _resetQueue.Verify(q => q.EnqueueAsync(email.Trim().ToLowerInvariant(), It.IsAny<CancellationToken>()), Times.Once);
        _accounts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ForgotPassword_WithoutEmail_FailsWithoutQueueing()
    {
        var result = await ForgotPassword().ExecuteAsync(new ForgotPasswordRequest(" "));

        Assert.False(result.Succeeded);
        _resetQueue.Verify(q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetCodeIssuer_ForKnownEmail_IssuesHashedCodeAndSendsIt()
    {
        _accounts.Setup(a => a.EmailExistsAsync("user@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _jobs.Setup(s => s.PrepareAsync(It.IsAny<PasswordResetJob>(), It.IsAny<OtpCode>(), It.IsAny<string>(), _now.UtcDateTime, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PasswordResetJob job, OtpCode otp, string cipher, DateTime now, CancellationToken token) => { job.Prepare(otp, cipher); return job; });
        _jobs.Setup(s => s.RenewForSendAsync(It.IsAny<PasswordResetJob>(), _now.UtcDateTime, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        string? sentCode = null;
        _otpSender.Setup(s => s.SendPasswordResetCodeAsync("user@test.com", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, DateTime, CancellationToken>((_, code, _, _) => sentCode = code).Returns(Task.CompletedTask);

        await Issuer().ProcessAsync(PasswordResetJob.Request("user@test.com", _now.UtcDateTime));

        Assert.NotNull(sentCode);
        Assert.Matches("^[0-9]{6}$", sentCode);
        _jobs.Verify(s => s.PrepareAsync(It.IsAny<PasswordResetJob>(), It.Is<OtpCode>(o => o.Target == "user@test.com" && o.Purpose == OtpCode.ResetPasswordPurpose &&
            o.CodeHash.StartsWith("v1:") && o.CodeHash != sentCode && o.ExpiresAt == _now.UtcDateTime.AddMinutes(15) && o.FailedAttempts == 0),
            It.Is<string>(c => c.StartsWith("v1:") && c != sentCode), _now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetCodeIssuer_ForUnknownEmail_IssuesNothing()
    {
        _accounts.Setup(a => a.EmailExistsAsync("nobody@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await Issuer().ProcessAsync(PasswordResetJob.Request("nobody@test.com", _now.UtcDateTime));
        _jobs.Verify(s => s.PrepareAsync(It.IsAny<PasswordResetJob>(), It.IsAny<OtpCode>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _otpSender.Verify(s => s.SendPasswordResetCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetCodeIssuer_OverHourlyLimit_SendsNothing()
    {
        _accounts.Setup(a => a.EmailExistsAsync("user@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _jobs.Setup(s => s.PrepareAsync(It.IsAny<PasswordResetJob>(), It.IsAny<OtpCode>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PasswordResetJob?)null);
        await Issuer().ProcessAsync(PasswordResetJob.Request("user@test.com", _now.UtcDateTime));
        _otpSender.Verify(s => s.SendPasswordResetCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetPassword_DelegatesNormalizedEmailAndHashedCodeToGateway()
    {
        _passwordReset.Setup(g => g.ResetWithOtpAsync("user@test.com", It.IsAny<string>(), "NewPass123", _now.UtcDateTime, It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Success());

        var useCase = new ResetPasswordUseCase(_passwordReset.Object, _clock.Object, _protector);
        var result = await useCase.ExecuteAsync(new ResetPasswordRequest(" User@Test.com ", " 123456 ", "NewPass123"));

        Assert.True(result.Succeeded);
        _passwordReset.Verify(g => g.ResetWithOtpAsync("user@test.com", It.Is<string>(h => h != "123456" && h.StartsWith("v1:") && h.Length == 67), "NewPass123",
            _now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_WithMissingFields_DoesNotCallGateway()
    {
        var useCase = new ResetPasswordUseCase(_passwordReset.Object, _clock.Object, _protector);
        var result = await useCase.ExecuteAsync(new ResetPasswordRequest("user@test.com", "", "NewPass123"));

        Assert.False(result.Succeeded);
        _passwordReset.Verify(g => g.ResetWithOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private AssignUserRolesUseCase AssignRoles() => new(_accounts.Object, _currentUser.Object, _clock.Object);

    [Fact]
    public async Task AssignUserRoles_WhenNotAdmin_ThrowsUnauthorizedAccessException()
    {
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            AssignRoles().ExecuteAsync(Guid.NewGuid(), new AssignRolesRequest([RoleNames.Agent])));
    }

    [Fact]
    public async Task AssignUserRoles_WhenAdminWithValidRoles_CallsGatewayWithClockTime()
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid().ToString());
        var targetId = Guid.NewGuid();
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(true);
        _accounts.Setup(a => a.AssignRolesAsync(targetId.ToString(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Success());

        var result = await AssignRoles().ExecuteAsync(targetId, new AssignRolesRequest([RoleNames.Agent, RoleNames.Requester]));

        Assert.True(result.Succeeded);
        _accounts.Verify(a => a.AssignRolesAsync(targetId.ToString(), It.Is<IReadOnlyList<string>>(r => r.Contains(RoleNames.Agent)),
            _now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignUserRoles_WhenGatewayRejects_ReturnsItsError()
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid().ToString());
        var targetId = Guid.NewGuid();
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(true);
        _accounts.Setup(a => a.AssignRolesAsync(targetId.ToString(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Failed("Không thể gỡ vai trò Admin của quản trị viên cuối cùng."));

        var result = await AssignRoles().ExecuteAsync(targetId, new AssignRolesRequest([RoleNames.Requester]));

        Assert.False(result.Succeeded);
        Assert.Equal("Không thể gỡ vai trò Admin của quản trị viên cuối cùng.", Assert.Single(result.Errors));
    }
}
