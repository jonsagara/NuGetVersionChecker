using NuGetVersionChecker.Models;
using NuGetVersionChecker.Services;
using Spectre.Console;

NuGetAssemblyQuery[] queries =
[
    new(NuGetPackageUrl: "https://www.nuget.org/packages/Sagara.Core/5.10.4", AssemblyName: "Sagara.Core"),
    new(NuGetPackageUrl: "https://www.nuget.org/packages/Sagara.Core.AspNetCore/5.10.4", AssemblyName: "Sagara.Core.AspNetCore"),
    new(NuGetPackageUrl: "https://www.nuget.org/packages/Sagara.Core.Caching/5.10.4", AssemblyName: "Sagara.Core.Caching"),
    new(NuGetPackageUrl: "https://www.nuget.org/packages/Sagara.Core.Data/5.10.4", AssemblyName: "Sagara.Core.Data"),
    new(NuGetPackageUrl: "https://www.nuget.org/packages/Sagara.Core.Google/5.10.4", AssemblyName: "Sagara.Core.Google"),
    new(NuGetPackageUrl: "https://www.nuget.org/packages/Sagara.Core.Logging.Serilog/5.10.4", AssemblyName: "Sagara.Core.Logging.Serilog"),
    // Doesn't support dotnet tools
    //new(NuGetPackageUrl: "https://www.nuget.org/packages/Sagara.NuGetDiff.Tool/1.0.3", AssemblyName: "Sagara.NuGetDiff.Tool"),
];

using HttpClient httpClient = new();
NuGetPackageDownloader downloader = new(httpClient);

foreach (NuGetAssemblyQuery query in queries)
{
    try
    {
        (string packageId, string packageVersion, MemoryStream package) = await AnsiConsole.Status()
            .StartAsync($"Downloading [blue]{Markup.Escape(query.NuGetPackageUrl)}[/]...", _ => downloader.DownloadAsync(query.NuGetPackageUrl));

        IReadOnlyList<AssemblyVersionInfo> results;
        await using (package)
        {
            results = await AssemblyVersionReader.ReadAsync(package, packageVersion, query.AssemblyName);
        }

        AnsiConsole.Write(BuildTable(packageId, packageVersion, query.AssemblyName, results));
        AnsiConsole.WriteLine();
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Error processing {Markup.Escape(query.NuGetPackageUrl)}:[/] {Markup.Escape(ex.Message)}");
        AnsiConsole.WriteLine();
    }
}

static Table BuildTable(string packageId, string packageVersion, string assemblyName, IReadOnlyList<AssemblyVersionInfo> results)
{
    Table table = new Table()
        .Title($"[bold]{Markup.Escape(packageId)} {Markup.Escape(packageVersion)}[/]")
        .Border(TableBorder.Rounded)
        .AddColumn("Target Framework")
        .AddColumn("DLL")
        .AddColumn("Version")
        .AddColumn("AssemblyVersion")
        .AddColumn("FileVersion")
        .AddColumn("InformationalVersion");

    if (results.Count == 0)
    {
        table.Caption($"[yellow]No {Markup.Escape(assemblyName)} found under lib/[/]");
        return table;
    }

    foreach (AssemblyVersionInfo result in results)
    {
        table.AddRow(
            Markup.Escape(result.TargetFramework),
            Markup.Escape(result.DllName),
            Markup.Escape(result.PackageVersion),
            FormatValue(result.AssemblyVersion),
            FormatValue(result.FileVersion),
            FormatValue(result.InformationalVersion));
    }

    return table;
}

static string FormatValue(string? value) =>
    value is null ? "[grey]—[/]" : Markup.Escape(value);
