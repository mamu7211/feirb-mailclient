namespace Feirb.Api.Services;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>Prefix of the placeholder signing key shipped in appsettings.json.</summary>
    public const string PlaceholderKeyPrefix = "CHANGE-ME";

    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public required string Key { get; set; }
    public int AccessTokenExpiryMinutes { get; set; } = 60;
    public int RefreshTokenExpiryDays { get; set; } = 7;
}
