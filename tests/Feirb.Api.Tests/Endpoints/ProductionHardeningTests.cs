using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Feirb.Api.Tests.Endpoints;

public class ProductionHardeningTests
{
    private const string _strongKey = "unit-test-signing-key-that-is-at-least-32-characters-long";

    private static WebApplicationFactory<Program> CreateFactory(
        string environment, string? autoLogin = null, string? jwtKey = null) =>
        TestWebApplicationFactory.Create($"hardening-{Guid.NewGuid()}").WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            if (autoLogin is not null)
                builder.UseSetting("AUTO_LOGIN", autoLogin);
            if (jwtKey is not null)
                builder.UseSetting("Jwt:Key", jwtKey);
        });

    private static async Task<bool> GetAutoLoginAsync(WebApplicationFactory<Program> factory)
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/dev/config");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("autoLogin").GetBoolean();
    }

    [Fact]
    public async Task DevConfig_DevelopmentWithAutoLogin_ReturnsAutoLoginTrueAsync()
    {
        await using var factory = CreateFactory(Environments.Development, autoLogin: "true");

        (await GetAutoLoginAsync(factory)).Should().BeTrue();
    }

    [Fact]
    public async Task DevConfig_DevelopmentWithoutAutoLogin_ReturnsAutoLoginFalseAsync()
    {
        await using var factory = CreateFactory(Environments.Development);

        (await GetAutoLoginAsync(factory)).Should().BeFalse();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task DevConfig_NonDevelopmentWithAutoLogin_ReturnsAutoLoginFalseAsync(string environment)
    {
        await using var factory = CreateFactory(environment, autoLogin: "true", jwtKey: _strongKey);

        (await GetAutoLoginAsync(factory)).Should().BeFalse();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Startup_NonDevelopmentWithPlaceholderJwtKey_Throws(string environment)
    {
        using var factory = CreateFactory(environment);

        var act = () => factory.CreateClient();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:Key*placeholder*");
    }

    [Fact]
    public void Startup_DevelopmentWithPlaceholderJwtKey_Starts()
    {
        using var factory = CreateFactory(Environments.Development);

        var act = () => factory.CreateClient();

        act.Should().NotThrow();
    }

    [Fact]
    public void Startup_ProductionWithCustomJwtKey_Starts()
    {
        using var factory = CreateFactory(Environments.Production, jwtKey: _strongKey);

        var act = () => factory.CreateClient();

        act.Should().NotThrow();
    }
}
