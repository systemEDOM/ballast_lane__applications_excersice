using System.Globalization;
using BallastLane.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Shouldly;
using Xunit;

namespace BallastLane.InfrastructureTests;

public sealed class SchemaMigratorTests : SqliteTestBase
{
    [Fact]
    public async Task MigrateAsync_FreshDatabase_CreatesSchemaAndVersionRow()
    {
        await MigrateAsync();

        (await ScalarLongAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('users', 'tasks', 'schema_version');"))
            .ShouldBe(3L);

        (await ScalarLongAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'ix_tasks_user_id';"))
            .ShouldBe(1L);

        (await ScalarLongAsync("SELECT MAX(version) FROM schema_version;")).ShouldBe(1L);
        (await ScalarLongAsync("SELECT COUNT(*) FROM schema_version;")).ShouldBe(1L);
        (await ScalarLongAsync("PRAGMA foreign_keys;")).ShouldBe(1L);
    }

    [Fact]
    public async Task MigrateAsync_RunTwice_AppliesScriptOnlyOnce()
    {
        await MigrateAsync();
        var appliedAt = await ScalarStringAsync("SELECT applied_at FROM schema_version WHERE version = 1;");
        appliedAt.ShouldNotBeNull();

        await MigrateAsync();

        (await ScalarLongAsync("SELECT COUNT(*) FROM schema_version;")).ShouldBe(1L);
        (await ScalarLongAsync("SELECT MAX(version) FROM schema_version;")).ShouldBe(1L);
        (await ScalarStringAsync("SELECT applied_at FROM schema_version WHERE version = 1;")).ShouldBe(appliedAt);
    }

    [Fact]
    public async Task MigrateAsync_MissingDirectory_ThrowsAndCreatesNothing()
    {
        var missingDirectory = Path.Combine(
            Path.GetTempPath(),
            "bl-missing-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
            "nested");

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(missingDirectory, "app.db"),
            Pooling = false
        }.ToString();

        var migrator = new SchemaMigrator(new SqliteConnectionFactory(connectionString));

        await Should.ThrowAsync<SqliteException>(() => migrator.MigrateAsync(CancellationToken.None));

        Directory.Exists(missingDirectory).ShouldBeFalse();
    }
}
