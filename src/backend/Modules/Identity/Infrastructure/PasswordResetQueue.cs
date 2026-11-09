using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceDesk.Modules.Identity.Application.ForgotPassword;
using ServiceDesk.Modules.Identity.Application.Ports;

namespace ServiceDesk.Modules.Identity.Infrastructure;

/// <summary>
/// Hàng đợi trong bộ nhớ cho yêu cầu quên mật khẩu (mỗi instance API một hàng đợi). Mất khi tiến trình dừng;
/// người dùng gửi lại yêu cầu. Đầy thì bỏ yêu cầu mới và ghi cảnh báo, không ghi email.
/// </summary>
internal sealed class PasswordResetQueue : IPasswordResetQueue
{
    internal const int DefaultCapacity = 1000;

    private readonly ILogger<PasswordResetQueue> logger;

    // FullMode.Wait: TryWrite trả false khi đầy. Với DropWrite, TryWrite vẫn trả true dù yêu cầu bị bỏ,
    // nên không thể biết để ghi cảnh báo.
    private readonly Channel<string> channel;

    public PasswordResetQueue(ILogger<PasswordResetQueue> logger) : this(logger, DefaultCapacity)
    {
    }

    internal PasswordResetQueue(ILogger<PasswordResetQueue> logger, int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        this.logger = logger;
        channel = Channel.CreateBounded<string>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });
    }

    internal ChannelReader<string> Reader => channel.Reader;

    public bool TryEnqueue(string normalizedEmail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        if (channel.Writer.TryWrite(normalizedEmail)) return true;
        logger.LogWarning("Hàng đợi quên mật khẩu đã đầy; yêu cầu bị bỏ qua.");
        return false;
    }
}

/// <summary>Xử lý nền hàng đợi quên mật khẩu: mỗi yêu cầu chạy trong một scope DI riêng.</summary>
internal sealed class PasswordResetQueueProcessor(
    PasswordResetQueue queue,
    IServiceScopeFactory scopes,
    ILogger<PasswordResetQueueProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var email in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<PasswordResetCodeIssuer>().IssueAsync(email, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Không ghi email hay mã; chỉ loại lỗi để vận hành điều tra.
                logger.LogError("Phát mã đặt lại mật khẩu thất bại ({ErrorType}).", ex.GetType().Name);
            }
        }
    }
}
