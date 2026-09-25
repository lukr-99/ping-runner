using System.Text.Json;
using Microsoft.Data.Sqlite;
using PingRunner.Core.History;
using PingRunner.Core.SpeedTest;
using PingRunner.Core.Statistics;
using PingRunner.Core.Throughput;

namespace PingRunner.Infrastructure.History;

/// <summary>
/// Speed tests in the history database, kept whole: the throughput series and every latency sample
/// are stored as JSON arrays, so a stored test reads back exactly as it was measured.
/// </summary>
public sealed class SqliteSpeedTestHistory(SqliteHistoryDatabase database) : ISpeedTestHistory
{
    public Task<long> SaveAsync(SpeedTestResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        return database.RunAsync(
            connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO speed_tests (
                        started_at_ms, started_offset_min, server, latency_host,
                        download_average_bps, download_peak_bps, download_bytes, download_duration_ms, download_series,
                        upload_average_bps, upload_peak_bps, upload_bytes, upload_duration_ms, upload_series,
                        idle_latency, download_latency, upload_latency)
                    VALUES (
                        $started, $offset, $server, $host,
                        $downAverage, $downPeak, $downBytes, $downDuration, $downSeries,
                        $upAverage, $upPeak, $upBytes, $upDuration, $upSeries,
                        $idle, $downLatency, $upLatency);
                    SELECT last_insert_rowid();
                    """;
                command.Parameters.AddWithValue("$started", result.StartedAt.ToUnixTimeMilliseconds());
                command.Parameters.AddWithValue("$offset", (int)result.StartedAt.Offset.TotalMinutes);
                command.Parameters.AddWithValue("$server", result.Server);
                command.Parameters.AddWithValue("$host", result.LatencyHost);
                AddDirection(command, "down", result.Download);
                AddDirection(command, "up", result.Upload);
                command.Parameters.AddWithValue("$idle", Samples(result.IdleLatency));
                command.Parameters.AddWithValue("$downLatency", Samples(result.DownloadLatency));
                command.Parameters.AddWithValue("$upLatency", Samples(result.UploadLatency));
                return (long)command.ExecuteScalar()!;
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<SpeedTestRecord>> ListAsync(CancellationToken cancellationToken) => database.RunAsync<IReadOnlyList<SpeedTestRecord>>(
        connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, started_at_ms, started_offset_min, server, latency_host,
                       download_average_bps, download_peak_bps, download_bytes, download_duration_ms, download_series,
                       upload_average_bps, upload_peak_bps, upload_bytes, upload_duration_ms, upload_series,
                       idle_latency, download_latency, upload_latency
                FROM speed_tests
                ORDER BY started_at_ms DESC, id DESC
                """;
            using var reader = command.ExecuteReader();
            var tests = new List<SpeedTestRecord>();
            while (reader.Read())
            {
                var startedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1)).ToOffset(TimeSpan.FromMinutes(reader.GetInt32(2)));
                tests.Add(new SpeedTestRecord(
                    reader.GetInt64(0),
                    new SpeedTestResult(
                        startedAt,
                        reader.GetString(3),
                        reader.GetString(4),
                        Distribution(reader.GetString(15)),
                        Direction(reader, 5, ThroughputDirection.Download),
                        Distribution(reader.GetString(16)),
                        Direction(reader, 10, ThroughputDirection.Upload),
                        Distribution(reader.GetString(17)))));
            }

            return tests;
        },
        cancellationToken);

    public Task DeleteAsync(long id, CancellationToken cancellationToken) => database.RunAsync(
        connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM speed_tests WHERE id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
            return true;
        },
        cancellationToken);

    private static void AddDirection(SqliteCommand command, string prefix, ThroughputResult result)
    {
        command.Parameters.AddWithValue($"${prefix}Average", result.AverageBitsPerSecond);
        command.Parameters.AddWithValue($"${prefix}Peak", result.PeakBitsPerSecond);
        command.Parameters.AddWithValue($"${prefix}Bytes", result.TotalBytes);
        command.Parameters.AddWithValue($"${prefix}Duration", (long)result.Duration.TotalMilliseconds);
        command.Parameters.AddWithValue(
            $"${prefix}Series",
            JsonSerializer.Serialize(result.Series.Select(point => new[] { point.Elapsed.TotalMilliseconds, point.BitsPerSecond })));
    }

    private static ThroughputResult Direction(SqliteDataReader reader, int first, ThroughputDirection direction)
    {
        var series = JsonSerializer.Deserialize<double[][]>(reader.GetString(first + 4)) ?? [];
        return new ThroughputResult(
            direction,
            reader.GetDouble(first),
            reader.GetDouble(first + 1),
            reader.GetInt64(first + 2),
            TimeSpan.FromMilliseconds(reader.GetInt64(first + 3)),
            [.. series.Where(point => point.Length == 2).Select(point => new ThroughputPoint(TimeSpan.FromMilliseconds(point[0]), point[1]))]);
    }

    private static string Samples(LatencyDistribution? distribution) =>
        JsonSerializer.Serialize(distribution?.Samples ?? []);

    private static LatencyDistribution? Distribution(string json) =>
        LatencyDistribution.From(JsonSerializer.Deserialize<double[]>(json) ?? []);
}
