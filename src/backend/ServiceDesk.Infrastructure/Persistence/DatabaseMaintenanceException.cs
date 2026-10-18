namespace ServiceDesk.Infrastructure.Persistence;

/// <summary>Credential-free operation failure code safe for CLI diagnostics.</summary>
public sealed class DatabaseMaintenanceException(string code) : InvalidOperationException(code);
