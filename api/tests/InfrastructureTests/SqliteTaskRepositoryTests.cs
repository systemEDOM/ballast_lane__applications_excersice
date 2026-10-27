using System.Globalization;
using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;
using BallastLane.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace BallastLane.InfrastructureTests;

public sealed class SqliteTaskRepositoryTests : SqliteTestBase
{
    private static readonly DateOnly Today = new(2026, 11, 10);
    private static readonly DateTimeOffset Morning = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddAsync_ThenFindByIdAsync_RoundTripsEveryColumn()
    {
        await MigrateAsync();
        var user = await AddUserAsync();
        var repository = new SqliteTaskRepository(Factory);
        var createdAt = new DateTimeOffset(2026, 11, 1, 9, 30, 15, TimeSpan.Zero).AddTicks(1234567);
        var task = TaskItem.Rehydrate(
            Guid.NewGuid(),
            user.Id,
            "Draft the proposal",
            "First draft for the client review.",
            TaskItemStatus.InProgress,
            new DateOnly(2026, 11, 15),
            createdAt,
            createdAt.AddMinutes(7));

        await repository.AddAsync(task, CancellationToken.None);

        var stored = await repository.FindByIdAsync(task.Id, CancellationToken.None);
        stored.ShouldNotBeNull();
        var actual = stored!;

        actual.Id.ShouldBe(task.Id);
        actual.UserId.ShouldBe(user.Id);
        actual.Title.ShouldBe("Draft the proposal");
        actual.Description.ShouldBe("First draft for the client review.");
        actual.Status.ShouldBe(TaskItemStatus.InProgress);
        actual.DueDate.ShouldBe(new DateOnly(2026, 11, 15));
        actual.CreatedAt.ShouldBe(createdAt);
        actual.UpdatedAt.ShouldBe(createdAt.AddMinutes(7));

        (await ScalarStringAsync("SELECT id FROM tasks;")).ShouldBe(task.Id.ToString("D", CultureInfo.InvariantCulture));
        (await ScalarStringAsync("SELECT status FROM tasks;")).ShouldBe("InProgress");
        (await ScalarStringAsync("SELECT due_date FROM tasks;")).ShouldBe("2026-11-15");
        (await ScalarStringAsync("SELECT created_at FROM tasks;"))
            .ShouldBe(createdAt.ToString("O", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task AddAsync_NullDescriptionAndDueDate_AreStoredAsNull()
    {
        await MigrateAsync();
        var user = await AddUserAsync();
        var repository = new SqliteTaskRepository(Factory);
        var task = NewTask(user.Id, title: "Collect client feedback", description: null, dueDate: null);

        await repository.AddAsync(task, CancellationToken.None);

        var stored = await repository.FindByIdAsync(task.Id, CancellationToken.None);
        stored.ShouldNotBeNull();
        stored!.Description.ShouldBeNull();
        stored.DueDate.ShouldBeNull();

        (await ScalarLongAsync("SELECT COUNT(*) FROM tasks WHERE description IS NULL AND due_date IS NULL;"))
            .ShouldBe(1L);
    }

    [Fact]
    public async Task ListByUserAsync_OrdersDatedTasksFirstThenUndated()
    {
        await MigrateAsync();
        var user = await AddUserAsync();
        var repository = new SqliteTaskRepository(Factory);

        await repository.AddAsync(
            NewTask(user.Id, "Later dated", dueDate: Today.AddDays(5), createdAt: Morning.AddDays(0)),
            CancellationToken.None);
        await repository.AddAsync(
            NewTask(user.Id, "Undated", dueDate: null, createdAt: Morning.AddDays(1)),
            CancellationToken.None);
        await repository.AddAsync(
            NewTask(user.Id, "Sooner dated earlier", dueDate: Today.AddDays(2), createdAt: Morning.AddDays(2)),
            CancellationToken.None);
        await repository.AddAsync(
            NewTask(user.Id, "Sooner dated later", dueDate: Today.AddDays(2), createdAt: Morning.AddDays(3)),
            CancellationToken.None);

        var tasks = await repository.ListByUserAsync(user.Id, null, CancellationToken.None);

        tasks.Select(task => task.Title).ShouldBe(
            ["Sooner dated earlier", "Sooner dated later", "Later dated", "Undated"]);
    }

    [Fact]
    public async Task ListByUserAsync_StatusFilter_ReturnsMatchingTasksOnly()
    {
        await MigrateAsync();
        var user = await AddUserAsync();
        var repository = new SqliteTaskRepository(Factory);

        await repository.AddAsync(
            NewTask(user.Id, "Waiting task", status: TaskItemStatus.Todo, dueDate: Today.AddDays(1)),
            CancellationToken.None);
        await repository.AddAsync(
            NewTask(user.Id, "Focused task", status: TaskItemStatus.InProgress, dueDate: Today.AddDays(2)),
            CancellationToken.None);
        await repository.AddAsync(
            NewTask(user.Id, "Finished task", status: TaskItemStatus.Done, dueDate: Today.AddDays(3)),
            CancellationToken.None);

        var inProgress = await repository.ListByUserAsync(user.Id, TaskItemStatus.InProgress, CancellationToken.None);
        var all = await repository.ListByUserAsync(user.Id, null, CancellationToken.None);

        inProgress.Count.ShouldBe(1);
        inProgress[0].Title.ShouldBe("Focused task");
        all.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ListByUserAsync_OtherUsersTasks_AreExcluded()
    {
        await MigrateAsync();
        var alice = await AddUserAsync("alice@example.test");
        var bob = await AddUserAsync("bob@example.test");
        var repository = new SqliteTaskRepository(Factory);

        await repository.AddAsync(NewTask(alice.Id, "Alice first", dueDate: Today.AddDays(1)), CancellationToken.None);
        await repository.AddAsync(NewTask(alice.Id, "Alice second", dueDate: Today.AddDays(2)), CancellationToken.None);
        await repository.AddAsync(NewTask(bob.Id, "Bob first", dueDate: Today.AddDays(3)), CancellationToken.None);

        var tasks = await repository.ListByUserAsync(alice.Id, null, CancellationToken.None);

        tasks.Count.ShouldBe(2);
        tasks.ShouldAllBe(task => task.UserId == alice.Id);
    }

    [Fact]
    public async Task UpdateAsync_OwnedTask_PersistsChanges()
    {
        await MigrateAsync();
        var user = await AddUserAsync();
        var repository = new SqliteTaskRepository(Factory);
        var task = NewTask(user.Id, title: "Original title", dueDate: Today.AddDays(4));
        await repository.AddAsync(task, CancellationToken.None);

        var changedAt = Morning.AddDays(2);
        task.UpdateDetails("Updated title", "Updated description", Today.AddDays(9), Today, changedAt);
        task.ChangeStatus(TaskItemStatus.InProgress, changedAt);

        var updated = await repository.UpdateAsync(task, CancellationToken.None);

        updated.ShouldBeTrue();
        var stored = await repository.FindByIdAsync(task.Id, CancellationToken.None);
        stored.ShouldNotBeNull();
        stored!.Title.ShouldBe("Updated title");
        stored.Description.ShouldBe("Updated description");
        stored.Status.ShouldBe(TaskItemStatus.InProgress);
        stored.DueDate.ShouldBe(Today.AddDays(9));
        stored.UpdatedAt.ShouldBe(changedAt);
    }

    [Fact]
    public async Task UpdateAsync_ForeignTask_ReturnsFalseAndLeavesRowUnchanged()
    {
        await MigrateAsync();
        var owner = await AddUserAsync("owner@example.test");
        var stranger = await AddUserAsync("stranger@example.test");
        var repository = new SqliteTaskRepository(Factory);
        var task = NewTask(owner.Id, title: "Owners task", dueDate: Today.AddDays(3));
        await repository.AddAsync(task, CancellationToken.None);

        var tampered = TaskItem.Rehydrate(
            task.Id,
            stranger.Id,
            "Hijacked title",
            "Hijacked description",
            TaskItemStatus.Done,
            Today.AddDays(30),
            task.CreatedAt,
            Morning.AddDays(20));

        var updated = await repository.UpdateAsync(tampered, CancellationToken.None);

        updated.ShouldBeFalse();
        var stored = await repository.FindByIdAsync(task.Id, CancellationToken.None);
        stored.ShouldNotBeNull();
        stored!.UserId.ShouldBe(owner.Id);
        stored.Title.ShouldBe("Owners task");
        stored.Status.ShouldBe(TaskItemStatus.Todo);
        stored.DueDate.ShouldBe(Today.AddDays(3));
        stored.UpdatedAt.ShouldBe(task.UpdatedAt);
    }

    [Fact]
    public async Task DeleteAsync_OwnedTask_RemovesRow()
    {
        await MigrateAsync();
        var user = await AddUserAsync();
        var repository = new SqliteTaskRepository(Factory);
        var task = NewTask(user.Id, dueDate: Today.AddDays(2));
        await repository.AddAsync(task, CancellationToken.None);

        var deleted = await repository.DeleteAsync(task.Id, user.Id, CancellationToken.None);

        deleted.ShouldBeTrue();
        (await repository.FindByIdAsync(task.Id, CancellationToken.None)).ShouldBeNull();
        (await ScalarLongAsync("SELECT COUNT(*) FROM tasks;")).ShouldBe(0L);
    }

    [Fact]
    public async Task DeleteAsync_ForeignTask_ReturnsFalseAndKeepsRow()
    {
        await MigrateAsync();
        var owner = await AddUserAsync("owner@example.test");
        var stranger = await AddUserAsync("stranger@example.test");
        var repository = new SqliteTaskRepository(Factory);
        var task = NewTask(owner.Id, dueDate: Today.AddDays(2));
        await repository.AddAsync(task, CancellationToken.None);

        var deleted = await repository.DeleteAsync(task.Id, stranger.Id, CancellationToken.None);

        deleted.ShouldBeFalse();
        (await repository.FindByIdAsync(task.Id, CancellationToken.None)).ShouldNotBeNull();
    }

    [Fact]
    public async Task FindByIdAsync_UnknownTask_ReturnsNull()
    {
        await MigrateAsync();
        var repository = new SqliteTaskRepository(Factory);

        var stored = await repository.FindByIdAsync(Guid.NewGuid(), CancellationToken.None);

        stored.ShouldBeNull();
    }

    private static TaskItem NewTask(
        Guid userId,
        string title = "Draft the proposal",
        string? description = "First draft for the client review.",
        TaskItemStatus status = TaskItemStatus.Todo,
        DateOnly? dueDate = null,
        DateTimeOffset? createdAt = null)
    {
        var created = createdAt ?? Morning;

        return TaskItem.Rehydrate(
            Guid.NewGuid(),
            userId,
            title,
            description,
            status,
            dueDate,
            created,
            created.AddHours(1));
    }
}
