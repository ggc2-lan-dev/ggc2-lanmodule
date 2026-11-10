using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("GGC2LanModule")]
[assembly: AssemblyDescription("A LAN multiplayer module for Guns, Gore and Cannoli 2")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("ggc2-lan-dev")]
[assembly: AssemblyProduct("GGC2LanModule")]
[assembly: AssemblyCopyright("Copyright © 2026 ggc2-lan-dev")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]

[assembly: Guid("3d5b6b77-32af-4429-ad6e-0a8094339832")]

[assembly: AssemblyVersion(ModuleBuildInfo.VersionFull)]
[assembly: AssemblyFileVersion(ModuleBuildInfo.VersionFull)]
[assembly: AssemblyInformationalVersion(ModuleBuildInfo.Version)]

internal static class ModuleBuildInfo
{
    public const string Version = "1.7.0";
    public const string VersionFull = Version + ".0";
    public const string VersionLabel = "v" + Version;
}