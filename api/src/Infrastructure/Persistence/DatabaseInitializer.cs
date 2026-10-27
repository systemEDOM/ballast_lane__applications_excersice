using BallastLane.Application.Abstractions;
using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;

namespace BallastLane.Infrastructure.Persistence;

public sealed class DatabaseInitializer : IAppInitializer
{
    public const string DemoEmail = "demo@ballastlane.test";
    public const string DemoPassword = "Demo12345!";
    public const string DemoDisplayName = "Demo User";

    private static readonly SeedTask[] DemoTasks =
    [
        new SeedTask(
            "Collect October receipts",
            "Gather every expense receipt for the Northwind account.",
            TaskItemStatus.InProgress,
            1),
        new SeedTask(
            "Draft the proposal",
            null,
            TaskItemStatus.Todo,
            3),
        new SeedTask(
            "Review design handoff",
            "Check spacing, empty states and error screens before the client call.",
            TaskItemStatus.Todo,
            5),
        new SeedTask(
            "Send the retainer invoice",
            "Invoice 2041, payable on receipt.",
            TaskItemStatus.Done,
            7),
        new SeedTask(
            "Prepare sprint demo",
            "Pick three flows and record a short walkthrough.",
            TaskItemStatus.InProgress,
            10),
        new SeedTask(
            "Archive the kickoff notes",
            "Move the signed scope into the shared client folder.",
            TaskItemStatus.Done,
            null)
    ];

    private readonly IDbConnectionFactory _factory;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;

    public DatabaseInitializer(IDbConnectionFactory factory, IPasswordHasher hasher, IClock clock)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await new SchemaMigrator(_factory).MigrateAsync(cancellationToken);

        var users = new SqliteUserRepository(_factory);

        if (await users.EmailExistsAsync(DemoEmail, cancellationToken))
        {
            return;
        }

        var now = _clock.UtcNow;
        var today = _clock.Today;
        var hash = _hasher.Hash(DemoPassword, out var salt, out var iterations);

        var demoUser = User.Create(DemoEmail, DemoDisplayName, hash, salt, iterations, now);
        await users.AddAsync(demoUser, cancellationToken);

        var tasks = new SqliteTaskRepository(_factory);

        for (var index = 0; index < DemoTasks.Length; index++)
        {
            var seed = DemoTasks[index];
            var createdAt = now.AddMinutes(-(60 + (index * 45)));

            var task = TaskItem.Create(
                demoUser.Id,
                seed.Title,
                seed.Description,
                seed.DueInDays is null ? null : today.AddDays(seed.DueInDays.Value),
                today,
                createdAt);

            if (seed.Status != TaskItemStatus.Todo)
            {
                task.ChangeStatus(seed.Status, createdAt.AddMinutes(15));
            }

            await tasks.AddAsync(task, cancellationToken);
        }
    }

    private sealed record SeedTask(string Title, string? Description, TaskItemStatus Status, int? DueInDays);
}
