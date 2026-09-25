namespace PingRunner.Core.SpeedTest;

/// <summary>How much a busy connection slows everything else down, best to worst.</summary>
public enum BufferbloatGrade
{
    APlus,
    A,
    B,
    C,
    D,
    F,
}
