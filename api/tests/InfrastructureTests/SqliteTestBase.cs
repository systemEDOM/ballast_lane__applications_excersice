using System.Globalization;
using BallastLane.Domain.Entities;
using BallastLane.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace BallastLane.InfrastructureTests;

/// <summary>
/// Gives every test its own throwaway SQLite database file.
/// </summary>
public abstract class SqliteTestBase : IDisposable
{
    protected SqliteTestBase()
    {
        DatabasePath = Path.Combine(
            Path.GetTempPath(),
            "bl-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".db");

        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Pooling = false
        }.ToString();

        Factory = new SqliteConnectionFactory(ConnectionString);
    }

    protected string DatabasePath { get; }

    protected string ConnectionString { get; }

    protected IDbConnectionFactory Factory { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        SqliteConnection.ClearAllPools();

        DeleteDatabaseFile(DatabasePath);
        DeleteDatabaseFile(DatabasePath + "-wal");
        DeleteDatabaseFile(DatabasePath + "-shm");
    }

    protected async Task MigrateAsync()
    {
        var migrator = new SchemaMigrator(Factory);
        await migrator.MigrateAsync(CancellationToken.None);
    }

    protected async Task<User> AddUserAsync(string? email = null, string displayName = "Test User")
    {
        var user = User.Create(
            email ?? "user-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + "@example.test",
            displayName,
            "stored-hash",
            "stored-salt",
            210_000,
            DateTimeOffset.Parse("2026-01-05T08:00:00.0000000+00:00", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

        await new SqliteUserRepository(Factory).AddAsync(user, CancellationToken.None);

        return user;
    }

    protected async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = Factory.Create();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        return await command.ExecuteScalarAsync(CancellationToken.None);
    }

    protected async Task<long> ScalarLongAsync(string sql)
    {
        var value = await ScalarAsync(sql);
        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    protected async Task<string?> ScalarStringAsync(string sql)
    {
        var value = await ScalarAsync(sql);
        return value as string;
    }

    private static void DeleteDatabaseFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
