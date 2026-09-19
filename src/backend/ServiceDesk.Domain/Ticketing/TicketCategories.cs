namespace ServiceDesk.Domain.Ticketing;

public static class TicketCategories
{
    public const string ACC = "ACC";       // Account/Access
    public const string NET = "NET";       // Network
    public const string VPN = "VPN";       // VPN
    public const string MAIL = "MAIL";     // Email
    public const string SW = "SW";         // Software
    public const string HW = "HW";         // Hardware
    public const string PRINT = "PRINT";   // Printer
    public const string ACCESS = "ACCESS"; // Physical Access
    public const string EQUIP = "EQUIP";   // Equipment
    public const string SEC = "SEC";       // Security
    public const string OTHER = "OTHER";   // Other

    public static readonly string[] All = { ACC, NET, VPN, MAIL, SW, HW, PRINT, ACCESS, EQUIP, SEC, OTHER };
}
