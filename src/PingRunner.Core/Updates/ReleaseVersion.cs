using System.Globalization;
using System.Text.RegularExpressions;

namespace PingRunner.Core.Updates;

/// <summary>
/// A semantic version such as 2.0.0 or 2.1.0-dev, as tags ("v2.0.0") and assembly versions write it.
/// A pre-release sorts before its release, so 2.0.0-dev is older than 2.0.0.
/// </summary>
public sealed partial record ReleaseVersion(int Major, int Minor, int Patch, string PreRelease) : IComparable<ReleaseVersion>
{
    public bool IsPreRelease => PreRelease.Length > 0;

    public static ReleaseVersion? TryParse(string? text)
    {
        var match = Pattern().Match(text?.Trim() ?? string.Empty);
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Groups["major"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            && int.TryParse(match.Groups["minor"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            && int.TryParse(match.Groups["patch"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var patch)
                ? new ReleaseVersion(major, minor, patch, match.Groups["pre"].Value)
                : null;
    }

    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var core = (Major, Minor, Patch).CompareTo((other.Major, other.Minor, other.Patch));
        if (core != 0)
        {
            return core;
        }

        return (IsPreRelease, other.IsPreRelease) switch
        {
            (false, false) => 0,
            (true, false) => -1,
            (false, true) => 1,
            _ => string.CompareOrdinal(PreRelease, other.PreRelease),
        };
    }

    public static bool operator <(ReleaseVersion left, ReleaseVersion right) => left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator >(ReleaseVersion left, ReleaseVersion right) => left is not null && left.CompareTo(right) > 0;

    public static bool operator <=(ReleaseVersion left, ReleaseVersion right) => left is null || left.CompareTo(right) <= 0;

    public static bool operator >=(ReleaseVersion left, ReleaseVersion right) => left is null ? right is null : left.CompareTo(right) >= 0;

    public override string ToString() => IsPreRelease ? $"{Major}.{Minor}.{Patch}-{PreRelease}" : $"{Major}.{Minor}.{Patch}";

    [GeneratedRegex(@"^v?(?<major>\d{1,9})\.(?<minor>\d{1,9})\.(?<patch>\d{1,9})(?:-(?<pre>[0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$")]
    private static partial Regex Pattern();
}
