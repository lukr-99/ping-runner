namespace PingRunner.App.Features.Graphing.Models;

public sealed record GraphOption<T>(string Label, T Value)
{
    public override string ToString() => Label;
}
