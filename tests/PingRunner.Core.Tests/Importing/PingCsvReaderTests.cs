using System.Globalization;
using System.Text;
using PingRunner.Core.Importing;
using PingRunner.Core.Pinging;
using PingRunner.Core.Sessions;
using PingRunner.Core.Tests.Fakes;

namespace PingRunner.Core.Tests.Importing;

public sealed class PingCsvReaderTests : IDisposable
{
    private readonly CultureInfo previous = CultureInfo.CurrentCulture;

    public PingCsvReaderTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    public void Dispose() => CultureInfo.CurrentCulture = previous;

    [Fact]
    public async Task ExportThenRead_RoundTripsEveryField()
    {
        var attempts = new List<PingAttempt>
        {
            Attempts.At(0, 18),
            Attempts.At(1, null) with { Details = "Timed out, \"twice\"" },
        };
        using var stream = new MemoryStream();
        await PingAttemptCsv.ExportAsync(attempts, stream, TestContext.Current.CancellationToken);
        stream.Position = 0;

        var imported = await new PingCsvReader().ReadAsync(stream, "session.csv", TestContext.Current.CancellationToken);

        Assert.Equal(attempts, imported.Attempts);
        Assert.Equal(0, imported.SkippedRows);
        Assert.Equal("Read 2 pings from session.csv.", imported.Describe());
    }

    [Fact]
    public async Task Export_DetailsWithLineBreaks_StayOnOneRow()
    {
        using var stream = new MemoryStream();

        await PingAttemptCsv.ExportAsync([Attempts.At(0, null) with { Details = "line one\nline two" }], stream, TestContext.Current.CancellationToken);

        Assert.Equal(2, Encoding.UTF8.GetString(stream.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task Read_VersionOneFile_Loads()
    {
        var imported = await Read("""
            Timestamp,TargetHost,IsSuccess,RoundtripTimeMilliseconds,Details
            2026-03-14T10:00:01.0000000+01:00,8.8.8.8,true,21,Reply from 8.8.8.8
            2026-03-14T10:00:00.0000000+01:00,8.8.8.8,false,,Ping status: TimedOut
            """);

        Assert.Equal(2, imported.Attempts.Count);
        Assert.False(imported.Attempts[0].IsSuccess);
        Assert.Equal(21, imported.Attempts[1].RoundtripMilliseconds);
        Assert.Equal(TimeSpan.FromHours(1), imported.Attempts[1].Timestamp.Offset);
    }

    [Fact]
    public async Task Read_SpreadsheetResavedWithSemicolonsAndRenamedColumns_Loads()
    {
        // What a spreadsheet in a comma-decimal locale writes back: semicolons, local times, a UTC offset column.
        var imported = await Read("""
            Time;UTC offset;Target;Result;Latency (ms);Details
            2026-09-25 18:00:00;+02:00;1.1.1.1;Reply;17,6;Reply from 1.1.1.1
            2026-09-25 18:00:01;+02:00;1.1.1.1;Lost;–;Request timed out
            """);

        Assert.Equal(2, imported.Attempts.Count);
        var first = imported.Attempts[0];
        Assert.Equal(new DateTimeOffset(2026, 9, 25, 18, 0, 0, TimeSpan.FromHours(2)), first.Timestamp);
        Assert.Equal("1.1.1.1", first.TargetHost);
        Assert.Equal(18, first.RoundtripMilliseconds);
        Assert.False(imported.Attempts[1].IsSuccess);
        Assert.Null(imported.Attempts[1].RoundtripMilliseconds);
    }

    [Fact]
    public async Task Read_TabSeparatedWithoutResultColumn_TakesRepliesFromLatency()
    {
        var imported = await Read("timestamp\thost\trtt\n2026-09-25T10:00:00Z\t9.9.9.9\t12\n2026-09-25T10:00:01Z\t9.9.9.9\t\n");

        Assert.True(imported.Attempts[0].IsSuccess);
        Assert.False(imported.Attempts[1].IsSuccess);
        Assert.Equal("Lost", imported.Attempts[1].Details);
    }

    [Fact]
    public async Task Read_BadRows_AreLeftOutAndListed()
    {
        var imported = await Read($"""
            {PingAttemptCsv.Header}
            2026-09-25T10:00:00Z,8.8.8.8,true,12,ok
            yesterday,8.8.8.8,true,12,ok
            2026-09-25T10:00:02Z,8.8.8.8,maybe,12,ok
            2026-09-25T10:00:03Z,8.8.8.8,true,-4,ok
            2026-09-25T10:00:04Z,8.8.8.8,true,15,ok
            """);

        Assert.Equal(2, imported.Attempts.Count);
        Assert.Equal(3, imported.SkippedRows);
        Assert.Equal([3, 4, 5], imported.Issues.Select(issue => issue.Line));
        Assert.Contains("unreadable time", imported.Issues[0].Message, StringComparison.Ordinal);
        Assert.StartsWith("Read 2 pings from test.csv; left out 3 unreadable rows (Line 3:", imported.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Read_NoTimeColumn_IsRefused()
    {
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Read("name,score\nada,3\n"));

        Assert.Contains("not a ping file", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Read_NoReadableRow_IsRefusedWithTheReason()
    {
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Read($"{PingAttemptCsv.Header}\nsoon,8.8.8.8,true,1,x\n"));

        Assert.Contains("Line 2", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Read_TwoTargets_SplitsPerTarget()
    {
        var imported = await Read($"""
            {PingAttemptCsv.Header}
            2026-09-25T10:00:00Z,8.8.8.8,true,12,ok
            2026-09-25T10:00:00Z,1.1.1.1,true,9,ok
            2026-09-25T10:00:01Z,8.8.8.8,true,13,ok
            """);

        Assert.Equal(["8.8.8.8", "1.1.1.1"], imported.Targets);
        Assert.Equal([2, 1], imported.ByTarget().Select(run => run.Count));
    }

    [Fact]
    public async Task Importer_PicksTheReaderByExtension()
    {
        var importer = new PingFileImporter([new PingCsvReader()]);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes($"{PingAttemptCsv.Header}\n2026-09-25T10:00:00Z,8.8.8.8,true,12,ok\n"));

        var imported = await importer.ReadAsync(stream, @"C:\exports\run.CSV", TestContext.Current.CancellationToken);

        Assert.Equal("run.CSV", imported.SourceName);
        Assert.StartsWith("Ping data (*.csv;*.txt;*.tsv)|", importer.FileFilter, StringComparison.Ordinal);
    }

    private static async Task<PingImport> Read(string text)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        return await new PingCsvReader().ReadAsync(stream, "test.csv", TestContext.Current.CancellationToken);
    }
}
