using Feirb.Api.Data;
using Feirb.Api.Data.Entities;
using Feirb.Shared.Settings;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Feirb.Api.Tests.Data;

public class DatabaseSeederTests
{
    private static FeirbDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<FeirbDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new FeirbDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static ILogger CreateLogger() =>
        LoggerFactory.Create(_ => { }).CreateLogger("DatabaseSeeder");

    private static IDataProtectionProvider CreateDataProtection() =>
        DataProtectionProvider.Create("Tests");

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder().Build();

    private static IHostEnvironment CreateEnvironment(string? environmentName = null) =>
        new TestHostEnvironment { EnvironmentName = environmentName ?? Environments.Development };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Feirb.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public async Task SeedAsync_EmptyDatabase_CreatesUsersMailboxesAndSmtpSettingsAsync()
    {
        using var db = CreateInMemoryContext();

        await DatabaseSeeder.SeedAsync(db, CreateLogger(), CreateDataProtection(), CreateConfiguration(), CreateEnvironment());

        var users = await db.Users.OrderBy(u => u.Username).ToListAsync();
        users.Should().HaveCount(2);

        users[0].Email.Should().Be("admin@feirb.local");
        users[0].Username.Should().Be("admin");
        users[0].IsAdmin.Should().BeTrue();
        BCrypt.Net.BCrypt.Verify("password", users[0].PasswordHash).Should().BeTrue();

        users[1].Email.Should().Be("alice@feirb.local");
        users[1].Username.Should().Be("alice");
        users[1].IsAdmin.Should().BeFalse();
        BCrypt.Net.BCrypt.Verify("password", users[1].PasswordHash).Should().BeTrue();

        var mailboxes = await db.Mailboxes.OrderBy(m => m.Name).ToListAsync();
        mailboxes.Should().HaveCount(2);
        mailboxes[0].EmailAddress.Should().Be("admin@feirb.local");
        mailboxes[0].ImapHost.Should().Be("localhost");
        mailboxes[0].ImapPort.Should().Be(3143);
        mailboxes[0].ImapTlsMode.Should().Be(TlsMode.None);
        mailboxes[0].SmtpHost.Should().Be("localhost");
        mailboxes[0].SmtpPort.Should().Be(3025);
        mailboxes[0].ImapEncryptedPassword.Should().NotBeNullOrEmpty();

        mailboxes[1].EmailAddress.Should().Be("alice@feirb.local");

        var smtp = await db.SmtpSettings.SingleAsync();
        smtp.Host.Should().Be("localhost");
        smtp.Port.Should().Be(3025);
        smtp.TlsMode.Should().Be(TlsMode.None);
        smtp.RequiresAuth.Should().BeFalse();
        smtp.FromAddress.Should().Be("noreply@feirb.local");
    }

    [Fact]
    public async Task SeedAsync_UsersAlreadyExist_DoesNotCreateDuplicatesAsync()
    {
        using var db = CreateInMemoryContext();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            Email = "admin@feirb.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("existing"),
            IsAdmin = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "alice",
            Email = "alice@feirb.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("existing"),
            IsAdmin = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await DatabaseSeeder.SeedAsync(db, CreateLogger(), CreateDataProtection(), CreateConfiguration(), CreateEnvironment());

        var users = await db.Users.ToListAsync();
        users.Should().HaveCount(2);
    }

    [Fact]
    public async Task SeedAsync_SmtpSettingsAlreadyExist_DoesNotCreateDuplicateAsync()
    {
        using var db = CreateInMemoryContext();
        db.SmtpSettings.Add(new SmtpSettings
        {
            Id = Guid.NewGuid(),
            Host = "existing-host",
            Port = 587,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await DatabaseSeeder.SeedAsync(db, CreateLogger(), CreateDataProtection(), CreateConfiguration(), CreateEnvironment());

        var settings = await db.SmtpSettings.ToListAsync();
        settings.Should().HaveCount(1);
        settings[0].Host.Should().Be("existing-host");
    }

    [Fact]
    public async Task SeedAsync_CalledTwice_IsIdempotentAsync()
    {
        using var db = CreateInMemoryContext();
        var dp = CreateDataProtection();

        await DatabaseSeeder.SeedAsync(db, CreateLogger(), dp, CreateConfiguration(), CreateEnvironment());
        await DatabaseSeeder.SeedAsync(db, CreateLogger(), dp, CreateConfiguration(), CreateEnvironment());

        (await db.Users.CountAsync()).Should().Be(2);
        (await db.Mailboxes.CountAsync()).Should().Be(2);
        (await db.SmtpSettings.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SeedAsync_MailboxAlreadyExists_DoesNotCreateDuplicateAsync()
    {
        using var db = CreateInMemoryContext();
        var dp = CreateDataProtection();

        // First seed creates everything
        await DatabaseSeeder.SeedAsync(db, CreateLogger(), dp, CreateConfiguration(), CreateEnvironment());

        // Second seed should not duplicate mailboxes
        await DatabaseSeeder.SeedAsync(db, CreateLogger(), dp, CreateConfiguration(), CreateEnvironment());

        var mailboxes = await db.Mailboxes.ToListAsync();
        mailboxes.Should().HaveCount(2);
    }

    [Fact]
    public async Task SeedAsync_ProductionEnvironment_ThrowsAndSeedsNothingAsync()
    {
        using var db = CreateInMemoryContext();

        var act = async () => await DatabaseSeeder.SeedAsync(
            db,
            CreateLogger(),
            CreateDataProtection(),
            CreateConfiguration(),
            CreateEnvironment(Environments.Production));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Production*");

        (await db.Users.CountAsync()).Should().Be(0);
        (await db.Mailboxes.CountAsync()).Should().Be(0);
        (await db.SmtpSettings.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public async Task SeedAsync_NonProductionEnvironments_SeedsSuccessfullyAsync(string environmentName)
    {
        using var db = CreateInMemoryContext();

        await DatabaseSeeder.SeedAsync(
            db,
            CreateLogger(),
            CreateDataProtection(),
            CreateConfiguration(),
            CreateEnvironment(environmentName));

        (await db.Users.CountAsync()).Should().Be(2);
    }
}
