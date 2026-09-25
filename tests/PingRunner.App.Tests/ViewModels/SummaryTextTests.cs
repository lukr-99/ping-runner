using System.Globalization;
using PingRunner.App.Formatting;
using PingRunner.Core.Pinging;
using PingRunner.Core.Statistics;

namespace PingRunner.App.Tests.ViewModels;

public sealed class SummaryTextTests
{
    [Fact]
    public void Build_WritesTheHeadlineFigures()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            var start = new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);
            var attempts = new List<PingAttempt>
            {
                new("8.8.8.8", start, true, 10, "Reply from 8.8.8.8"),
                new("8.8.8.8", start.AddSeconds(1), false, null, "Request timed out"),
                new("8.8.8.8", start.AddSeconds(2), true, 20, "Reply from 8.8.8.8"),
            };

            var text = SummaryText.Build("2.0.0", "8.8.8.8", null, PingStatistics.From(attempts));

            Assert.StartsWith("Ping Runner 2.0.0 · 8.8.8.8", text, StringComparison.Ordinal);
            Assert.Contains("Sent 3 · received 2 · lost 1 (33.3 %)", text, StringComparison.Ordinal);
            Assert.Contains("avg 15.0 ms", text, StringComparison.Ordinal);
            Assert.Contains("Jitter 10.0 ms", text, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
