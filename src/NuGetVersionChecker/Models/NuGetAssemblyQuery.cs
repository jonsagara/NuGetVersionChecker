namespace NuGetVersionChecker.Models;

/// <summary>
/// A request to inspect a single assembly within a NuGet package.
/// </summary>
/// <param name="NuGetPackageUrl">The nuget.org gallery URL, e.g. https://www.nuget.org/packages/Newtonsoft.Json/13.0.3</param>
/// <param name="AssemblyName">The assembly to look for, with or without the .dll extension, e.g. Newtonsoft.Json</param>
public record NuGetAssemblyQuery(string NuGetPackageUrl, string AssemblyName);
