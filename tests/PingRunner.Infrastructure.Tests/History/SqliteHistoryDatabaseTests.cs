using Microsoft.Data.Sqlite;
using PingRunner.Core.History;
using PingRunner.Infrastructure.History;
using PingRunner.Infrastructure.Tests.Fakes;

namespace PingRunner.Infrastructure.Tests.History;

public sealed class SqliteHistoryDatabaseTests
{
    [Fact]
    public async Task Backup_ClearThenRestore_BringsEverythingBack()
    {
        using var history = new HistoryFixture();
        var token = TestContext.Current.CancellationToken;
        var id = await history.Runs.StartRunAsync(HistoryFixture.Settings(), HistoryFixture.Start, token);
        await history.Runs.AppendAttemptsAsync(id, 0, HistoryFixture.Attempts(20), token);
        await history.SpeedTests.SaveAsync(SqliteSpeedTestHistoryTests.Minimal(), token);
        var backup = history.File("backup.db");

        await history.Database.BackupAsync(backup, token);
        await history.Database.ClearAsync(token);
        Assert.Equal(0, (await history.Database.GetInfoAsync(token)).Runs);
        var restored = await history.Database.RestoreAsync(backup, token);

        Assert.Equal(1, restored.Runs);
        Assert.Equal(20, restored.Attempts);
        Assert.Equal(1, restored.SpeedTests);
        Assert.Equal(20, (await history.Runs.LoadAttemptsAsync(id, token)).Count);
        Assert.True(File.Exists(history.File("history.before-restore.db")));
    }

    [Fact]
    public async Task Restore_FileIsNotADatabase_LeavesTheHistoryAlone()
    {
        using var history = new HistoryFixture();
        var token = TestContext.Current.CancellationToken;
        await history.Runs.StartRunAsync(HistoryFixture.Settings(), HistoryFixture.Start, token);
        var bogus = history.File("notes.db");
        await File.WriteAllTextAsync(bogus, "these are not the pings you are looking for", token);

        await Assert.ThrowsAsync<HistoryException>(() => history.Database.RestoreAsync(bogus, token));

        Assert.Equal(1, (await history.Database.GetInfoAsync(token)).Runs);
    }

    [Fact]
    public async Task Restore_SqliteFileOfAnotherApp_IsRefused()
    {
        using var history = new HistoryFixture();
        var token = TestContext.Current.CancellationToken;
        var other = history.File("other.db");
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = other, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE notes (text TEXT)";
            command.ExecuteNonQuery();
        }

        var error = await Assert.ThrowsAsync<HistoryException>(() => history.Database.RestoreAsync(other, token));

        Assert.Contains("not a Ping Runner history", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dispose_WhileAnOperationRuns_LetsItFinishThenRefusesMore()
    {
        using var history = new HistoryFixture();
        var token = TestContext.Current.CancellationToken;
        using var started = new ManualResetEventSlim();
        var running = history.Database.RunAsync(
            _ =>
            {
                started.Set();
                Thread.Sleep(300);
                return 42;
            },
            token);
        started.Wait(token);

        history.Database.Dispose();

        Assert.Equal(42, await running);
        await Assert.ThrowsAsync<HistoryException>(() => history.Database.GetInfoAsync(token));
    }

    [Fact]
    public void Open_UnreadableFile_IsSetAsideAndANewHistoryStarts()
    {
        using var folder = new TemporaryDirectory();
        var path = folder.File("history.db");
        File.WriteAllBytes(path, [.. Enumerable.Repeat((byte)0x5A, 4096)]);

        using var database = SqliteHistoryDatabase.Open(path);

        Assert.NotNull(database.Problem);
        Assert.Single(Directory.GetFiles(folder.Path, "history.unreadable-*.db"));
    }

    [Fact]
    public void Open_HistoryFromANewerVersion_IsRefusedAndLeftAlone()
    {
        using var folder = new TemporaryDirectory();
        var path = folder.File("history.db");
        SqliteHistoryDatabase.Open(path).Dispose();
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO schema_migrations (number, filename, checksum_sha256) VALUES (9999, '9999_future.sql', 'x')";
            command.ExecuteNonQuery();
        }

        var before = File.ReadAllBytes(path);

        Assert.Throws<HistoryException>(() => SqliteHistoryDatabase.Open(path));
        Assert.Equal(before, File.ReadAllBytes(path));
    }
}
