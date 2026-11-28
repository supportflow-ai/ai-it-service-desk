namespace ServiceDesk.SharedKernel.Application.Abstractions;

public sealed record StorageObjectEntry(string Key, DateTime LastModifiedUtc);
public sealed record StorageObjectPage(IReadOnlyList<StorageObjectEntry> Objects, string? ContinuationToken);
