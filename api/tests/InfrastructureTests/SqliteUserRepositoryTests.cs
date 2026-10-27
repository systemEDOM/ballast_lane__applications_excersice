using System.Globalization;
using BallastLane.Domain.Entities;
using BallastLane.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Shouldly;
using Xunit;

namespace BallastLane.InfrastructureTests;

public sealed class SqliteUserRepositoryTests : SqliteTestBase
{
    private static readonly DateTimeOffset CreatedAt =
        new DateTimeOffset(2026, 1, 2, 3, 4, 5, 678, TimeSpan.Zero).AddTicks(9012);

    [Fact]
    public async Task AddAsync_DuplicateEmail_ThrowsSqliteException()
    {
        await MigrateAsync();
        var repository = new SqliteUserRepository(Factory);

        await repository.AddAsync(NewUser(), CancellationToken.None);

        var exception = await Should.ThrowAsync<SqliteException>(
            () => repository.AddAsync(NewUser(), CancellationToken.None));

        exception.SqliteErrorCode.ShouldBe(19);
    }

    [Fact]
    public async Task AddAsync_ThenFindByEmailAsync_RoundTripsEveryColumn()
    {
        await MigrateAsync();
        var repository = new SqliteUserRepository(Factory);
        var user = NewUser("Alice@Example.TEST");

        await repository.AddAsync(user, CancellationToken.None);

        var stored = await repository.FindByEmailAsync("ALICE@example.test", CancellationToken.None);
        stored.ShouldNotBeNull();
        var actual = stored!;

        actual.Id.ShouldBe(user.Id);
        actual.Email.ShouldBe("alice@example.test");
        actual.DisplayName.ShouldBe("Alice Example");
        actual.PasswordHash.ShouldBe("hash-value");
        actual.PasswordSalt.ShouldBe("salt-value");
        actual.PasswordIterations.ShouldBe(210_000);
        actual.CreatedAt.ShouldBe(CreatedAt);
        actual.CreatedAt.Offset.ShouldBe(TimeSpan.Zero);

        (await ScalarStringAsync("SELECT id FROM users;"))
            .ShouldBe(user.Id.ToString("D", CultureInfo.InvariantCulture));
        (await ScalarStringAsync("SELECT created_at FROM users;"))
            .ShouldBe(CreatedAt.ToString("O", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task FindByEmailAsync_UnknownEmail_ReturnsNull()
    {
        await MigrateAsync();
        var repository = new SqliteUserRepository(Factory);
        await repository.AddAsync(NewUser(), CancellationToken.None);

        var stored = await repository.FindByEmailAsync("nobody@example.test", CancellationToken.None);

        stored.ShouldBeNull();
    }

    [Fact]
    public async Task FindByIdAsync_ExistingUser_ReturnsUser()
    {
        await MigrateAsync();
        var repository = new SqliteUserRepository(Factory);
        var user = NewUser();
        await repository.AddAsync(user, CancellationToken.None);

        var stored = await repository.FindByIdAsync(user.Id, CancellationToken.None);

        stored.ShouldNotBeNull();
        stored!.Id.ShouldBe(user.Id);
        stored.Email.ShouldBe(user.Email);
        stored.DisplayName.ShouldBe(user.DisplayName);
        stored.PasswordIterations.ShouldBe(user.PasswordIterations);
    }

    [Fact]
    public async Task FindByIdAsync_UnknownUser_ReturnsNull()
    {
        await MigrateAsync();
        var repository = new SqliteUserRepository(Factory);
        await repository.AddAsync(NewUser(), CancellationToken.None);

        var stored = await repository.FindByIdAsync(Guid.NewGuid(), CancellationToken.None);

        stored.ShouldBeNull();
    }

    [Fact]
    public async Task EmailExistsAsync_ReturnsTrueOnlyForStoredEmails()
    {
        await MigrateAsync();
        var repository = new SqliteUserRepository(Factory);
        await repository.AddAsync(NewUser("known@example.test"), CancellationToken.None);

        (await repository.EmailExistsAsync("known@example.test", CancellationToken.None)).ShouldBeTrue();
        (await repository.EmailExistsAsync("KNOWN@EXAMPLE.TEST", CancellationToken.None)).ShouldBeTrue();
        (await repository.EmailExistsAsync("missing@example.test", CancellationToken.None)).ShouldBeFalse();
    }

    private static User NewUser(string email = "alice@example.test")
        => User.Create(email, "Alice Example", "hash-value", "salt-value", 210_000, CreatedAt);
}
