using System.Data.Common;
using BallastLane.Application.Abstractions;
using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;

namespace BallastLane.Infrastructure.Persistence;

public sealed class SqliteTaskRepository : ITaskRepository
{
    private const string ListByUserSql = """
        SELECT id, user_id, title, description, status, due_date, created_at, updated_at
        FROM tasks
        WHERE user_id = @userId
          AND (@status IS NULL OR status = @status)
        ORDER BY due_date IS NULL, due_date, created_at;
        """;

    private const string FindByIdSql = """
        SELECT id, user_id, title, description, status, due_date, created_at, updated_at
        FROM tasks
        WHERE id = @id
        LIMIT 1;
        """;

    private const string InsertSql = """
        INSERT INTO tasks (id, user_id, title, description, status, due_date, created_at, updated_at)
        VALUES (@id, @userId, @title, @description, @status, @dueDate, @createdAt, @updatedAt);
        """;

    private const string UpdateSql = """
        UPDATE tasks
        SET title = @title,
            description = @description,
            status = @status,
            due_date = @dueDate,
            updated_at = @updatedAt
        WHERE id = @id
          AND user_id = @userId;
        """;

    private const string DeleteSql = "DELETE FROM tasks WHERE id = @id AND user_id = @userId;";

    private readonly IDbConnectionFactory _factory;

    public SqliteTaskRepository(IDbConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<IReadOnlyList<TaskItem>> ListByUserAsync(
        Guid userId,
        TaskItemStatus? status,
        CancellationToken cancellationToken)
    {
        await using var connection = _factory.Create();
        await using var command = connection.CreateCommand();
        command.CommandText = ListByUserSql;
        command.Parameters.Add(SqliteParameters.Create("@userId", SqliteValues.ToText(userId)));
        command.Parameters.Add(SqliteParameters.Create(
            "@status",
            status is null ? null : SqliteValues.ToText(status.Value)));

        var tasks = new List<TaskItem>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tasks.Add(ReadTask(reader));
        }

        return tasks;
    }

    public async Task<TaskItem?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = _factory.Create();
        await using var command = connection.CreateCommand();
        command.CommandText = FindByIdSql;
        command.Parameters.Add(SqliteParameters.Create("@id", SqliteValues.ToText(id)));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadTask(reader) : null;
    }

    public async Task AddAsync(TaskItem task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);

        await using var connection = _factory.Create();
        await using var command = connection.CreateCommand();
        command.CommandText = InsertSql;
        AddDetailsParameters(command, task);
        command.Parameters.Add(SqliteParameters.Create("@createdAt", SqliteValues.ToText(task.CreatedAt)));
        command.Parameters.Add(SqliteParameters.Create("@updatedAt", SqliteValues.ToText(task.UpdatedAt)));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(TaskItem task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);

        await using var connection = _factory.Create();
        await using var command = connection.CreateCommand();
        command.CommandText = UpdateSql;
        AddDetailsParameters(command, task);
        command.Parameters.Add(SqliteParameters.Create("@updatedAt", SqliteValues.ToText(task.UpdatedAt)));

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = _factory.Create();
        await using var command = connection.CreateCommand();
        command.CommandText = DeleteSql;
        command.Parameters.Add(SqliteParameters.Create("@id", SqliteValues.ToText(id)));
        command.Parameters.Add(SqliteParameters.Create("@userId", SqliteValues.ToText(userId)));

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected == 1;
    }

    private static void AddDetailsParameters(DbCommand command, TaskItem task)
    {
        command.Parameters.Add(SqliteParameters.Create("@id", SqliteValues.ToText(task.Id)));
        command.Parameters.Add(SqliteParameters.Create("@userId", SqliteValues.ToText(task.UserId)));
        command.Parameters.Add(SqliteParameters.Create("@title", task.Title));
        command.Parameters.Add(SqliteParameters.Create("@description", task.Description));
        command.Parameters.Add(SqliteParameters.Create("@status", SqliteValues.ToText(task.Status)));
        command.Parameters.Add(SqliteParameters.Create(
            "@dueDate",
            task.DueDate is null ? null : SqliteValues.ToText(task.DueDate.Value)));
    }

    private static TaskItem ReadTask(DbDataReader reader)
        => TaskItem.Rehydrate(
            SqliteValues.ReadGuid(reader, "id"),
            SqliteValues.ReadGuid(reader, "user_id"),
            SqliteValues.ReadString(reader, "title"),
            SqliteValues.ReadNullableString(reader, "description"),
            SqliteValues.ReadStatus(reader, "status"),
            SqliteValues.ReadNullableDate(reader, "due_date"),
            SqliteValues.ReadTimestamp(reader, "created_at"),
            SqliteValues.ReadTimestamp(reader, "updated_at"));
}
