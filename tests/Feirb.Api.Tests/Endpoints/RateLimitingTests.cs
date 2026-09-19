using System.Net;
using System.Net.Http.Json;
using Feirb.Shared.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Feirb.Api.Tests.Endpoints;

public class RateLimitingTests
{
    private static WebApplicationFactory<Program> CreateFactory(
        int? permitLimit = null, int? windowSeconds = null, int? refreshPermitLimit = null, int? refreshWindowSeconds = null) =>
        TestWebApplicationFactory.Create($"ratelimit-{Guid.NewGuid()}").WithWebHostBuilder(builder =>
        {
            if (permitLimit is not null)
                builder.UseSetting("RateLimiting:Auth:PermitLimit", permitLimit.Value.ToString());
            if (windowSeconds is not null)
                builder.UseSetting("RateLimiting:Auth:WindowSeconds", windowSeconds.Value.ToString());
            if (refreshPermitLimit is not null)
                builder.UseSetting("RateLimiting:AuthRefresh:PermitLimit", refreshPermitLimit.Value.ToString());
            if (refreshWindowSeconds is not null)
                builder.UseSetting("RateLimiting:AuthRefresh:WindowSeconds", refreshWindowSeconds.Value.ToString());
        });

    [Fact]
    public async Task Login_ExceedsConfiguredPermitLimit_ReturnsTooManyRequestsAsync()
    {
        await using var factory = CreateFactory(permitLimit: 2);
        using var client = factory.CreateClient();
        var request = new LoginRequest("nonexistent-user", "wrong-password");

        for (var i = 0; i < 2; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", request);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var limited = await client.PostAsJsonAsync("/api/auth/login", request);

        limited.StatusCode.Should().Be((HttpStatusCode)429);
    }

    [Fact]
    public async Task Login_ExceedsPermitLimit_SetsRetryAfterHeaderAsync()
    {
        await using var factory = CreateFactory(permitLimit: 1, windowSeconds: 30);
        using var client = factory.CreateClient();
        var request = new LoginRequest("nonexistent-user", "wrong-password");

        await client.PostAsJsonAsync("/api/auth/login", request);
        var limited = await client.PostAsJsonAsync("/api/auth/login", request);

        limited.StatusCode.Should().Be((HttpStatusCode)429);
        limited.Headers.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task Login_WithinConfiguredPermitLimit_NotRejectedAsync()
    {
        await using var factory = CreateFactory(permitLimit: 5);
        using var client = factory.CreateClient();
        var request = new LoginRequest("nonexistent-user", "wrong-password");

        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", request);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                because: "requests within the configured limit must reach the endpoint handler");
        }
    }

    [Fact]
    public async Task NonAuthEndpoint_NotLimitedByAuthPolicy_RemainsAvailableAfterAuthLimitTrippedAsync()
    {
        await using var factory = CreateFactory(permitLimit: 1);
        using var client = factory.CreateClient();
        var loginRequest = new LoginRequest("nonexistent-user", "wrong-password");

        // Trip the "auth" policy for this client's partition.
        await client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var limited = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
        limited.StatusCode.Should().Be((HttpStatusCode)429);

        // An anonymous endpoint outside the "auth" policy must still succeed.
        var devConfigResponse = await client.GetAsync("/api/dev/config");

        devConfigResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_LimitTakenFromConfiguration_DifferentLimitsProduceDifferentThresholdsAsync()
    {
        await using var tightFactory = CreateFactory(permitLimit: 1);
        using var tightClient = tightFactory.CreateClient();
        var request = new LoginRequest("nonexistent-user", "wrong-password");

        await tightClient.PostAsJsonAsync("/api/auth/login", request);
        var tightSecond = await tightClient.PostAsJsonAsync("/api/auth/login", request);
        tightSecond.StatusCode.Should().Be((HttpStatusCode)429);

        await using var looseFactory = CreateFactory(permitLimit: 3);
        using var looseClient = looseFactory.CreateClient();

        // A second request that would trip a limit of 1 must succeed under a configured limit of 3.
        await looseClient.PostAsJsonAsync("/api/auth/login", request);
        var looseSecond = await looseClient.PostAsJsonAsync("/api/auth/login", request);
        looseSecond.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ExceedsConfiguredPermitLimit_ReturnsTooManyRequestsAsync()
    {
        await using var factory = CreateFactory(refreshPermitLimit: 2);
        using var client = factory.CreateClient();

        for (var i = 0; i < 2; i++)
        {
            var response = await client.PostAsync("/api/auth/refresh", null);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                because: "no refresh token cookie was sent");
        }

        var limited = await client.PostAsync("/api/auth/refresh", null);

        limited.StatusCode.Should().Be((HttpStatusCode)429);
    }

    [Fact]
    public async Task Login_ExhaustsAuthBucket_DoesNotAffectRefreshBucketAsync()
    {
        await using var factory = CreateFactory(permitLimit: 1, refreshPermitLimit: 100000);
        using var client = factory.CreateClient();
        var loginRequest = new LoginRequest("nonexistent-user", "wrong-password");

        // Trip the "auth" policy for this client's partition.
        await client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var limitedLogin = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
        limitedLogin.StatusCode.Should().Be((HttpStatusCode)429);

        // Refresh is a different named policy ("auth-refresh") with its own bucket, so it must
        // still be reachable even though the "auth" bucket for this IP is exhausted.
        var refreshResponse = await client.PostAsync("/api/auth/refresh", null);

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "the refresh policy bucket is independent of the exhausted login/auth bucket");
    }

    [Fact]
    public async Task Refresh_ExhaustsRefreshBucket_DoesNotAffectAuthBucketAsync()
    {
        await using var factory = CreateFactory(permitLimit: 100000, refreshPermitLimit: 1);
        using var client = factory.CreateClient();

        // Trip the "auth-refresh" policy for this client's partition.
        await client.PostAsync("/api/auth/refresh", null);
        var limitedRefresh = await client.PostAsync("/api/auth/refresh", null);
        limitedRefresh.StatusCode.Should().Be((HttpStatusCode)429);

        // Login uses the separate "auth" policy bucket, unaffected by the exhausted refresh bucket.
        var loginRequest = new LoginRequest("nonexistent-user", "wrong-password");
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest);

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
