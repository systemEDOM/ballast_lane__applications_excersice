namespace BallastLane.Application.Dtos;

public sealed record TaskDto(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateTaskRequest(string? Title, string? Description, DateOnly? DueDate);

public sealed record UpdateTaskRequest(string? Title, string? Description, DateOnly? DueDate);

public sealed record ChangeTaskStatusRequest(string? Status);
