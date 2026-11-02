namespace ServiceDesk.Application.Common.Interfaces;

/// <summary>Metadata tin cậy do object storage trả về.</summary>
public sealed record ObjectStorageObjectInfo(long Size, string ContentType);
