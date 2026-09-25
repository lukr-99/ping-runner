using Microsoft.Data.Sqlite;
using PingRunner.Core.History;
using PingRunner.Core.Pinging;

namespace PingRunner.Infrastructure.History;

/// <summary>Ping runs and their attempts in the history database.</summary>
public sealed class SqlitePingRunHistory(SqliteHistoryDatabase database) : IPingRunHistory
{
    public Task<long> StartRunAsync(PingRunSettings settings, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return database.RunAsync(
            connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO ping_runs (target_host, started_at_ms, started_offset_min, interval_ms, timeout_ms, planned_duration_ms, outcome)
                    VALUES ($host, $started, $offset, $interval, $timeout, $planned, 'running');
                    SELECT last_insert_rowid();
                    """;
                command.Parameters.AddWithValue("$host", settings.TargetHost);
                command.Parameters.AddWithValue("$started", startedAt.ToUnixTimeMilliseconds());
                command.Parameters.AddWithValue("$offset", (int)startedAt.Offset.TotalMinutes);
                command.Parameters.AddWithValue("$interval", (long)settings.Interval.TotalMilliseconds);
                command.Parameters.AddWithValue("$timeout", (long)settings.Timeout.TotalMilliseconds);
                command.Parameters.AddWithValue("$planned", settings.Duration is { } duration ? (long)duration.TotalMilliseconds : DBNull.Value);
                return (long)command.ExecuteScalar()!;
            },
            cancellationToken);
    }

    public Task AppendAttemptsAsync(long runId, int firstSequence, IReadOnlyList<PingAttempt> attempts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        return database.RunAsync(
            connection =>
            {
                using var transaction = connection.BeginTransaction();
                InsertAttempts(connection, transaction, runId, firstSequence, attempts);

                // Live counts, so a run still going shows how far it got.
                using var counts = connection.CreateCommand();
                counts.Transaction = transaction;
                counts.CommandText = """
                    UPDATE ping_runs SET
                        sent = (SELECT COUNT(*) FROM ping_attempts WHERE run_id = $run),
                        received = (SELECT COUNT(*) FROM ping_attempts WHERE run_id = $run AND is_success = 1)
                    WHERE id = $run
                    """;
                counts.Parameters.AddWithValue("$run", runId);
                counts.ExecuteNonQuery();

                transaction.Commit();
                return true;
            },
            cancellationToken);
    }

    public Task FinishRunAsync(long runId, RunOutcome outcome, DateTimeOffset? endedAt, RunSummary summary, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return database.RunAsync(
            connection =>
            {
                WriteSummary(connection, null, runId, outcome, endedAt, summary);
                return true;
            },
            cancellationToken);
    }

    public Task<long> ImportRunAsync(string sourceName, IReadOnlyList<PingAttempt> attempts, RunSummary summary, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(attempts);
        ArgumentNullException.ThrowIfNull(summary);
        if (attempts.Count == 0)
        {
            throw new ArgumentException("An imported run needs at least one ping.", nameof(attempts));
        }

        var interval = PingSpacing.Typical(attempts) is { } typical && typical >= TimeSpan.FromMilliseconds(1) ? typical : TimeSpan.FromSeconds(1);
        return database.RunAsync(
            connection =>
            {
                using var transaction = connection.BeginTransaction();
                using var command = connection.CreateCommand();
                command.Transaction = transaction;

                // The interval is also the timeout: a file does not say how long each ping waited.
                command.CommandText = """
                    INSERT INTO ping_runs (target_host, started_at_ms, started_offset_min, interval_ms, timeout_ms, outcome, source, source_name)
                    VALUES ($host, $started, $offset, $interval, $interval, 'completed', 'imported', $name);
                    SELECT last_insert_rowid();
                    """;
                command.Parameters.AddWithValue("$host", attempts[0].TargetHost);
                command.Parameters.AddWithValue("$started", attempts[0].Timestamp.ToUnixTimeMilliseconds());
                command.Parameters.AddWithValue("$offset", (int)attempts[0].Timestamp.Offset.TotalMinutes);
                command.Parameters.AddWithValue("$interval", (long)interval.TotalMilliseconds);
                command.Parameters.AddWithValue("$name", sourceName);
                var runId = (long)command.ExecuteScalar()!;

                InsertAttempts(connection, transaction, runId, 0, attempts);
                WriteSummary(connection, transaction, runId, RunOutcome.Completed, attempts[^1].Timestamp, summary);
                transaction.Commit();
                return runId;
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<RunRecord>> ListRunsAsync(CancellationToken cancellationToken) => database.RunAsync<IReadOnlyList<RunRecord>>(
        connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, target_host, started_at_ms, started_offset_min, ended_at_ms, interval_ms, timeout_ms, planned_duration_ms,
                       outcome, sent, received, mean_ms, median_ms, p95_ms, min_ms, max_ms, jitter_ms, outages, longest_outage_ms, mos,
                       source, source_name
                FROM ping_runs
                ORDER BY started_at_ms DESC, id DESC
                """;
            using var reader = command.ExecuteReader();
            var runs = new List<RunRecord>();
            while (reader.Read())
            {
                var offset = TimeSpan.FromMinutes(reader.GetInt32(3));
                runs.Add(new RunRecord(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(2)).ToOffset(offset),
                    reader.IsDBNull(4) ? null : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(4)).ToOffset(offset),
                    TimeSpan.FromMilliseconds(reader.GetInt64(5)),
                    TimeSpan.FromMilliseconds(reader.GetInt64(6)),
                    reader.IsDBNull(7) ? null : TimeSpan.FromMilliseconds(reader.GetInt64(7)),
                    ParseOutcome(reader.GetString(8)),
                    new RunSummary(
                        reader.GetInt32(9),
                        reader.GetInt32(10),
                        Double(reader, 11),
                        Double(reader, 12),
                        Double(reader, 13),
                        Double(reader, 14),
                        Double(reader, 15),
                        Double(reader, 16),
                        reader.GetInt32(17),
                        reader.IsDBNull(18) ? null : TimeSpan.FromMilliseconds(reader.GetInt64(18)),
                        Double(reader, 19)))
                {
                    Source = reader.GetString(20) == "imported" ? RunSource.Imported : RunSource.Recorded,
                    SourceName = reader.IsDBNull(21) ? null : reader.GetString(21),
                });
            }

