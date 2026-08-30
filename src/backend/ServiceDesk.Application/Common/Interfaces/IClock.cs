namespace ServiceDesk.Application.Common.Interfaces;

/// <summary>
/// Abstraction over system clock for testability.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Returns the current UTC date/time.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}
