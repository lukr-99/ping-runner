namespace PingRunner.Infrastructure.History;

/// <summary>One immutable migration file: its number, file name, SQL and the SHA-256 of its bytes.</summary>
public sealed record Migration(int Number, string FileName, string Sql, string Checksum);
