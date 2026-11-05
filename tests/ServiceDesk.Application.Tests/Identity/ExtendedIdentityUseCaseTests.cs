using System.Security.Claims;
using Moq;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Identity.AssignUserRoles;
using ServiceDesk.Application.Identity.ChangePassword;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Application.Identity.ForgotPassword;
using ServiceDesk.Application.Identity.GetDepartments;
using ServiceDesk.Application.Identity.Ports;
using ServiceDesk.Application.Identity.RefreshAuth;
using ServiceDesk.Application.Identity.ResetPassword;
using ServiceDesk.Application.Identity.RevokeToken;
using ServiceDesk.Domain.Identity;
using Xunit;

namespace ServiceDesk.Application.Tests.Identity;

public sealed class ExtendedIdentityUseCaseTests
{
    private readonly Mock<IRefreshTokenStore> _refreshTokens = new();
    private readonly Mock<IOtpCodeStore> _otpStore = new();
    private readonly Mock<IDepartmentQueries> _departmentQueries = new();
    private readonly Mock<IUserAccountGateway> _accounts = new();
    private readonly Mock<IAccessTokenIssuer> _tokens = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IClock> _clock = new();
    private readonly DateTimeOffset _now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    public ExtendedIdentityUseCaseTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    [Fact]
    public async Task RefreshAuth_WithValidToken_RotatesAndReturnsNewTokens()
    {
        var userId = Guid.NewGuid().ToString();
        var user = new UserInfo { UserId = userId, Email = "user@test.com", Roles = [RoleNames.Requester] };
        var activeToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "oldhash",
            ExpiresAt = _now.UtcDateTime.AddDays(1)
        };

        _refreshTokens.Setup(r => r.FindActiveByHashAsync(It.IsAny<string>(), _now.UtcDateTime, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeToken);
        _accounts.Setup(a => a.FindByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _tokens.Setup(t => t.GenerateToken(user)).Returns("new-jwt-token");

        var useCase = new RefreshAuthUseCase(_refreshTokens.Object, _accounts.Object, _tokens.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new RefreshAuthRequest("raw-refresh-token"));

        Assert.True(result.Succeeded);
        Assert.Equal("new-jwt-token", result.Token);
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        _refreshTokens.Verify(r => r.RevokeTokenAsync(It.IsAny<string>(), _now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokens.Verify(r => r.SaveTokenAsync(It.Is<RefreshToken>(t => t.UserId == userId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshAuth_WithInvalidToken_ReturnsFailed()
    {
        _refreshTokens.Setup(r => r.FindActiveByHashAsync(It.IsAny<string>(), _now.UtcDateTime, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        var useCase = new RefreshAuthUseCase(_refreshTokens.Object, _accounts.Object, _tokens.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new RefreshAuthRequest("invalid-token"));

        Assert.False(result.Succeeded);
        Assert.Equal("Invalid or expired refresh token.", Assert.Single(result.Errors));
    }

    [Fact]
    public async Task RevokeToken_CallsStoreWithHashedToken()
    {
        var useCase = new RevokeTokenUseCase(_refreshTokens.Object, _clock.Object);
        await useCase.ExecuteAsync(new RevokeTokenRequest("token-to-revoke"));

        _refreshTokens.Verify(r => r.RevokeTokenAsync(It.IsAny<string>(), _now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDepartments_ReturnsListFromQueries()
    {
        var expected = new List<DepartmentDto>
        {
            new(Guid.NewGuid(), "IT", "Information Technology", "IT Dept"),
            new(Guid.NewGuid(), "HR", "Human Resources", "HR Dept")
        };
        _departmentQueries.Setup(q => q.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var useCase = new GetDepartmentsUseCase(_departmentQueries.Object);
        var result = await useCase.ExecuteAsync(new GetDepartmentsQuery());

        Assert.Equal(2, result.Count);
        Assert.Equal("IT", result[0].Code);
    }

    [Fact]
    public async Task ChangePassword_WhenAuthenticated_CallsAccountGateway()
    {
        var userId = Guid.NewGuid();
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(userId.ToString());
        _accounts.Setup(a => a.ChangePasswordAsync(userId.ToString(), "oldpass", "newpass", It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Success());

        var useCase = new ChangePasswordUseCase(_accounts.Object, _currentUser.Object);
        var result = await useCase.ExecuteAsync(new ChangePasswordRequest("oldpass", "newpass"));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ForgotPassword_GeneratesAndSavesOtpCode()
    {
        var useCase = new ForgotPasswordUseCase(_otpStore.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new ForgotPasswordRequest("user@test.com"));

        Assert.True(result.Succeeded);
        _otpStore.Verify(s => s.SaveOtpAsync(It.Is<OtpCode>(o => o.Target == "user@test.com" && o.Purpose == "ResetPassword"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_WithValidOtp_ResetsPasswordAndRevokesTokens()
    {
        var email = "user@test.com";
        var validOtp = new OtpCode
        {
            Id = Guid.NewGuid(),
            Target = email,
            Purpose = "ResetPassword",
            ExpiresAt = _now.UtcDateTime.AddMinutes(5)
        };
        var user = new UserInfo { UserId = Guid.NewGuid().ToString(), Email = email, Roles = [RoleNames.Requester] };

        _otpStore.Setup(s => s.FindValidOtpAsync(email, "ResetPassword", It.IsAny<string>(), _now.UtcDateTime, It.IsAny<CancellationToken>()))
            .ReturnsAsync(validOtp);
        _accounts.Setup(a => a.ResetPasswordWithOtpAsync(email, "newpass", It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Success());
        _accounts.Setup(a => a.CheckPasswordAsync(email, "newpass", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var useCase = new ResetPasswordUseCase(_otpStore.Object, _accounts.Object, _refreshTokens.Object, _clock.Object);
        var result = await useCase.ExecuteAsync(new ResetPasswordRequest(email, "123456", "newpass"));

        Assert.True(result.Succeeded);
        _otpStore.Verify(s => s.MarkUsedAsync(validOtp.Id, It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokens.Verify(r => r.RevokeAllForUserAsync(user.UserId, _now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignUserRoles_WhenNotAdmin_ThrowsUnauthorizedAccessException()
    {
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var useCase = new AssignUserRolesUseCase(_accounts.Object, _currentUser.Object);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.ExecuteAsync(Guid.NewGuid(), new AssignRolesRequest([RoleNames.Agent])));
    }

    [Fact]
    public async Task AssignUserRoles_WhenAdminWithValidRoles_CallsGateway()
    {
        var targetId = Guid.NewGuid();
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(true);
        _accounts.Setup(a => a.AssignRolesAsync(targetId.ToString(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Success());

        var useCase = new AssignUserRolesUseCase(_accounts.Object, _currentUser.Object);
        var result = await useCase.ExecuteAsync(targetId, new AssignRolesRequest([RoleNames.Agent, RoleNames.Requester]));

        Assert.True(result.Succeeded);
        _accounts.Verify(a => a.AssignRolesAsync(targetId.ToString(), It.Is<IReadOnlyList<string>>(r => r.Contains(RoleNames.Agent)), It.IsAny<CancellationToken>()), Times.Once);
    }
}
