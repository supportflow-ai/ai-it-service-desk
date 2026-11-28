using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ServiceDesk.Modules.Identity;
using ServiceDesk.Modules.Identity.Domain;
using ServiceDesk.Modules.Identity.Infrastructure;
using ServiceDesk.Modules.Identity.Infrastructure.Options;
using Xunit;

namespace ServiceDesk.Application.Tests.Identity;

public sealed class OtpCodeProtectionTests
{
    private static OtpCodeProtector Protector(byte keyByte = 1) => new(Options.Create(new OtpProtectionOptions
    {
        Key = Convert.ToBase64String(Enumerable.Repeat(keyByte, 32).ToArray())
    }));

    [Fact]
    public void VerificationIsBoundToEmailPurposeCodeAndIndependentKey()
    {
        var first = Protector();
        var digest = first.Hash("a@test.invalid", OtpCode.ResetPasswordPurpose, "123456");
        Assert.StartsWith("v1:", digest);
        Assert.Equal(digest, Protector().Hash("a@test.invalid", OtpCode.ResetPasswordPurpose, "123456"));
        Assert.NotEqual(digest, first.Hash("b@test.invalid", OtpCode.ResetPasswordPurpose, "123456"));
        Assert.NotEqual(digest, first.Hash("a@test.invalid", "OtherPurpose", "123456"));
        Assert.NotEqual(digest, first.Hash("a@test.invalid", OtpCode.ResetPasswordPurpose, "654321"));
        Assert.NotEqual(digest, Protector(2).Hash("a@test.invalid", OtpCode.ResetPasswordPurpose, "123456"));
        Assert.DoesNotContain("a@test.invalid", digest);
    }

    [Fact]
    public void StructuredInputsCannotCollideByMovingSeparators()
    {
        var protector = Protector();
        Assert.NotEqual(protector.Hash("a", "purpose", "b\0c"), protector.Hash("a\0b", "purpose", "c"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("YQ==")]
    public void MissingOrMalformedKeyFailsClosedWithoutDisclosingIt(string key)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Otp:Key"] = key }).Build();
        var services = new ServiceCollection().AddLogging().AddIdentityInfrastructure(config);
        using var provider = services.BuildServiceProvider();
        var error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<OtpProtectionOptions>>().Value);
        Assert.Contains("Otp:Key", error.Message);
        if (key.Length > 0) Assert.DoesNotContain(key, error.Message);
    }
}
