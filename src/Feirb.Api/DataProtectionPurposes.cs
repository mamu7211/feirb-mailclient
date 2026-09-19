namespace Feirb.Api;

/// <summary>
/// Data Protection purpose strings for encrypted credentials.
/// The values are part of the encryption key derivation: changing one makes all credentials
/// that were stored with it unrecoverable. Never edit the values; add a new constant instead.
/// </summary>
internal static class DataProtectionPurposes
{
    /// <summary>System-wide outgoing SMTP password.</summary>
    public const string SmtpPassword = "SmtpPassword";

    /// <summary>IMAP password of a user mailbox.</summary>
    public const string MailboxImapPassword = "MailboxImapPassword";

    /// <summary>SMTP password of a user mailbox.</summary>
    public const string MailboxSmtpPassword = "MailboxSmtpPassword";
}
