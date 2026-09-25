using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using PingRunner.Core.History;
using PingRunner.Infrastructure.History;
using PingRunner.Infrastructure.Tests.Fakes;

namespace PingRunner.Infrastructure.Tests.History;

public sealed class SqliteMigratorTests
{
    [Fact]
    public void BuiltIn_ChainStartsAtOneAndHasNoGaps()
    {
        var migrator = SqliteMigrator.BuiltIn();

        Assert.Equal(Enumerable.Range(1, migrator.Migrations.Count), migrator.Migrations.Select(migration => migration.Number));
        Assert.Equal("0001_initial.sql", migrator.Migrations[0].FileName);
    }

    [Fact]
    public void BuiltIn_ChecksumIsTheShaOfTheFileInTheRepository()
    {
        // tools/migrations.py hashes the file on disk; the app must record the same value.
        var directory = Path.Combine(RepositoryRoot(), "src", "PingRunner.Infrastructure", "History", "Migrations");

        foreach (var migration in SqliteMigrator.BuiltIn().Migrations)
        {
            var bytes = File.ReadAllBytes(Path.Combine(directory, migration.FileName));
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), migration.Checksum);
        }
    }

    [Fact]
    public void Migrate_EmptyDatabase_CreatesTheSchemaAndRecordsEachFile()
    {
        using var folder = new TemporaryDirectory();
        using var connection = Open(folder.File("history.db"));
        var migrator = SqliteMigrator.BuiltIn();

        var applied = migrator.Migrate(connection);

        Assert.Equal(migrator.Migrations.Count, applied);
        Assert.Equal(["ping_attempts", "ping_runs", "schema_migrations", "speed_tests"], Tables(connection));
        Assert.Equal(0, migrator.Migrate(connection));
    }

    [Fact]
    public void Migrate_DatabaseFromANewerVersion_IsRefused()
    {
        using var folder = new TemporaryDirectory();
        using var connection = Open(folder.File("history.db"));
        var migrator = SqliteMigrator.BuiltIn();
        migrator.Migrate(connection);
        Execute(connection, "INSERT INTO schema_migrations (number, filename, checksum_sha256) VALUES (9999, '9999_future.sql', 'x')");

        var error = Assert.Throws<HistoryException>(() => migrator.Migrate(connection));

        Assert.Contains("newer Ping Runner", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Migrate_AppliedFileWasEdited_IsRefused()
    {
        using var folder = new TemporaryDirectory();
        using var connection = Open(folder.File("history.db"));
        var migrator = SqliteMigrator.BuiltIn();
        migrator.Migrate(connection);
        Execute(connection, "UPDATE schema_migrations SET checksum_sha256 = 'edited' WHERE number = 1");

        Assert.Throws<HistoryException>(() => migrator.Migrate(connection));
    }

    private static SqliteConnection Open(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static List<string> Tables(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name";
        using var reader = command.ExecuteReader();
        var tables = new List<string>();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PingRunner.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found above the test output.");
    }
}
