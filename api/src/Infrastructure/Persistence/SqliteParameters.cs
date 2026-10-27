using Microsoft.Data.Sqlite;

namespace BallastLane.Infrastructure.Persistence;

internal static class SqliteParameters
{
    public static SqliteParameter Create(string name, object? value)
        => new(name, value ?? DBNull.Value);
}
