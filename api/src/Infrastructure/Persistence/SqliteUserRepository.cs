using System.Data.Common;
using System.Globalization;
using BallastLane.Application.Abstractions;
using BallastLane.Domain.Entities;

namespace BallastLane.Infrastructure.Persistence;

public sealed class SqliteUserRepository : IUserRepository
{
    private const string FindByEmailSql = """
        SELECT id, email, display_name, password_hash, password_salt, password_iterations, created_at
        FROM users
        WHERE email = @email
        LIMIT 1;
        """;

    private const string FindByIdSql = """
        SELECT id, email, display_name, password_hash, password_salt, password_iterations, created_at
        FROM users
        WHERE id = @id
        LIMIT 1;
        """;

    private const string EmailExistsSql = "SELECT EXISTS (SELECT 1 FROM users WHERE email = @email);";

    private const string InsertSql = """
        INSERT INTO users (id, email, display_name, password_hash, password_salt, password_iterations, created_at)
        VALUES (@id, @email, @displayName, @passwordHash, @passwordSalt, @passwordIterations, @createdAt);
        """;

    private readonly IDbConnectionFactory _factory;

    public SqliteUserRepository(IDbConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);

        await using var connection = _factory.Create();
        await using var command = connection.CreateCommand();
        command.CommandText = FindByEmailSql;
        command.Parameters.Add(SqliteParameters.Create("@email", normalizedEmail.Trim().ToLowerInvariant()));

        return await ReadSingleAsync(command, cancellationToken);
    }

    public async Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = _factory.Create();
        await using var command = connection.CreateCommand();
        command.CommandText = FindByIdSql;
        command.Parameters.Add(SqliteParameters.Create("@id", SqliteValues.ToText(id)));

        return await ReadSingleAsync(command, cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);

        await using var connection = _factory.Create();
        await using var command = connection.CreateCommand();
        command.CommandText = EmailExistsSql;
        command.Parameters.Add(SqliteParameters.Create("@email", normalizedEmail.Trim().ToLowerInvariant()));

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(value, CultureInfo.InvariantCulture) == 1L;
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using var connection = _factory.Create();
        await using var command = connection.CreateCommand();
        command.CommandText = InsertSql;
        command.Parameters.Add(SqliteParameters.Create("@id", SqliteValues.ToText(user.Id)));
        command.Parameters.Add(SqliteParameters.Create("@email", user.Email));
        command.Parameters.Add(SqliteParameters.Create("@displayName", user.DisplayName));
        command.Parameters.Add(SqliteParameters.Create("@passwordHash", user.PasswordHash));
        command.Parameters.Add(SqliteParameters.Create("@passwordSalt", user.PasswordSalt));
        command.Parameters.Add(SqliteParameters.Create("@passwordIterations", user.PasswordIterations));
        command.Parameters.Add(SqliteParameters.Create("@createdAt", SqliteValues.ToText(user.CreatedAt)));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<User?> ReadSingleAsync(DbCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadUser(reader) : null;
    }

    private static User ReadUser(DbDataReader reader)
        => User.Rehydrate(
            SqliteValues.ReadGuid(reader, "id"),
            SqliteValues.ReadString(reader, "email"),
            SqliteValues.ReadString(reader, "display_name"),
            SqliteValues.ReadString(reader, "password_hash"),
            SqliteValues.ReadString(reader, "password_salt"),
            SqliteValues.ReadInt32(reader, "password_iterations"),
            SqliteValues.ReadTimestamp(reader, "created_at"));
}
