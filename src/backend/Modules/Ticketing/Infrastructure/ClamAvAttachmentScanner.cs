using System.Buffers.Binary;
using System.IO.Compression;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.Ticketing.Infrastructure;

internal sealed class ClamAvAttachmentScanner(IObjectStorageService storage, IOptions<AttachmentScanningOptions> options)
    : IAttachmentScanner, IDisposable
{
    private readonly SemaphoreSlim _slots = new(2);

    public Task ScanAsync(string key, long expectedSize, string contentType, CancellationToken cancellationToken = default) =>
        ProcessAsync(key, expectedSize, contentType, copy: false, cancellationToken);

    public Task<string> PrepareDownloadAsync(TicketAttachmentDto attachment, CancellationToken cancellationToken = default)
    {
        AttachmentPolicy.ValidateMetadata(attachment.FileName, attachment.FileSizeBytes, attachment.ContentType);
        return ProcessAsync(attachment.StorageKey, attachment.FileSizeBytes, attachment.ContentType, copy: true, cancellationToken);
    }

    private async Task<string> ProcessAsync(string key, long expectedSize, string contentType, bool copy, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Host)) throw Unavailable();
        if (!await _slots.WaitAsync(0, cancellationToken)) throw Unavailable();
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            var ct = deadline.Token;
            var bucket = storage.DefaultBucket;
            var source = await storage.StatAsync(bucket, key, ct);
            if (source is null || source.Size != expectedSize || expectedSize <= 0 || expectedSize > AttachmentPolicy.MaxFileSizeBytes ||
                string.IsNullOrWhiteSpace(source.ETag) || !SameType(source.ContentType, contentType)) throw InvalidContent();
            var scannedKey = key;
            if (copy)
            {
                // Bản sao không có upload URL, kể cả khi nguồn là PUT key legacy còn ghi được.
                scannedKey = $"downloads/quarantine/{Guid.NewGuid():N}";
                await storage.CopyAsync(bucket, key, scannedKey, source.ETag, ct);
                source = await storage.StatAsync(bucket, scannedKey, ct);
                if (source is null || source.Size != expectedSize || string.IsNullOrWhiteSpace(source.ETag) || !SameType(source.ContentType, contentType)) throw InvalidContent();
            }
            var bytes = await storage.ReadAsync(bucket, scannedKey, source.ETag, checked((int)AttachmentPolicy.MaxFileSizeBytes), ct);
            if (bytes.LongLength != expectedSize) throw InvalidContent();
            ValidateContent(bytes, contentType);
            await ScanBytesAsync(bytes, ct);
            return scannedKey;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ArgumentException) { throw; }
        catch (Exception) { throw Unavailable(); }
        finally { _slots.Release(); }
    }

    internal async Task ScanBytesAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(options.Value.Host, options.Value.Port, cancellationToken);
        await using var connection = client.GetStream();
        await connection.WriteAsync("zINSTREAM\0"u8.ToArray(), cancellationToken);
        var header = new byte[4];
        for (var offset = 0; offset < bytes.Length; offset += 32768)
        {
            var count = Math.Min(32768, bytes.Length - offset);
            BinaryPrimitives.WriteUInt32BigEndian(header, (uint)count);
            await connection.WriteAsync(header, cancellationToken);
            await connection.WriteAsync(bytes.AsMemory(offset, count), cancellationToken);
        }
        await connection.WriteAsync(new byte[4], cancellationToken);
        var reply = new byte[1024];
        var length = 0;
        while (length < reply.Length)
        {
            var read = await connection.ReadAsync(reply.AsMemory(length, 1), cancellationToken);
            if (read == 0) throw new IOException("Incomplete scanner response.");
            if (reply[length] == 0)
            {
                var verdict = Encoding.ASCII.GetString(reply, 0, length);
                if (verdict == "stream: OK") return;
                if (verdict.StartsWith("stream: ", StringComparison.Ordinal) && verdict.EndsWith(" FOUND", StringComparison.Ordinal))
                    throw InvalidContent();
                throw new IOException("Invalid scanner response.");
            }
            length++;
        }
        throw new IOException("Scanner response exceeded limit.");
    }

    private static bool SameType(string? actual, string declared) =>
        string.Equals(actual?.Split(';')[0].Trim(), declared.Split(';')[0].Trim(), StringComparison.OrdinalIgnoreCase);

    internal static void ValidateContent(byte[] bytes, string contentType)
    {
        var type = contentType.Split(';')[0].Trim().ToLowerInvariant();
        var valid = type switch
        {
            "application/pdf" => bytes.AsSpan().StartsWith("%PDF-"u8),
            "image/png" => bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/jpeg" => bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 }),
            "text/plain" => IsText(bytes),
            "application/zip" or "application/x-zip-compressed" => IsArchive(bytes, docx: false),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => IsArchive(bytes, docx: true),
            _ => false
        };
        if (!valid) throw InvalidContent();
    }

    private static bool IsText(byte[] bytes)
    {
        try
        {
            var text = new UTF8Encoding(false, true).GetString(bytes);
            return text.Length > 0 && !text.Any(c => char.IsControl(c) && c != (char)13 && c != (char)10 && c != (char)9) &&
                !text.StartsWith("MZ", StringComparison.Ordinal) && !text.StartsWith("%PDF-", StringComparison.Ordinal);
        }
        catch (DecoderFallbackException) { return false; }
    }

    private static bool IsArchive(byte[] bytes, bool docx)
    {
        try
        {
            using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            if (archive.Entries.Count is 0 or > 1000 || archive.Entries.Sum(entry => entry.Length) > 50L * 1024 * 1024) return false;
            return !docx || archive.GetEntry("[Content_Types].xml") is not null && archive.GetEntry("word/document.xml") is not null;
        }
        catch (InvalidDataException) { return false; }
    }

    private static ArgumentException InvalidContent() => new("Tệp không hợp lệ hoặc không vượt qua kiểm tra an toàn.");
    private static ServiceUnavailableException Unavailable() => new("Dịch vụ kiểm tra an toàn tệp hiện không khả dụng. Vui lòng thử lại.");
    public void Dispose() => _slots.Dispose();
}
