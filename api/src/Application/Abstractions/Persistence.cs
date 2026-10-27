using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;

namespace BallastLane.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task AddAsync(User user, CancellationToken cancellationToken);
}

public interface ITaskRepository
{
    Task<IReadOnlyList<TaskItem>> ListByUserAsync(Guid userId, TaskItemStatus? status, CancellationToken cancellationToken);

    Task<TaskItem?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(TaskItem task, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(TaskItem task, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken);
}
