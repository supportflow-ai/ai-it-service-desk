using Moq;
using ServiceDesk.Application.Identity.RegisterUser;
using ServiceDesk.Application.Identity.LoginUser;
using ServiceDesk.Application.Identity.Ports;
using ServiceDesk.Application.Identity.Contracts;
using ServiceDesk.Domain.Identity;
using Xunit;
namespace ServiceDesk.Application.Tests.Identity;

public sealed class IdentityUseCaseTests
{
    private readonly Mock<IUserAccountGateway> _accounts = new(MockBehavior.Strict);
    private readonly Mock<IAccessTokenIssuer> _tokens = new(MockBehavior.Strict);
    private static UserInfo User => new() { UserId = Guid.NewGuid().ToString(), Email = "user@test.invalid", Roles = [RoleNames.Requester] };

    [Fact]
    public async Task Registration_DoesNotForwardClientRole_AndPreservesFullName()
    {
        var user = User;
        _accounts.Setup(a => a.CreateRequesterAsync(user.Email, "Test@1234", It.IsAny<CancellationToken>())).ReturnsAsync(new AccountCreation(user, []));
        _tokens.Setup(t => t.GenerateToken(It.Is<UserInfo>(u => u.FullName == "Example" && u.Roles.SequenceEqual(new[] { RoleNames.Requester })))).Returns("token");
        var result = await new RegisterUserUseCase(_accounts.Object, _tokens.Object).ExecuteAsync(new()
            { Email = user.Email, Password = "Test@1234", Role = RoleNames.Admin, FullName = "Example" });
        Assert.True(result.Succeeded); Assert.Equal("Example", result.User!.FullName); Assert.Equal("token", result.Token);
        _accounts.VerifyAll(); _tokens.VerifyAll();
    }

    [Fact]
    public async Task FailedRegistration_DoesNotIssueToken()
    {
        _accounts.Setup(a => a.CreateRequesterAsync("x", "y", It.IsAny<CancellationToken>())).ReturnsAsync(new AccountCreation(null, ["Email is already registered."]));
        var result = await new RegisterUserUseCase(_accounts.Object, _tokens.Object).ExecuteAsync(new() { Email = "x", Password = "y" });
        Assert.False(result.Succeeded); Assert.Equal("Email is already registered.", Assert.Single(result.Errors));
        _tokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FailedOrLockedLogin_DoesNotIssueToken()
    {
        _accounts.Setup(a => a.CheckPasswordAsync("x", "y", It.IsAny<CancellationToken>())).ReturnsAsync((UserInfo?)null);
        var result = await new LoginUserUseCase(_accounts.Object, _tokens.Object).ExecuteAsync(new() { Email = "x", Password = "y" });
        Assert.False(result.Succeeded); Assert.Equal("Invalid email or password.", Assert.Single(result.Errors));
        _tokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ValidLogin_IssuesTokenForGatewayIdentity()
    {
        var user = User;
        _accounts.Setup(a => a.CheckPasswordAsync(user.Email, "password", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _tokens.Setup(t => t.GenerateToken(user)).Returns("token");
        var result = await new LoginUserUseCase(_accounts.Object, _tokens.Object).ExecuteAsync(new() { Email = user.Email, Password = "password" });
        Assert.True(result.Succeeded); Assert.Same(user, result.User); Assert.Equal("token", result.Token);
    }
}
