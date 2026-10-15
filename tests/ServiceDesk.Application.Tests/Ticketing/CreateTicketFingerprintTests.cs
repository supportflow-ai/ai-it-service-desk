using FluentAssertions;
using ServiceDesk.Application.Ticketing.Commands;
using ServiceDesk.Application.Ticketing.Idempotency;
using Xunit;

namespace ServiceDesk.Application.Tests.Ticketing;

public class CreateTicketFingerprintTests
{
    private static readonly CreateTicketCommand Payload = new("Network issue", "Cannot connect to the network", "NET");

    [Fact]
    public void SameSemanticPayloadAndTrimmedKey_HaveStableFingerprint()
    {
        var user = Guid.NewGuid();
        var original = CreateTicketFingerprint.Create(user, "key", Payload);
        var padded = CreateTicketFingerprint.Create(user, " key ", Payload with
        {
            Title = " Network issue ", Description = " Cannot connect to the network "
        });
        padded.Should().Be(original);
    }

    [Fact]
    public void DifferentRequester_SameKeyAndPayload_UsesDifferentLockScope()
    {
        var first = CreateTicketFingerprint.Create(Guid.NewGuid(), "shared-key", Payload);
        var second = CreateTicketFingerprint.Create(Guid.NewGuid(), "shared-key", Payload);
        first.LockId.Should().NotBe(second.LockId);
        first.PayloadHash.Should().Be(second.PayloadHash);
    }

    [Fact]
    public void ChangedPayload_IsDetectedEvenWhenFieldSeparatorsCouldBeAmbiguous()
    {
        var user = Guid.NewGuid();
        var first = CreateTicketFingerprint.Create(user, "key", new("ab", "c:de", "NET"));
        var second = CreateTicketFingerprint.Create(user, "key", new("ab:c", "de", "NET"));
        first.PayloadHash.Should().NotBe(second.PayloadHash);
        first.LockId.Should().Be(second.LockId);
    }

    [Fact]
    public void ChangedCategoryOrDescription_IsAChangedPayload()
    {
        var user = Guid.NewGuid();
        var original = CreateTicketFingerprint.Create(user, "key", Payload);
        CreateTicketFingerprint.Create(user, "key", Payload with { CategoryId = "ACC" })
            .PayloadHash.Should().NotBe(original.PayloadHash);
        CreateTicketFingerprint.Create(user, "key", Payload with { Description = "Different valid description" })
            .PayloadHash.Should().NotBe(original.PayloadHash);
    }

    [Fact]
    public void DifferentKey_DoesNotShareScope_AndFingerprintsDoNotRetainRawData()
    {
        var user = Guid.NewGuid();
        var first = CreateTicketFingerprint.Create(user, "private-key", Payload);
        var second = CreateTicketFingerprint.Create(user, "other-key", Payload);
        first.LockId.Should().NotBe(second.LockId);
        first.KeyHash.Should().NotBe(second.KeyHash);
        first.KeyHash.Should().MatchRegex("^[0-9A-F]{64}$");
        first.PayloadHash.Should().MatchRegex("^[0-9A-F]{64}$");
        first.ToString().Should().NotContain("private-key").And.NotContain(Payload.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("internal space")]
    [InlineData("key\nvalue")]
    [InlineData("khóa")]
    public void EmptyOrNonVisibleAsciiKey_IsRejected(string key)
    {
        var action = () => CreateTicketFingerprint.Create(Guid.NewGuid(), key, Payload);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void OversizedKey_IsRejectedBeforeStorage()
    {
        var action = () => CreateTicketFingerprint.Create(Guid.NewGuid(), new string('K', 201), Payload);
        action.Should().Throw<ArgumentException>();
        CreateTicketFingerprint.Create(Guid.NewGuid(), new string('K', 200), Payload).KeyHash.Length.Should().Be(64);
    }
}
