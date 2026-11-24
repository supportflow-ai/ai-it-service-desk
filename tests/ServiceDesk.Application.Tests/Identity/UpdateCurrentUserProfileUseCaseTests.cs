using Moq;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Application.UpdateCurrentUserProfile;
using ServiceDesk.SharedKernel.Application.Abstractions;
using Xunit;

namespace ServiceDesk.Application.Tests.Identity;

public sealed class UpdateCurrentUserProfileUseCaseTests
{
    private readonly Mock<IUserAccountGateway> _accounts = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Guid _userId = Guid.NewGuid();

    public UpdateCurrentUserProfileUseCaseTests()
    {
        _currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(u => u.UserId).Returns(_userId.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_ValidInputs_UpdatesAndReturnsProfile()
    {
        var expectedUser = new UserInfo
        {
            UserId = _userId.ToString(),
            Email = "test@example.com",
            FullName = "Nguyễn Văn A",
            PhoneNumber = "0912345678",
            Roles = ["Requester"]
        };

        _accounts.Setup(a => a.UpdateProfileAsync(_userId.ToString(), "Nguyễn Văn A", "0912345678", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUser);

        var useCase = new UpdateCurrentUserProfileUseCase(_accounts.Object, _currentUser.Object);
        var result = await useCase.ExecuteAsync(new UpdateCurrentUserProfileCommand("Nguyễn Văn A", "0912345678"));

        Assert.NotNull(result);
        Assert.Equal("0912345678", result.PhoneNumber);
        Assert.Equal("Nguyễn Văn A", result.FullName);
    }

    [Fact]
    public async Task ExecuteAsync_PhoneTooLong_ThrowsArgumentException()
    {
        var useCase = new UpdateCurrentUserProfileUseCase(_accounts.Object, _currentUser.Object);
        var command = new UpdateCurrentUserProfileCommand("Name", new string('1', 25));

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task ExecuteAsync_NameTooLong_ThrowsArgumentException()
    {
        var useCase = new UpdateCurrentUserProfileUseCase(_accounts.Object, _currentUser.Object);
        var command = new UpdateCurrentUserProfileCommand(new string('A', 101), "0912345678");

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(command));
    }
}
