using System.Text;
using PingRunner.Core.Pinging;
using PingRunner.Core.Sessions;
using PingRunner.Core.Tests.Fakes;

namespace PingRunner.Core.Tests.Sessions;

public sealed class PingAttemptCsvTests
{
    [Fact]
    public async Task ExportThenImport_RoundTripsEveryField()
    {
        var attempts = new List<PingAttempt>
        {
            Attempts.At(0, 18),
            Attempts.At(1, null) with { Details = "Timed out, \"twice\"" },
        };
        using var stream = new MemoryStream();

        await PingAttemptCsv.ExportAsync(attempts, stream, TestContext.Current.CancellationToken);
        stream.Position = 0;
        var imported = await PingAttemptCsv.ImportAsync(stream, TestContext.Current.CancellationToken);

        Assert.Equal(attempts, imported);
    }

    [Fact]
    public async Task Export_DetailsWithLineBreaks_StayOnOneRow()
    {
        using var stream = new MemoryStream();

        await PingAttemptCsv.ExportAsync([Attempts.At(0, null) with { Details = "line one\nline two" }], stream, TestContext.Current.CancellationToken);

        var text = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal(2, text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task Import_VersionOneFile_Loads()
    {
        const string file = """
            Timestamp,TargetHost,IsSuccess,RoundtripTimeMilliseconds,Details
            2026-03-14T10:00:01.0000000+01:00,8.8.8.8,true,21,Reply from 8.8.8.8
            2026-03-14T10:00:00.0000000+01:00,8.8.8.8,false,,Ping status: TimedOut
            """;

        var imported = await Import(file);

        Assert.Equal(2, imported.Count);
        Assert.False(imported[0].IsSuccess);
        Assert.Equal(21, imported[1].RoundtripMilliseconds);
    }

    [Theory]
    [InlineData("not-a-date,8.8.8.8,true,1,x", "timestamp")]
    [InlineData("2026-03-14T10:00:00Z,8.8.8.8,maybe,1,x", "IsSuccess")]
    [InlineData("2026-03-14T10:00:00Z,8.8.8.8,true,-4,x", "round-trip")]
    [InlineData("2026-03-14T10:00:00Z,,true,1,x", "host")]
    [InlineData("2026-03-14T10:00:00Z,8.8.8.8,true", "fields")]
    public async Task Import_BadRow_NamesTheLine(string row, string problem)
    {
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Import($"{PingAttemptCsv.Header}\n{row}\n"));

        Assert.Contains("Line 2", error.Message, StringComparison.Ordinal);
        Assert.Contains(problem, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Import_OverlongLine_IsRejected()
    {
        var row = $"2026-03-14T10:00:00Z,8.8.8.8,true,1,{new string('x', PingAttemptCsv.MaximumLineLength)}";

        await Assert.ThrowsAsync<InvalidDataException>(() => Import($"{PingAttemptCsv.Header}\n{row}\n"));
    }

    private static async Task<IReadOnlyList<PingAttempt>> Import(string text)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        return await PingAttemptCsv.ImportAsync(stream, TestContext.Current.CancellationToken);
    }
}
