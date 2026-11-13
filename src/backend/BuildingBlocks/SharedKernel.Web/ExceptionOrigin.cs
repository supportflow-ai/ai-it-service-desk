namespace ServiceDesk.SharedKernel.Web;

/// <summary>
/// Xác định nơi một ngoại lệ được ném ra (kiểu khai báo phương thức ném). Bộ lọc lỗi dùng nó để chỉ trả thông điệp
/// của lỗi nghiệp vụ cho client; lỗi cùng loại đến từ .NET, thư viện hoặc adapter hạ tầng chỉ nhận thông điệp chung.
/// </summary>
public static class ExceptionOrigin
{
    /// <summary>Ngoại lệ được ném trực tiếp từ một assembly ServiceDesk (không phải .NET hay thư viện).</summary>
    public static bool IsRaisedByApplication(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var assembly = exception.TargetSite?.DeclaringType?.Assembly.GetName().Name;
        return assembly is not null && assembly.StartsWith("ServiceDesk.", StringComparison.Ordinal);
    }

    /// <summary>
    /// Ngoại lệ được ném từ một kiểu thuộc namespace <paramref name="namespacePrefix"/> (ví dụ Domain của một module).
    /// Kiểu lồng do trình biên dịch sinh (closure, máy trạng thái async) được quy về kiểu chứa nó.
    /// </summary>
    public static bool IsRaisedIn(Exception exception, string namespacePrefix)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(namespacePrefix);
        var type = exception.TargetSite?.DeclaringType;
        while (type?.DeclaringType is not null) type = type.DeclaringType;
        var ns = type?.Namespace;
        return ns is not null && (ns == namespacePrefix || ns.StartsWith(namespacePrefix + ".", StringComparison.Ordinal));
    }
}
