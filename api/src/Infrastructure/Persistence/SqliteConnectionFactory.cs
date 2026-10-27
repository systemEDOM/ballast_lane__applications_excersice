using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace BallastLane.Infrastructure.Persistence;

/// <summary>
/// Opens SQLite connections with foreign key enforcement switched on for the connection lifetime.
/// </summary>
public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private const string ForeignKeysPragma = "PRAGMA foreign_keys = ON;";

    private readonly string _connectionString;

    public SqliteConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQLite connection string is required.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public DbConnection Create()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = ForeignKeysPragma;
        command.ExecuteNonQuery();

        return connection;
    }
}