            return runs;
        },
        cancellationToken);

    public Task<IReadOnlyList<PingAttempt>> LoadAttemptsAsync(long runId, CancellationToken cancellationToken) => database.RunAsync<IReadOnlyList<PingAttempt>>(
        connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT r.target_host, a.timestamp_ms, a.offset_min, a.is_success, a.roundtrip_ms, a.details
                FROM ping_attempts a JOIN ping_runs r ON r.id = a.run_id
                WHERE a.run_id = $run
                ORDER BY a.sequence
                """;
            command.Parameters.AddWithValue("$run", runId);
            using var reader = command.ExecuteReader();
            var attempts = new List<PingAttempt>();
            while (reader.Read())
            {
                attempts.Add(new PingAttempt(
                    reader.GetString(0),
                    DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1)).ToOffset(TimeSpan.FromMinutes(reader.GetInt32(2))),
                    reader.GetInt32(3) == 1,
                    reader.IsDBNull(4) ? null : reader.GetInt64(4),
                    reader.GetString(5)));
            }

            return attempts;
        },
        cancellationToken);

    public Task DeleteRunAsync(long runId, CancellationToken cancellationToken) => database.RunAsync(
        connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM ping_runs WHERE id = $id";
            command.Parameters.AddWithValue("$id", runId);
            command.ExecuteNonQuery();
            return true;
        },
        cancellationToken);

    private static void InsertAttempts(SqliteConnection connection, SqliteTransaction transaction, long runId, int firstSequence, IReadOnlyList<PingAttempt> attempts)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        // A retried batch writes the same rows again, so repeating one is harmless.
        command.CommandText = """
            INSERT OR IGNORE INTO ping_attempts (run_id, sequence, timestamp_ms, offset_min, is_success, roundtrip_ms, details)
            VALUES ($run, $sequence, $timestamp, $offset, $success, $roundtrip, $details)
            """;
        var run = command.Parameters.Add("$run", SqliteType.Integer);
        var sequence = command.Parameters.Add("$sequence", SqliteType.Integer);
        var timestamp = command.Parameters.Add("$timestamp", SqliteType.Integer);
        var offset = command.Parameters.Add("$offset", SqliteType.Integer);
        var success = command.Parameters.Add("$success", SqliteType.Integer);
        var roundtrip = command.Parameters.Add("$roundtrip", SqliteType.Integer);
        var details = command.Parameters.Add("$details", SqliteType.Text);
        run.Value = runId;

        for (var index = 0; index < attempts.Count; index++)
        {
            var attempt = attempts[index];
            sequence.Value = firstSequence + index;
            timestamp.Value = attempt.Timestamp.ToUnixTimeMilliseconds();
            offset.Value = (int)attempt.Timestamp.Offset.TotalMinutes;
            success.Value = attempt.IsSuccess ? 1 : 0;
            roundtrip.Value = attempt.RoundtripMilliseconds is { } value ? value : DBNull.Value;
            details.Value = attempt.Details;
            command.ExecuteNonQuery();
        }
    }

    private static void WriteSummary(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        long runId,
        RunOutcome outcome,
        DateTimeOffset? endedAt,
        RunSummary summary)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE ping_runs SET
                outcome = $outcome, ended_at_ms = $ended, sent = $sent, received = $received,
                mean_ms = $mean, median_ms = $median, p95_ms = $p95, min_ms = $min, max_ms = $max,
                jitter_ms = $jitter, outages = $outages, longest_outage_ms = $longest, mos = $mos
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", runId);
        command.Parameters.AddWithValue("$outcome", OutcomeText(outcome));
        command.Parameters.AddWithValue("$ended", Nullable(endedAt?.ToUnixTimeMilliseconds()));
        command.Parameters.AddWithValue("$sent", summary.Sent);
        command.Parameters.AddWithValue("$received", summary.Received);
        command.Parameters.AddWithValue("$mean", Nullable(summary.MeanMilliseconds));
        command.Parameters.AddWithValue("$median", Nullable(summary.MedianMilliseconds));
        command.Parameters.AddWithValue("$p95", Nullable(summary.P95Milliseconds));
        command.Parameters.AddWithValue("$min", Nullable(summary.MinimumMilliseconds));
        command.Parameters.AddWithValue("$max", Nullable(summary.MaximumMilliseconds));
        command.Parameters.AddWithValue("$jitter", Nullable(summary.JitterMilliseconds));
        command.Parameters.AddWithValue("$outages", summary.Outages);
        command.Parameters.AddWithValue("$longest", Nullable(summary.LongestOutage is { } longest ? (long?)longest.TotalMilliseconds : null));
        command.Parameters.AddWithValue("$mos", Nullable(summary.MeanOpinionScore));
        command.ExecuteNonQuery();
    }

    private static string OutcomeText(RunOutcome outcome) => outcome switch
    {
        RunOutcome.Completed => "completed",
        RunOutcome.Stopped => "stopped",
        RunOutcome.Interrupted => "interrupted",
        _ => "running",
    };

    private static RunOutcome ParseOutcome(string text) => text switch
    {
        "completed" => RunOutcome.Completed,
        "stopped" => RunOutcome.Stopped,
        "interrupted" => RunOutcome.Interrupted,
        _ => RunOutcome.Running,
    };

    private static object Nullable<T>(T? value)
        where T : struct => value is { } present ? present : DBNull.Value;

    private static double? Double(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);
}
