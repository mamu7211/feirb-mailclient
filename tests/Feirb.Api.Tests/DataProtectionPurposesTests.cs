using FluentAssertions;

namespace Feirb.Api.Tests;

public class DataProtectionPurposesTests
{
    // The purpose strings are part of the key derivation for encrypted credentials.
    // Changing a value makes every credential stored with it unrecoverable, so the
    // literals are pinned here on purpose.

    [Fact]
    public void SmtpPassword_Value_IsUnchanged() =>
        DataProtectionPurposes.SmtpPassword.Should().Be("SmtpPassword");

    [Fact]
    public void MailboxImapPassword_Value_IsUnchanged() =>
        DataProtectionPurposes.MailboxImapPassword.Should().Be("MailboxImapPassword");

    [Fact]
    public void MailboxSmtpPassword_Value_IsUnchanged() =>
        DataProtectionPurposes.MailboxSmtpPassword.Should().Be("MailboxSmtpPassword");
}
