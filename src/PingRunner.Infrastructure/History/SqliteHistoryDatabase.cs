using System.Globalization;
using Microsoft.Data.Sqlite;
using PingRunner.Core.History;

namespace PingRunner.Infrastructure.History;

/// <summary>
/// The history file (<c>history.db</c>, SQLite) and everything done to it as a whole. Every operation
/// opens its own connection on the thread pool and holds the file exclusively while it runs, so a
/// restore can swap the file safely. Connections are not pooled, so nothing keeps the file open
/// between operations. SQLite errors surface as <see cref="HistoryException"/>. Disposing waits for the
/// operation in progress and closes the history; later calls fail with a <see cref="HistoryException"/>
/// instead of touching the file.
/// </summary>
public sealed class SqliteHistoryDatabase : IHistoryMaintenance, IDisposable
{
    private static readonly TimeSpan CloseWait = TimeSpan.FromSeconds(3);

    // Never disposed: an operation still finishing when the app closes must be able to release it.
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly SqliteMigrator migrator;
    private volatile bool closed;

    private SqliteHistoryDatabase(string path, SqliteMigrator migrator)
    {
        Location = Path.GetFullPath(path);
        this.migrator = migrator;
    }

    public string Location { get; }

    public string? Problem { get; private set; }

    /// <summary>
    /// Opens the history at <paramref name="path"/>, creating or migrating it. A file SQLite cannot read
    /// is moved aside (the name is in <see cref="Problem"/>) and a new history starts.
    /// </summary>
    /// <exception cref="HistoryException">The history was written by a newer Ping Runner; it is left untouched.</exception>
    public static SqliteHistoryDatabase Open(string path, SqliteMigrator? migrator = null)
    {
        var database = new SqliteHistoryDatabase(path, migrator ?? SqliteMigrator.BuiltIn());
        try
        {
            database.Initialize();
        }
        catch (SqliteException) when (File.Exists(database.Location))
        {
            var aside = Path.Combine(
                Path.GetDirectoryName(database.Location)!,
                $"history.unreadable-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.db");
            File.Move(database.Location, aside);
            database.Problem = $"The history file could not be read, so a new one was started. The old file is kept as {Path.GetFileName(aside)}.";
            database.Initialize();
        }

        return database;
    }

    public Task<HistoryStorageInfo> GetInfoAsync(CancellationToken cancellationToken) =>
        RunAsync(connection => Info(connection, Location), cancellationToken);

    public Task BackupAsync(string destinationPath, CancellationToken cancellationToken) => RunAsync(
        connection =>
        {
            var destination = Path.GetFullPath(destinationPath);
            var temporary = destination + ".tmp";
            File.Delete(temporary);
            using (var target = Connect(temporary))
            {
                connection.BackupDatabase(target);
            }

            File.Move(temporary, destination, overwrite: true);
            return true;
        },
        cancellationToken);

    public async Task<HistoryStorageInfo> RestoreAsync(string sourcePath, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var candidate = Path.Combine(Path.GetDirectoryName(Location)!, $"history.restore-{Guid.NewGuid():N}.db");
        try
        {
            ThrowIfClosed();
            return await Task.Run(() => Restore(Path.GetFullPath(sourcePath), candidate), cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception)
        {
            throw new HistoryException($"{Path.GetFileName(sourcePath)} is not a readable Ping Runner history ({exception.Message}).", exception);
        }
        catch (IOException exception)
        {
            throw new HistoryException($"The backup could not be restored: {exception.Message}", exception);
        }
        finally
        {
            File.Delete(candidate);
            gate.Release();
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken) => RunAsync(
        connection =>
        {
            using (var transaction = connection.BeginTransaction())
            {
                Execute(connection, transaction, "DELETE FROM ping_attempts; DELETE FROM ping_runs; DELETE FROM speed_tests;");
                transaction.Commit();
            }

            Execute(connection, null, "VACUUM;");
            return true;
        },
        cancellationToken);

    public void Dispose()
    {
        var entered = gate.Wait(CloseWait);
        closed = true;
        if (entered)
        {
            gate.Release();
        }
    }

    /// <summary>Runs <paramref name="work"/> on the thread pool with the file to itself.</summary>
    internal async Task<T> RunAsync<T>(Func<SqliteConnection, T> work, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfClosed();
            return await Task.Run(
                () =>
                {
                    using var connection = Connect(Location);
                    return work(connection);
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception)
        {
            throw new HistoryException($"The history could not be read or written: {exception.Message}", exception);
        }
        catch (IOException exception)
        {
            throw new HistoryException($"The history file could not be used: {exception.Message}", exception);
        }
        finally
        {
            gate.Release();
        }
    }

    internal static void Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private void ThrowIfClosed()
    {
        if (closed)
        {
            throw new HistoryException("The history is closed.");
        }
    }

    private static SqliteConnection Connect(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString());
        connection.Open();
        Execute(connection, null, "PRAGMA foreign_keys = ON;");
        return connection;
    }

    private static HistoryStorageInfo Info(SqliteConnection connection, string path)
    {
        long Count(string table)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table}";
            return (long)command.ExecuteScalar()!;
        }

        return new HistoryStorageInfo(
            (int)Count("ping_runs"),
            Count("ping_attempts"),
            (int)Count("speed_tests"),
            File.Exists(path) ? new FileInfo(path).Length : 0);
    }

    private static void CheckIntegrity(SqliteConnection connection, string name)
    {
        using var integrity = connection.CreateCommand();
        integrity.CommandText = "PRAGMA integrity_check";
        if (integrity.ExecuteScalar() as string != "ok")
        {
            throw new HistoryException($"{name} is damaged (SQLite integrity check failed).");
        }

        using var foreignKeys = connection.CreateCommand();
        foreignKeys.CommandText = "PRAGMA foreign_key_check";
        using var reader = foreignKeys.ExecuteReader();
        if (reader.Read())
        {
            throw new HistoryException($"{name} has attempts that belong to no run.");
        }
    }

    private void Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Location)!);
        using var connection = Connect(Location);
        migrator.Migrate(connection);
    }

    // Checks and upgrades a copy of the backup, and only then puts it in place of the current file,
    // which is kept as history.before-restore.db.
    private HistoryStorageInfo Restore(string source, string candidate)
    {
        var name = Path.GetFileName(source);
        File.Copy(source, candidate, overwrite: true);
        using (var connection = Connect(candidate))
        {
            using (var tables = connection.CreateCommand())
            {
                tables.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('schema_migrations', 'ping_runs')";
                if ((long)tables.ExecuteScalar()! != 2)
                {
                    throw new HistoryException($"{name} is not a Ping Runner history.");
                }
            }

            CheckIntegrity(connection, name);
            migrator.Migrate(connection);
            CheckIntegrity(connection, name);
        }

        if (File.Exists(Location))
        {
            File.Copy(Location, Path.Combine(Path.GetDirectoryName(Location)!, "history.before-restore.db"), overwrite: true);
        }

        File.Move(candidate, Location, overwrite: true);
        using var restored = Connect(Location);
        return Info(restored, Location);
    }
}
