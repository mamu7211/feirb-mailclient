namespace Feirb.Api.Services;

/// <summary>
/// Configuration for the fixed-window rate limiting policies applied to anonymous endpoints
/// (login, register, password reset, refresh, SMTP connection test) to mitigate brute-force
/// and credential-stuffing attempts. See issue #45.
///
/// Two named instances of this class are bound (see <see cref="Program"/>):
/// the default instance from <see cref="SectionName"/> drives the "auth" policy
/// (login, register, password reset, SMTP connection test), and the <see cref="RefreshOptionsName"/>
/// named instance from <see cref="RefreshSectionName"/> drives the separate "auth-refresh" policy.
/// Refresh gets its own, more generous bucket because the frontend's <c>AuthDelegatingHandler</c>
/// calls it on every 401 without de-duplication, so several widgets loading in parallel after an
/// access token expires can issue a burst of concurrent refresh calls from one IP.
/// </summary>
public class AuthRateLimitSettings
{
    public const string SectionName = "RateLimiting:Auth";
    public const string RefreshSectionName = "RateLimiting:AuthRefresh";

    /// <summary>Name under which the refresh-specific instance is registered via <c>IOptionsMonitor.Get(name)</c>.</summary>
    public const string RefreshOptionsName = "Refresh";

    /// <summary>Maximum number of requests permitted per client IP within the window.</summary>
    public int PermitLimit { get; set; } = 10;

    /// <summary>Length of the fixed rate-limiting window, in seconds.</summary>
    public int WindowSeconds { get; set; } = 60;
}
