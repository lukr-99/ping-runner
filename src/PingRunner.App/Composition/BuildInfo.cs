using System.Reflection;
using PingRunner.Core.Updates;

namespace PingRunner.App.Composition;

/// <summary>
/// What this build is: its version (2.0.0 or 2.0.0-dev) and whether it is a release build, both
/// baked in by Directory.Build.props.
/// </summary>
public sealed record BuildInfo(ReleaseVersion Version, bool IsReleaseBuild)
{
    public bool IsDevBuild => !IsReleaseBuild;

    public static BuildInfo FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = ReleaseVersion.TryParse(informational) ?? new ReleaseVersion(0, 0, 0, "dev");
        var release = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Any(attribute => attribute.Key == "PingRunner.ReleaseBuild" && string.Equals(attribute.Value, "true", StringComparison.OrdinalIgnoreCase));
        return new BuildInfo(version, release && !version.IsPreRelease);
    }
}
