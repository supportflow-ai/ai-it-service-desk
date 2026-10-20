namespace ServiceDesk.Domain.Common;

/// <summary>
/// Các giá trị mặc định và giới hạn độ dài chuẩn dùng chung trong tầng Domain.
/// </summary>
public static class DomainDefaults
{
    /// <summary>
    /// Độ dài tối đa cho các trường tên chuẩn trong tầng Domain.
    /// </summary>
    public const int NameMaxLength = 256;

    /// <summary>
    /// Độ dài tối đa cho các trường mô tả hoặc nội dung.
    /// </summary>
    public const int DescriptionMaxLength = 4000;
}
