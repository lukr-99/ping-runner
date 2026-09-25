namespace PingRunner.Core.Importing;

/// <summary>A row that was left out of an import, and why.</summary>
public sealed record ImportIssue(int Line, string Message)
{
    public override string ToString() => $"Line {Line}: {Message}";
}
