using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using Moq;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Infrastructure;
using ServiceDesk.SharedKernel.Application;
using ServiceDesk.SharedKernel.Application.Abstractions;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public sealed class AttachmentScannerTests
{
    [Theory]
    [InlineData("stream: OK", true, false)]
    [InlineData("stream: Test.Signature FOUND", true, true)]
    [InlineData("OK", true, true)]
    [InlineData("stream: ERROR", true, true)]
    [InlineData("stream: OK", false, true)]
    [InlineData("", true, true)]
    [InlineData("oversized", true, true)]
    public async Task OnlyCompleteCleanVerdictAllowsTheExactBoundedContent(string verdict, bool terminator, bool fails)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var bytes = Encoding.UTF8.GetBytes("Clean text for scanner");
        var request = ReplyAsync(listener, bytes, verdict == "oversized" ? new string('x', 1025) : verdict, terminator);
        var storage = Storage(bytes);
        using var scanner = Scanner(storage, ((IPEndPoint)listener.LocalEndpoint).Port);
        var action = () => scanner.ScanAsync("private/key", bytes.Length, "text/plain");
        if (!fails) await action();
        else if (verdict.EndsWith(" FOUND", StringComparison.Ordinal)) await Assert.ThrowsAsync<ArgumentException>(action);
        else
        {
            var error = await Assert.ThrowsAsync<ServiceUnavailableException>(action);
            Assert.DoesNotContain(verdict.Length > 0 ? verdict : "Incomplete", error.Message);
        }
        await request;
        storage.Verify(s => s.ReadAsync("private", "private/key", "etag", 10485760, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("application/pdf", "MZfake")]
    [InlineData("image/png", "%PDF-fake")]
    [InlineData("image/jpeg", "plain text")]
    [InlineData("application/zip", "plain text")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "plain text")]
    [InlineData("text/plain", "MZexecutable")]
    [InlineData("text/html", "<script>bad</script>")]
    public void DisguisedTypesCannotPassContentValidation(string type, string content) =>
        Assert.Throws<ArgumentException>(() => ClamAvAttachmentScanner.ValidateContent(Encoding.UTF8.GetBytes(content), type));

[Fact]
    public async Task ActualSizeAndReadLengthMustBothMatchTheGrant()
    {
        var bytes = "clean"u8.ToArray();
        var storage = Storage(bytes);
        using var scanner = Scanner(storage, 1);
        await Assert.ThrowsAsync<ArgumentException>(() => scanner.ScanAsync("key", 6, "text/plain"));
        storage.Verify(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        storage.Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 10485760, It.IsAny<CancellationToken>())).ReturnsAsync("changed"u8.ToArray());
        await Assert.ThrowsAsync<ArgumentException>(() => scanner.ScanAsync("key", 5, "text/plain"));
    }

    [Fact]
    public async Task MissingScannerAndCanceledCallerNeverReadContent()
    {
        var storage = Storage("clean"u8.ToArray());
        using var scanner = new ClamAvAttachmentScanner(storage.Object, Options.Create(new AttachmentScanningOptions()));
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => scanner.ScanAsync("key", 5, "text/plain"));
        storage.Verify(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        using var configured = Scanner(storage, 1);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => configured.ScanAsync("key", 5, "text/plain", canceled.Token));
    }

    [Fact]
    public async Task DownloadScansAnImmutableCopyAndUsesItsETag()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var bytes = "clean"u8.ToArray();
        var request = ReplyAsync(listener, bytes, "stream: OK", true);
        var storage = Storage(bytes);
        storage.Setup(s => s.StatAsync("private", It.Is<string>(key => key.StartsWith("downloads/quarantine/")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredObject(5, "text/plain", "copy-etag"));
        storage.Setup(s => s.ReadAsync("private", It.Is<string>(key => key.StartsWith("downloads/quarantine/")), "copy-etag", 10485760, It.IsAny<CancellationToken>())).ReturnsAsync(bytes);
        using var scanner = Scanner(storage, ((IPEndPoint)listener.LocalEndpoint).Port);
        var metadata = new TicketAttachmentDto(Guid.NewGuid(), Guid.NewGuid(), "legacy.txt", 5, "text/plain", "legacy/key", Guid.NewGuid(), DateTime.UtcNow);
        var key = await scanner.PrepareDownloadAsync(metadata);
        await request;
        Assert.StartsWith("downloads/quarantine/", key);
        Assert.NotEqual(metadata.StorageKey, key);
        storage.Verify(s => s.CopyAsync("private", "legacy/key", key, "etag", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SimultaneousAdmissionIsBoundedAndReleasedOnCancellation()
    {
        var bytes = "clean"u8.ToArray();
        var storage = Storage(bytes);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        storage.Setup(s => s.StatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, string _, CancellationToken ct) =>
            {
                if (Interlocked.Increment(ref calls) == 2) entered.SetResult();
                await Task.Delay(Timeout.Infinite, ct);
                return (StoredObject?)null;
            });
        using var scanner = Scanner(storage, 1);
        using var stop = new CancellationTokenSource();
        var a = scanner.ScanAsync("a", 5, "text/plain", stop.Token);
        var b = scanner.ScanAsync("b", 5, "text/plain", stop.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => scanner.ScanAsync("c", 5, "text/plain"));
        Assert.Equal(2, calls);
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => a);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => b);
        using var retry = new CancellationTokenSource();
        var next = scanner.ScanAsync("next", 5, "text/plain", retry.Token);
        Assert.Equal(3, calls);
        retry.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => next);
    }

    private static Mock<IObjectStorageService> Storage(byte[] bytes)
    {
        var storage = new Mock<IObjectStorageService>();
        storage.SetupGet(s => s.DefaultBucket).Returns("private");
        storage.Setup(s => s.StatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new StoredObject(bytes.Length, "text/plain", "etag"));
        storage.Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 10485760, It.IsAny<CancellationToken>())).ReturnsAsync(bytes);
        return storage;
    }

    private static ClamAvAttachmentScanner Scanner(Mock<IObjectStorageService> storage, int port) =>
        new(storage.Object, Options.Create(new AttachmentScanningOptions { Host = "127.0.0.1", Port = port }));

    private static async Task ReplyAsync(TcpListener listener, byte[] expected, string reply, bool terminator)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = await listener.AcceptTcpClientAsync(deadline.Token);
        await using var stream = client.GetStream();
        var command = new byte[10]; await stream.ReadExactlyAsync(command, deadline.Token);
        Assert.Equal(Encoding.ASCII.GetBytes("zINSTREAM" + (char)0), command);
        using var received = new MemoryStream();
        var header = new byte[4];
        while (true)
        {
            await stream.ReadExactlyAsync(header, deadline.Token);
            var count = BinaryPrimitives.ReadUInt32BigEndian(header);
            if (count == 0) break;
            Assert.InRange(count, 1u, 32768u);
            var chunk = new byte[count]; await stream.ReadExactlyAsync(chunk, deadline.Token);
            await received.WriteAsync(chunk, deadline.Token);
        }
        Assert.Equal(expected, received.ToArray());
        await stream.WriteAsync(Encoding.ASCII.GetBytes(reply + (terminator ? ((char)0).ToString() : "")), deadline.Token);
    }
}
