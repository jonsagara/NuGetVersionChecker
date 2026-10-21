namespace NuGetVersionChecker.Models;

/// <summary>
/// Version information for one copy of an assembly found under a package's lib/{tfm}/ folder.
/// </summary>
public record AssemblyVersionInfo(
    string TargetFramework,
    string DllName,
    string PackageVersion,
    string? AssemblyVersion,
    string? FileVersion,
    string? InformationalVersion);
