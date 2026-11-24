using Moq;
using ServiceDesk.Modules.Identity.Application.Departments;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Domain;
using ServiceDesk.SharedKernel.Application.Abstractions;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.Application.Tests.Identity;

public class DepartmentUseCaseTests
{
    private readonly Mock<IDepartmentStore> _store = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IClock> _clock = new();

    public DepartmentUseCaseTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task CreateDepartment_WhenNonAdmin_ThrowsUnauthorizedAccessException()
    {
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(false);

        var useCase = new CreateDepartmentUseCase(_store.Object, _currentUser.Object, _clock.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            useCase.ExecuteAsync(new("HR", "Human Resources", null)));
    }

    [Fact]
    public async Task CreateDepartment_WhenCodeExists_ThrowsDepartmentConflictException()
    {
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(true);
        _store.Setup(s => s.ExistsCodeAsync("HR", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var useCase = new CreateDepartmentUseCase(_store.Object, _currentUser.Object, _clock.Object);

        await Assert.ThrowsAsync<DepartmentConflictException>(() =>
            useCase.ExecuteAsync(new("HR", "Human Resources", null)));
    }

    [Fact]
    public async Task CreateDepartment_WhenValid_ReturnsCreatedDepartment()
    {
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(true);
        _store.Setup(s => s.ExistsCodeAsync("FIN", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _store.Setup(s => s.AddAsync(It.IsAny<Department>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var useCase = new CreateDepartmentUseCase(_store.Object, _currentUser.Object, _clock.Object);

        var result = await useCase.ExecuteAsync(new("FIN", "Finance & Accounting", "Manages finances"));

        Assert.Equal("FIN", result.Code);
        Assert.Equal("Finance & Accounting", result.Name);
        Assert.Equal("Manages finances", result.Description);
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task UpdateDepartment_WhenNotFound_ThrowsKeyNotFoundException()
    {
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(true);
        _store.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Department?)null);

        var useCase = new UpdateDepartmentUseCase(_store.Object, _currentUser.Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            useCase.ExecuteAsync(new(Guid.NewGuid(), "New Name", null)));
    }

    [Fact]
    public async Task UpdateDepartment_WhenValid_UpdatesAndReturnsDto()
    {
        _currentUser.Setup(u => u.IsInRole(RoleNames.Admin)).Returns(true);
        var dept = Department.Create("IT", "Information Tech", "Old Desc", DateTime.UtcNow);

        _store.Setup(s => s.GetByIdAsync(dept.Id, It.IsAny<CancellationToken>())).ReturnsAsync(dept);
        _store.Setup(s => s.UpdateAsync(dept, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var useCase = new UpdateDepartmentUseCase(_store.Object, _currentUser.Object);

        var result = await useCase.ExecuteAsync(new(dept.Id, "IT Services", "Updated Description"));

        Assert.Equal("IT Services", result.Name);
        Assert.Equal("Updated Description", result.Description);
    }
}
