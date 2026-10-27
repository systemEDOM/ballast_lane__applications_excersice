using System.Data.Common;
using System.Globalization;

namespace BallastLane.Infrastructure.Persistence;

/// <summary>
/// Applies the embedded migration scripts that have not been applied yet.
/// </summary>
public sealed class SchemaMigrator
{
    private const string CreateVersionTableSql = """
        CREATE TABLE IF NOT EXISTS schema_version (
            version    INTEGER NOT NULL,
            applied_at TEXT    NOT NULL
        );
        """;

    private const string ReadVersionSql = "SELECT COALESCE(MAX(version), 0) FROM schema_version;";

    private const string InsertVersionSql =
        "INSERT INTO schema_version (version, applied_at) VALUES (@version, @appliedAt);";

    private static readonly MigrationScript[] Scripts =
    [
        new MigrationScript(1, "001_initial.sql")
    ];

    private readonly IDbConnectionFactory _factory;

    public SchemaMigrator(IDbConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using var connection = _factory.Create();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = CreateVersionTableSql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var currentVersion = await ReadCurrentVersionAsync(connection, cancellationToken);

        foreach (var script in Scripts)
        {
            if (script.Version <= currentVersion)
            {
                continue;
            }

            await ApplyAsync(connection, script, cancellationToken);
        }
    }

    private static async Task<int> ReadCurrentVersionAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ReadVersionSql;

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static async Task ApplyAsync(
        DbConnection connection,
        MigrationScript script,
        CancellationToken cancellationToken)
    {
        var sql = ReadScript(script.FileName);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = InsertVersionSql;
            command.Parameters.Add(SqliteParameters.Create("@version", script.Version));
            command.Parameters.Add(SqliteParameters.Create(
                "@appliedAt",
                DateTimeOffset.UtcNow.ToString(SqliteValues.TimestampFormat, CultureInfo.InvariantCulture)));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static string ReadScript(string fileName)
    {
        var assembly = typeof(SchemaMigrator).Assembly;
        var suffix = $".Scripts.{fileName}";

        var resourceName = Array.Find(
            assembly.GetManifestResourceNames(),
            name => name.EndsWith(suffix, StringComparison.Ordinal));

        if (resourceName is null)
        {
            throw new InvalidOperationException($"The embedded migration script '{fileName}' was not found.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The embedded migration script '{fileName}' could not be opened.");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    private sealed record MigrationScript(int Version, string FileName);
}
