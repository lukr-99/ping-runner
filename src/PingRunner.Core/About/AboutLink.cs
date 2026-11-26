namespace PingRunner.Core.About;

/// <summary>A place to go from the About page: a label, the address and one line on what it is for.</summary>
public sealed record AboutLink(string Label, string Url, string Hint);
