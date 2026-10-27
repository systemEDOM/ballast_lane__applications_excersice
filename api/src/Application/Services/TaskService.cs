using BallastLane.Application.Abstractions;
using BallastLane.Application.Common;
using BallastLane.Application.Dtos;
using BallastLane.Application.Validation;
using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;
using BallastLane.Domain.Exceptions;

namespace BallastLane.Application.Services;

public sealed class TaskService
{
    private const string TaskNotFoundMessage = "Task not found.";

    private readonly ITaskRepository _tasks;
    private readonly IClock _clock;

    public TaskService(ITaskRepository tasks, IClock clock)
    {
        _tasks = tasks;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<TaskDto>>> ListAsync(Guid userId, string? status, CancellationToken ct)
    {
        TaskItemStatus? filter = null;

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!RequestValidator.TryParseStatus(status, out var parsed))
            {
                return Result<IReadOnlyList<TaskDto>>.Fail(
                    Error.Validation(RequestValidator.StatusField, RequestValidator.StatusUnknownMessage));
            }

            filter = parsed;
        }

        var tasks = await _tasks.ListByUserAsync(userId, filter, ct);
        var result = new List<TaskDto>(tasks.Count);

        foreach (var task in tasks)
        {
            result.Add(ToDto(task));
        }

        return Result<IReadOnlyList<TaskDto>>.Ok(result);
    }

    public async Task<Result<TaskDto>> GetAsync(Guid userId, Guid taskId, CancellationToken ct)
    {
        var task = await _tasks.FindByIdAsync(taskId, ct);

        if (task is null || !task.IsOwnedBy(userId))
        {
            return Result<TaskDto>.Fail(Error.NotFound(TaskNotFoundMessage));
        }

        return Result<TaskDto>.Ok(ToDto(task));
    }

    public async Task<Result<TaskDto>> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken ct)
    {
        var errors = RequestValidator.ValidateTaskDetails(
            request.Title,
            request.Description,
            request.DueDate,
            _clock.Today,
            currentDueDate: null);

        if (errors.Count > 0)
        {
            return Result<TaskDto>.Fail(Error.Validation(errors));
        }

        var task = TaskItem.Create(
            userId,
            request.Title!,
            request.Description,
            request.DueDate,
            _clock.Today,
            _clock.UtcNow);

        await _tasks.AddAsync(task, ct);

        return Result<TaskDto>.Ok(ToDto(task));
    }

    public async Task<Result<TaskDto>> UpdateAsync(Guid userId, Guid taskId, UpdateTaskRequest request, CancellationToken ct)
    {
        var task = await _tasks.FindByIdAsync(taskId, ct);

        if (task is null || !task.IsOwnedBy(userId))
        {
            return Result<TaskDto>.Fail(Error.NotFound(TaskNotFoundMessage));
        }

        var errors = RequestValidator.ValidateTaskDetails(
            request.Title,
            request.Description,
            request.DueDate,
            _clock.Today,
            task.DueDate);

        if (errors.Count > 0)
        {
            return Result<TaskDto>.Fail(Error.Validation(errors));
        }

        task.UpdateDetails(request.Title!, request.Description, request.DueDate, _clock.Today, _clock.UtcNow);

        if (!await _tasks.UpdateAsync(task, ct))
        {
            return Result<TaskDto>.Fail(Error.NotFound(TaskNotFoundMessage));
        }

        return Result<TaskDto>.Ok(ToDto(task));
    }

    public async Task<Result<TaskDto>> ChangeStatusAsync(
        Guid userId,
        Guid taskId,
        ChangeTaskStatusRequest request,
        CancellationToken ct)
    {
        var task = await _tasks.FindByIdAsync(taskId, ct);

        if (task is null || !task.IsOwnedBy(userId))
        {
            return Result<TaskDto>.Fail(Error.NotFound(TaskNotFoundMessage));
        }

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            return Result<TaskDto>.Fail(
                Error.Validation(RequestValidator.StatusField, RequestValidator.StatusRequiredMessage));
        }

        if (!RequestValidator.TryParseStatus(request.Status, out var next))
        {
            return Result<TaskDto>.Fail(
                Error.Validation(RequestValidator.StatusField, RequestValidator.StatusUnknownMessage));
        }

        try
        {
            task.ChangeStatus(next, _clock.UtcNow);
        }
        catch (DomainException exception)
        {
            return Result<TaskDto>.Fail(Error.Validation(RequestValidator.StatusField, exception.Message));
        }

        if (!await _tasks.UpdateAsync(task, ct))
        {
            return Result<TaskDto>.Fail(Error.NotFound(TaskNotFoundMessage));
        }

        return Result<TaskDto>.Ok(ToDto(task));
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid taskId, CancellationToken ct)
    {
        if (!await _tasks.DeleteAsync(taskId, userId, ct))
        {
            return Result.Fail(Error.NotFound(TaskNotFoundMessage));
        }

        return Result.Ok();
    }

    private static TaskDto ToDto(TaskItem task)
        => new(
            task.Id,
            task.Title,
            task.Description,
            task.Status.ToString(),
            task.DueDate,
            task.CreatedAt,
            task.UpdatedAt);
}
