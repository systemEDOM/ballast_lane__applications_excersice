using BallastLane.Domain.Enums;
using BallastLane.Infrastructure.Persistence;
using BallastLane.Infrastructure.Security;
using Shouldly;
using Xunit;

namespace BallastLane.InfrastructureTests;

public sealed class DatabaseInitializerTests : SqliteTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);

    [Fact]
    public async Task InitializeAsync_FreshDatabase_SeedsDemoUserWithSixTasks()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var initializer = new DatabaseInitializer(Factory, hasher, new FixedClock(Now));

        await initializer.InitializeAsync(CancellationToken.None);

        var users = new SqliteUserRepository(Factory);
        var demoUser = await users.FindByEmailAsync(DatabaseInitializer.DemoEmail, CancellationToken.None);
        demoUser.ShouldNotBeNull();
        var user = demoUser!;

        user.Email.ShouldBe("demo@ballastlane.test");
        user.DisplayName.ShouldBe("Demo User");
        user.PasswordIterations.ShouldBe(Pbkdf2PasswordHasher.DefaultIterations);
        hasher.Verify("Demo12345!", user.PasswordHash, user.PasswordSalt, user.PasswordIterations).ShouldBeTrue();

        var tasks = new SqliteTaskRepository(Factory);
        var seeded = await tasks.ListByUserAsync(user.Id, null, CancellationToken.None);

        seeded.Count.ShouldBe(6);
        seeded.Select(task => task.UserId).Distinct().ShouldBe([user.Id]);
        seeded.Select(task => task.Status).Distinct().ShouldBe(
            [TaskItemStatus.Todo, TaskItemStatus.InProgress, TaskItemStatus.Done],
            ignoreOrder: true);

        seeded.Count(task => task.Description is null).ShouldBe(1);
        seeded.Count(task => task.DueDate is null).ShouldBe(1);
        seeded.Where(task => task.DueDate is not null)
            .Select(task => task.DueDate.GetValueOrDefault())
            .ShouldBe(
                [
                    Today.AddDays(1),
                    Today.AddDays(3),
                    Today.AddDays(5),
                    Today.AddDays(7),
                    Today.AddDays(10)
                ],
                ignoreOrder: true);

        seeded.ShouldContain(task => task.DueDate == null && task.Title == "Archive the kickoff notes");
        seeded.ShouldContain(task => task.Description == null && task.Title == "Draft the proposal");
        seeded.ShouldAllBe(task => task.CreatedAt <= Now);
    }

    [Fact]
    public async Task InitializeAsync_RunTwice_DoesNotDuplicateSeedData()
    {
        var initializer = new DatabaseInitializer(Factory, new Pbkdf2PasswordHasher(), new FixedClock(Now));

        await initializer.InitializeAsync(CancellationToken.None);
        var firstUser = await new SqliteUserRepository(Factory)
            .FindByEmailAsync(DatabaseInitializer.DemoEmail, CancellationToken.None);
        firstUser.ShouldNotBeNull();

        await initializer.InitializeAsync(CancellationToken.None);

        (await ScalarLongAsync("SELECT COUNT(*) FROM users;")).ShouldBe(1L);
        (await ScalarLongAsync("SELECT COUNT(*) FROM tasks;")).ShouldBe(6L);
        (await ScalarLongAsync("SELECT COUNT(*) FROM schema_version;")).ShouldBe(1L);

        var secondUser = await new SqliteUserRepository(Factory)
            .FindByEmailAsync(DatabaseInitializer.DemoEmail, CancellationToken.None);
        secondUser.ShouldNotBeNull();
        secondUser!.Id.ShouldBe(firstUser!.Id);
    }
}
