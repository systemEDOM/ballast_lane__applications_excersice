using BallastLane.Domain.Enums;
using BallastLane.Domain.Exceptions;

namespace BallastLane.Domain.Entities;

public sealed class TaskItem
{
    public const int TitleMinLength = 3;
    public const int TitleMaxLength = 120;
    public const int DescriptionMaxLength = 1000;

    private TaskItem(
        Guid id,
        Guid userId,
        string title,
        string? description,
        TaskItemStatus status,
        DateOnly? dueDate,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        UserId = userId;
        Title = title;
        Description = description;
        Status = status;
        DueDate = dueDate;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public TaskItemStatus Status { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static TaskItem Create(
        Guid userId,
        string title,
        string? description,
        DateOnly? dueDate,
        DateOnly today,
        DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("A task must belong to a user.");
        }

        EnsureDueDateIsNotInThePast(dueDate, today, current: null);

        return new TaskItem(
            Guid.NewGuid(),
            userId,
            NormalizeTitle(title),
            NormalizeDescription(description),
            TaskItemStatus.Todo,
            dueDate,
            now,
            now);
    }

    public static TaskItem Rehydrate(
        Guid id,
        Guid userId,
        string title,
        string? description,
        TaskItemStatus status,
        DateOnly? dueDate,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        => new(id, userId, title, description, status, dueDate, createdAt, updatedAt);

    public void UpdateDetails(string title, string? description, DateOnly? dueDate, DateOnly today, DateTimeOffset now)
    {
        EnsureDueDateIsNotInThePast(dueDate, today, DueDate);
        Title = NormalizeTitle(title);
        Description = NormalizeDescription(description);
        DueDate = dueDate;
        UpdatedAt = now;
    }

    public void ChangeStatus(TaskItemStatus next, DateTimeOffset now)
    {
        if (next == Status)
        {
            throw new DomainException($"The task is already in the {Status} status.");
        }

        var allowed = Status switch
        {
            TaskItemStatus.Todo => next is TaskItemStatus.InProgress or TaskItemStatus.Done,
            TaskItemStatus.InProgress => next is TaskItemStatus.Done,
            _ => false
        };

        if (!allowed)
        {
            throw new DomainException($"A task cannot move from {Status} to {next}.");
        }

        Status = next;
        UpdatedAt = now;
    }

    public bool IsOwnedBy(Guid userId) => UserId == userId;

    private static string NormalizeTitle(string title)
    {
        var candidate = (title ?? string.Empty).Trim();

        if (candidate.Length is < TitleMinLength or > TitleMaxLength)
        {
            throw new DomainException(
                $"Title must be between {TitleMinLength} and {TitleMaxLength} characters.");
        }

        return candidate;
    }

    private static string? NormalizeDescription(string? description)
    {
        var candidate = description?.Trim();

        if (string.IsNullOrEmpty(candidate))
        {
            return null;
        }

        if (candidate.Length > DescriptionMaxLength)
        {
            throw new DomainException($"Description must be at most {DescriptionMaxLength} characters.");
        }

        return candidate;
    }

    private static void EnsureDueDateIsNotInThePast(DateOnly? dueDate, DateOnly today, DateOnly? current)
    {
        if (dueDate is null || dueDate.Value >= today || dueDate == current)
        {
            return;
        }

        throw new DomainException("Due date cannot be in the past.");
    }
}
