# NuGetVersionChecker

A .NET console app that downloads NuGet packages from nuget.org, finds a specific assembly in every target framework folder of each package, and shows its version metadata:

- **Version**: the NuGet package version
- **AssemblyVersion**: the version in the assembly's identity (`[AssemblyVersion]`)
- **FileVersion**: the `[AssemblyFileVersion]` attribute
- **InformationalVersion**: the `[AssemblyInformationalVersion]` attribute, which often includes a commit hash

This is useful when you need to check that the different versions match, for example before you publish a release or set up binding redirects.

## How it works

1. Reads the package ID and version from the nuget.org gallery URL. If the URL has no version, it uses the latest stable version.
2. Downloads the `.nupkg` into memory. Nothing is written to disk.
3. Finds the assembly in each `lib/{tfm}/` folder of the package.
4. Reads the assembly's metadata with `System.Reflection.Metadata` without loading the assembly, so it works with assemblies built for any target framework.
5. Shows one [Spectre.Console](https://spectreconsole.net/) table for each package.

## Requirements

- The .NET SDK version pinned in [global.json](global.json).

## Usage

The packages to check are hardcoded as `NuGetAssemblyQuery` records at the top of [Program.cs](src/NuGetVersionChecker/Program.cs):

```csharp
NuGetAssemblyQuery[] queries =
[
    new(NuGetPackageUrl: "https://www.nuget.org/packages/Newtonsoft.Json/13.0.3", AssemblyName: "Newtonsoft.Json"),
    new(NuGetPackageUrl: "https://www.nuget.org/packages/Serilog/4.0.0", AssemblyName: "Serilog"),
];
```

- `NuGetPackageUrl` is the package's page on nuget.org, ideally including the version.
- `AssemblyName` is the DLL to look for. The `.dll` extension is optional, and the match ignores case.

Run it with:

```shell
dotnet run --project src/NuGetVersionChecker
```

## Example output

```text
                                       Newtonsoft.Json 13.0.3
╭──────────────────┬─────────────────────┬─────────┬─────────────────┬──────────────┬───────────────────────────────────────────────────╮
│ Target Framework │ DLL                 │ Version │ AssemblyVersion │ FileVersion  │ InformationalVersion                              │
├──────────────────┼─────────────────────┼─────────┼─────────────────┼──────────────┼───────────────────────────────────────────────────┤
│ net20            │ Newtonsoft.Json.dll │ 13.0.3  │ 13.0.0.0        │ 13.0.3.27908 │ 13.0.3+0a2e291c0d9c0c7675d445703e51750363a549ef   │
│ net35            │ Newtonsoft.Json.dll │ 13.0.3  │ 13.0.0.0        │ 13.0.3.27908 │ 13.0.3+0a2e291c0d9c0c7675d445703e51750363a549ef   │
│ net40            │ Newtonsoft.Json.dll │ 13.0.3  │ 13.0.0.0        │ 13.0.3.27908 │ 13.0.3+0a2e291c0d9c0c7675d445703e51750363a549ef   │
│ net45            │ Newtonsoft.Json.dll │ 13.0.3  │ 13.0.0.0        │ 13.0.3.27908 │ 13.0.3+0a2e291c0d9c0c7675d445703e51750363a549ef   │
│ net6.0           │ Newtonsoft.Json.dll │ 13.0.3  │ 13.0.0.0        │ 13.0.3.27908 │ 13.0.3+0a2e291c0d9c0c7675d445703e51750363a549ef   │
│ netstandard1.0   │ Newtonsoft.Json.dll │ 13.0.3  │ 13.0.0.0        │ 13.0.3.27908 │ 13.0.3+0a2e291c0d9c0c7675d445703e51750363a549ef   │
│ netstandard1.3   │ Newtonsoft.Json.dll │ 13.0.3  │ 13.0.0.0        │ 13.0.3.27908 │ 13.0.3+0a2e291c0d9c0c7675d445703e51750363a549ef   │
│ netstandard2.0   │ Newtonsoft.Json.dll │ 13.0.3  │ 13.0.0.0        │ 13.0.3.27908 │ 13.0.3+0a2e291c0d9c0c7675d445703e51750363a549ef   │
╰──────────────────┴─────────────────────┴─────────┴─────────────────┴──────────────┴───────────────────────────────────────────────────╯
```

If the assembly isn't found under `lib/`, the table shows a note in its caption. If a package can't be downloaded, an error prints in red and the app moves on to the next package.

## Limitations

- Only nuget.org gallery URLs are supported.
- Only `lib/` is searched. `ref/` (reference assemblies) and `runtimes/{rid}/lib/` (platform-specific builds) are ignored.
- .NET tool packages are not supported, because they put their assemblies under `tools/` rather than `lib/`.
- Target frameworks are sorted alphabetically, so `net10.0` appears before `net6.0`.

## License

[MIT](LICENSE)
