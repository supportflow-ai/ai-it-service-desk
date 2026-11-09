namespace ServiceDesk.SharedKernel.Application.Abstractions;

/// <summary>
/// Danh bạ người dùng do module Identity công bố cho các module khác. Module khác tra cứu vai trò và tên hiển thị
/// qua cổng này thay vì đọc thẳng bảng ASP.NET Core Identity.
/// </summary>
public interface IUserDirectory
{
    /// <summary>Người dùng tồn tại và có ít nhất một vai trò trong <paramref name="roleNames"/>.</summary>
    Task<bool> IsInAnyRoleAsync(Guid userId, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default);

    /// <summary>Tên hiển thị (claim tên) của người dùng; <c>null</c> khi người dùng không tồn tại hoặc chưa có tên.</summary>
    Task<string?> FindDisplayNameAsync(Guid userId, CancellationToken cancellationToken = default);
}
