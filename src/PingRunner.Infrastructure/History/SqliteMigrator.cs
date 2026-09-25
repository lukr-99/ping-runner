using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using PingRunner.Core.History;

namespace PingRunner.Infrastructure.History;

/// <summary>
/// Applies the numbered SQL files embedded from <c>History/Migrations</c>, following CodePrint's rules
/// and its <c>tools/migrations.py</c>: contiguous numbers from 0001, one transaction per file, and a
/// <c>schema_migrations</c> table holding each file's number, name and SHA-256. A database that knows
/// a migration this build does not, or whose applied file differs from the one shipped, is refused
/// rather than repaired.
/// </summary>
public sealed partial class SqliteMigrator(IReadOnlyList<Migration> migrations)
{
    private const string ResourcePrefix = "PingRunner.History.Migrations.";

    public IReadOnlyList<Migration> Migrations { get; } = migrations;

    public int LatestNumber => Migrations[^1].Number;

    /// <summary>The migrations shipped in this assembly.</summary>
    public static SqliteMigrator BuiltIn()
    {
        var assembly = typeof(SqliteMigrator).Assembly;
        var migrations = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Select(name => Load(assembly, name))
            .OrderBy(migration => migration.Number)
            .ToList();

        for (var index = 0; index < migrations.Count; index++)
        {
            if (migrations[index].Number != index + 1)
            {
                throw new InvalidOperationException($"Migration {index + 1:0000} is missing; the chain must be contiguous from 0001.");
            }
        }

        return migrations.Count > 0
            ? new SqliteMigrator(migrations)
            : throw new InvalidOperationException("No history migrations are embedded.");
    }

    /// <summary>Applies every pending migration.</summary>
    /// <returns>How many were applied.</returns>
    /// <exception cref="HistoryException">The database belongs to a newer or different chain.</exception>
    public int Migrate(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var applied = Verify(connection);
        var count = 0;
        foreach (var migration in Migrations.Where(migration => !applied.Contains(migration.Number)))
        {
            using var transaction = connection.BeginTransaction();
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = migration.Sql;
                command.ExecuteNonQuery();
            }

            using (var record = connection.CreateCommand())
            {
                record.Transaction = transaction;
                record.CommandText = "INSERT INTO schema_migrations (number, filename, checksum_sha256) VALUES ($number, $filename, $checksum)";
                record.Parameters.AddWithValue("$number", migration.Number);
                record.Parameters.AddWithValue("$filename", migration.FileName);
                record.Parameters.AddWithValue("$checksum", migration.Checksum);
                record.ExecuteNonQuery();
            }

            transaction.Commit();
            count++;
        }

        return count;
    }

    /// <summary>Checks the applied migrations against the shipped ones without changing anything but the history table.</summary>
    /// <returns>The numbers already applied.</returns>
    public HashSet<int> Verify(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using (var create = connection.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE IF NOT EXISTS schema_migrations (
                    number INTEGER PRIMARY KEY,
                    filename TEXT NOT NULL UNIQUE,
                    checksum_sha256 TEXT NOT NULL,
                    applied_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                )
                """;
            create.ExecuteNonQuery();
        }

        var applied = new HashSet<int>();
        using var query = connection.CreateCommand();
        query.CommandText = "SELECT number, filename, checksum_sha256 FROM schema_migrations ORDER BY number";
        using var reader = query.ExecuteReader();
        while (reader.Read())
        {
            var number = reader.GetInt32(0);
            var known = Migrations.FirstOrDefault(migration => migration.Number == number);
            if (known is null)
            {
                throw new HistoryException(
                    $"The history was written by a newer Ping Runner (migration {number:0000}). Update Ping Runner to open it.");
            }

            if (known.FileName != reader.GetString(1) || known.Checksum != reader.GetString(2))
            {
                throw new HistoryException($"The history's migration {number:0000} does not match this version of Ping Runner.");
            }

            applied.Add(number);
        }

        return applied;
    }

    private static Migration Load(Assembly assembly, string resourceName)
    {
        var fileName = resourceName[ResourcePrefix.Length..];
        var match = FileNamePattern().Match(fileName);
        if (!match.Success)
        {
            throw new InvalidOperationException($"Migration file names look like 0001_description.sql: {fileName}");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing migration resource {resourceName}.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var bytes = buffer.ToArray();
        var sql = new UTF8Encoding(false).GetString(bytes).TrimStart('﻿');
        return new Migration(
            int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture),
            fileName,
            sql,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    [GeneratedRegex(@"^(?<number>\d{4})_[a-z0-9_]+\.sql$")]
    private static partial Regex FileNamePattern();
}
