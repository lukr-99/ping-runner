namespace PingRunner.Core.SpeedTest;

/// <summary>
/// The rise in latency while the line is full, compared with idle. The grade bands follow the widely
/// used Waveform bufferbloat test: under 5 ms is A+, under 30 ms A, 60 ms B, 200 ms C, 400 ms D, and
/// anything worse F.
/// </summary>
public sealed record Bufferbloat(double IncreaseMilliseconds)
{
    public BufferbloatGrade Grade => IncreaseMilliseconds switch
    {
        < 5 => BufferbloatGrade.APlus,
        < 30 => BufferbloatGrade.A,
        < 60 => BufferbloatGrade.B,
        < 200 => BufferbloatGrade.C,
        < 400 => BufferbloatGrade.D,
        _ => BufferbloatGrade.F,
    };

    public string GradeText => Grade == BufferbloatGrade.APlus ? "A+" : Grade.ToString();
}
