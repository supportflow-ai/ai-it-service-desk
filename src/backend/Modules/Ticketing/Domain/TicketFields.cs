using ServiceDesk.SharedKernel.Application;

namespace ServiceDesk.Modules.Ticketing.Domain;

/// <summary>
/// Tiêu đề phiếu đã chuẩn hóa (cắt khoảng trắng hai đầu) và hợp lệ: không rỗng, tối đa <see cref="MaxLength"/> ký tự.
/// Giá trị chỉ dùng bên trong Domain để <see cref="Ticket.Create"/> và <see cref="Ticket.Update"/> chung một quy tắc;
/// chữ ký công khai của <see cref="Ticket"/> vẫn nhận <see cref="string"/> và cột dữ liệu không đổi.
/// </summary>
internal readonly record struct TicketTitle
{
    public const int MaxLength = 100;

    private TicketTitle(string value) => Value = value;

    public string Value { get; }

    /// <exception cref="InvalidInputException">Rỗng hoặc dài quá <see cref="MaxLength"/> ký tự.</exception>
    public static TicketTitle Parse(string? raw)
    {
        var value = raw?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidInputException("ticket_title_required", "Tiêu đề không được để trống.");
        if (value.Length > MaxLength)
            throw new InvalidInputException("ticket_title_too_long", $"Tiêu đề không được vượt quá {MaxLength} ký tự.");
        return new TicketTitle(value);
    }
}

/// <summary>
/// Mô tả phiếu đã chuẩn hóa (cắt khoảng trắng hai đầu) và hợp lệ: không rỗng, tối thiểu <see cref="MinLength"/> ký tự.
/// Không có giới hạn trên (theo hợp đồng hiện hành); chỉ dùng bên trong Domain.
/// </summary>
internal readonly record struct TicketDescription
{
    public const int MinLength = 10;

    private TicketDescription(string value) => Value = value;

    public string Value { get; }

    /// <exception cref="InvalidInputException">Rỗng hoặc ngắn hơn <see cref="MinLength"/> ký tự.</exception>
    public static TicketDescription Parse(string? raw)
    {
        var value = raw?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidInputException("ticket_description_required", "Mô tả không được để trống.");
        if (value.Length < MinLength)
            throw new InvalidInputException("ticket_description_too_short", $"Mô tả phải có ít nhất {MinLength} ký tự.");
        return new TicketDescription(value);
    }
}

/// <summary>
/// Mã danh mục sự cố hợp lệ, so khớp chính xác (phân biệt hoa thường) với <see cref="TicketCategories.All"/>
/// vốn lấy từ <see cref="TicketCategory"/>; không cắt khoảng trắng để giữ đúng hành vi cũ. Chỉ dùng bên trong Domain.
/// </summary>
internal readonly record struct TicketCategoryCode
{
    private TicketCategoryCode(string value) => Value = value;

    public string Value { get; }

    /// <exception cref="InvalidInputException">Không thuộc danh mục hỗ trợ.</exception>
    public static TicketCategoryCode Parse(string? raw)
    {
        if (raw is null || !TicketCategories.All.Contains(raw, StringComparer.Ordinal))
            throw new InvalidInputException("ticket_category_invalid", "Danh mục không hợp lệ.");
        return new TicketCategoryCode(raw);
    }
}
