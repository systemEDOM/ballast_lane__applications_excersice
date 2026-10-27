using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;

namespace BallastLane.UnitTests.Support;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    public static readonly DateOnly Today = new(2026, 10, 26);

    public static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public const string PasswordHash = "stored-password-hash";

    public const string PasswordSalt = "stored-password-salt";

    public const int PasswordIterations = 210_000;

    public static User NewUser(
        Guid? id = null,
        string email = "demo@ballastlane.test",
        string displayName = "Demo User")
        => User.Rehydrate(
            id ?? UserId,
            email,
            displayName,
            PasswordHash,
            PasswordSalt,
            PasswordIterations,
            Now);

    public static TaskItem NewTask(
        Guid? id = null,
        Guid? userId = null,
        TaskItemStatus status = TaskItemStatus.Todo,
        string title = "Draft the proposal",
        string? description = null,
        DateOnly? dueDate = null,
        DateTimeOffset? updatedAt = null)
        => TaskItem.Rehydrate(
            id ?? Guid.NewGuid(),
            userId ?? UserId,
            title,
            description,
            status,
            dueDate,
            Now,
            updatedAt ?? Now);
}
