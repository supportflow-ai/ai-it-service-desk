using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ServiceDesk.Modules.Identity;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Infrastructure;
using ServiceDesk.Modules.Identity.Infrastructure.Options;
using Xunit;

namespace ServiceDesk.Application.Tests.Identity;

/// <summary>Chọn kênh gửi OTP theo cấu hình, kiểm tra cấu hình SMTP và nội dung email.</summary>
public sealed class OtpSenderSelectionTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddIdentityInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void WithoutSmtpHostOtpIsNotSent()
    {
        using var provider = Build([]);
        Assert.IsType<NoOpOtpSender>(provider.GetRequiredService<IOtpSender>());
    }

    [Fact]
    public void WithSmtpHostOtpIsSentBySmtp()
    {
        using var provider = Build(new()
        {
            ["Email:Smtp:Host"] = "smtp.test.invalid",
            ["Email:Smtp:FromAddress"] = "no-reply@test.invalid"
        });
        Assert.IsType<SmtpOtpSender>(provider.GetRequiredService<IOtpSender>());
    }

    [Fact]
    public void ConfiguredSmtpRequiresValidSenderAndPasswordForUser()
    {
        var result = new SmtpEmailOptionsValidator().Validate(null, new SmtpEmailOptions
        {
            Host = "smtp.test.invalid", Port = 0, FromAddress = "not-an-email", UserName = "mailer"
        });

        Assert.True(result.Failed);
        Assert.Equal(3, result.Failures!.Count());
    }

    [Fact]
    public void UnconfiguredSmtpIsValid() =>
        Assert.True(new SmtpEmailOptionsValidator().Validate(null, new SmtpEmailOptions()).Succeeded);

    [Fact]
    public void ResetEmailCarriesCodeAndExpiry()
    {
        var options = new SmtpEmailOptions { Host = "smtp.test.invalid", FromAddress = "no-reply@test.invalid", FromName = "SupportFlow" };

        using var message = PasswordResetEmail.Create(options, "user@test.invalid", "123456",
            new DateTime(2026, 11, 7, 9, 15, 0, DateTimeKind.Utc));

        Assert.Equal("user@test.invalid", Assert.Single(message.To).Address);
        Assert.Equal("no-reply@test.invalid", message.From!.Address);
        Assert.Equal(PasswordResetEmail.Subject, message.Subject);
        Assert.Contains("123456", message.Body, StringComparison.Ordinal);
        Assert.Contains("09:15 UTC 07/11/2026", message.Body, StringComparison.Ordinal);
        Assert.False(message.IsBodyHtml);
    }

    [Fact]
    public void FullResetQueueReportsTheDroppedRequest()
    {
        var queue = new PasswordResetQueue(NullLogger<PasswordResetQueue>.Instance, capacity: 2);

        Assert.True(queue.TryEnqueue("a@test.invalid"));
        Assert.True(queue.TryEnqueue("b@test.invalid"));
        Assert.False(queue.TryEnqueue("c@test.invalid"));
        Assert.True(queue.Reader.TryRead(out var first));
        Assert.Equal("a@test.invalid", first);
        Assert.True(queue.TryEnqueue("c@test.invalid"));
    }
}
