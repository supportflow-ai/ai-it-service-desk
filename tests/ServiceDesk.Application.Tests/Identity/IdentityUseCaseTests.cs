using Moq;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.Modules.Identity.Application.RegisterUser;
using ServiceDesk.Modules.Identity.Application.LoginUser;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.SharedKernel.Authorization;
using ServiceDesk.Modules.Identity.Domain;
using Xunit;

namespace ServiceDesk.Application.Tests.Identity;

public sealed class IdentityUseCaseTests
{
    private readonly Mock<IUserAccountGateway> _accounts = new(MockBehavior.Strict);
    private readonly Mock<IAccessTokenIssuer> _tokens = new(MockBehavior.Strict);
    private readonly Mock<IRefreshTokenStore> _refreshTokens = new();
    private readonly Mock<IClock> _clock = new();

    public IdentityUseCaseTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero));
    }

    private static UserInfo User => new() { UserId = Guid.NewGuid().ToString(), Email = "user@test.invalid", Roles = [RoleNames.Requester] };

    [Fact]
    public async Task Registration_DoesNotForwardClientRole_AndPreservesFullName()
    {
        var user = User;
        _accounts.Setup(a => a.CreateRequesterAsync(user.Email, "Test@1234", "Example", It.IsAny<CancellationToken>())).ReturnsAsync(new AccountCreation(user, []));
        _tokens.Setup(t => t.GenerateToken(It.Is<UserInfo>(u => u.FullName == "Example" && u.Roles.SequenceEqual(new[] { RoleNames.Requester })))).Returns("token");
        var result = await new RegisterUserUseCase(_accounts.Object, _tokens.Object).ExecuteAsync(new()
            { Email = user.Email, Password = "Test@1234", Role = RoleNames.Admin, FullName = "Example" });
        Assert.True(result.Succeeded); Assert.Equal("Example", result.User!.FullName); Assert.Equal("token", result.Token);
        _accounts.VerifyAll(); _tokens.VerifyAll();
    }

    [Fact]
    public async Task FailedRegistration_DoesNotIssueToken()
    {
        _accounts.Setup(a => a.CreateRequesterAsync("x", "y", null, It.IsAny<CancellationToken>())).ReturnsAsync(new AccountCreation(null, ["Email is already registered."]));
        var result = await new RegisterUserUseCase(_accounts.Object, _tokens.Object).ExecuteAsync(new() { Email = "x", Password = "y" });
        Assert.False(result.Succeeded); Assert.Equal("Email is already registered.", Assert.Single(result.Errors));
        _tokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FailedOrLockedLogin_DoesNotIssueToken()
    {
        _accounts.Setup(a => a.SignInAsync("x", "y", It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync((PasswordSignIn?)null);
        var result = await new LoginUserUseCase(_accounts.Object, _tokens.Object, _clock.Object).ExecuteAsync(new() { Email = "x", Password = "y" });
        Assert.False(result.Succeeded); Assert.Equal("Invalid email or password.", Assert.Single(result.Errors));
        _tokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ValidLogin_IssuesTokenAndRefreshTokenForGatewayIdentity()
    {
        var user = User;
        _accounts.Setup(a => a.SignInAsync(user.Email, "password", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordSignIn(user, "raw-refresh"));
        _tokens.Setup(t => t.GenerateToken(user)).Returns("token");
        var result = await new LoginUserUseCase(_accounts.Object, _tokens.Object, _clock.Object).ExecuteAsync(new() { Email = user.Email, Password = "password" });
        Assert.True(result.Succeeded); Assert.Same(user, result.User); Assert.Equal("token", result.Token);
        Assert.Equal("raw-refresh", result.RefreshToken);
        _refreshTokens.VerifyNoOtherCalls();
    }
}
