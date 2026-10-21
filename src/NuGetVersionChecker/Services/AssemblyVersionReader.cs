using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using NuGetVersionChecker.Models;
using Sagara.Core.Extensions;

namespace NuGetVersionChecker.Services;

/// <summary>
/// Finds an assembly under each lib/{tfm}/ folder of a .nupkg and reads its version metadata without loading it.
/// </summary>
public static class AssemblyVersionReader
{
    public static async Task<IReadOnlyList<AssemblyVersionInfo>> ReadAsync(Stream package, string packageVersion, string assemblyName, CancellationToken cancellationToken = default)
    {
        string dllName = assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? assemblyName
            : $"{assemblyName}.dll";

        List<AssemblyVersionInfo> results = [];

        await using ZipArchive archive = await ZipArchive.CreateAsync(package, ZipArchiveMode.Read, leaveOpen: true, entryNameEncoding: null, cancellationToken);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            // Matches lib/{tfm}/{dll}, plus legacy packages that put the dll directly in lib/.
            string[] segments = entry.FullName.Split('/');
            bool isLibEntry = segments[0].Equals("lib", StringComparison.OrdinalIgnoreCase)
                && segments.Length is 2 or 3
                && segments[^1].Equals(dllName, StringComparison.OrdinalIgnoreCase);

            if (!isLibEntry)
            {
                continue;
            }

            string targetFramework = segments.Length == 3 ? Uri.UnescapeDataString(segments[1]) : "(none)";

            // PEReader needs a seekable stream, and zip entry streams are not.
            using MemoryStream dllStream = new();
            await using (Stream entryStream = await entry.OpenAsync(cancellationToken))
            {
                await entryStream.CopyToAsync(dllStream, cancellationToken);
            }
            dllStream.Position = 0;

            results.Add(ReadVersionInfo(dllStream, targetFramework, segments[^1], packageVersion));
        }

        return results
            .OrderBy(r => r.TargetFramework, StringComparer.InvariantCultureNumericIgnoreCase)
            .ToList();
    }

    private static AssemblyVersionInfo ReadVersionInfo(Stream dllStream, string targetFramework, string dllName, string packageVersion)
    {
        using PEReader peReader = new(dllStream);
        if (!peReader.HasMetadata)
        {
            return new AssemblyVersionInfo(targetFramework, dllName, packageVersion, null, null, null);
        }

        MetadataReader metadataReader = peReader.GetMetadataReader();
        AssemblyDefinition assemblyDefinition = metadataReader.GetAssemblyDefinition();

        string? fileVersion = null;
        string? informationalVersion = null;

        foreach (CustomAttributeHandle handle in assemblyDefinition.GetCustomAttributes())
        {
            CustomAttribute attribute = metadataReader.GetCustomAttribute(handle);
            string? attributeName = GetAttributeTypeName(metadataReader, attribute);

            if (attributeName == "System.Reflection.AssemblyFileVersionAttribute")
            {
                fileVersion = ReadSingleStringArgument(metadataReader, attribute);
            }
            else if (attributeName == "System.Reflection.AssemblyInformationalVersionAttribute")
            {
                informationalVersion = ReadSingleStringArgument(metadataReader, attribute);
            }
        }

        return new AssemblyVersionInfo(
            targetFramework,
            dllName,
            packageVersion,
            assemblyDefinition.Version.ToString(),
            fileVersion,
            informationalVersion);
    }

    private static string? GetAttributeTypeName(MetadataReader metadataReader, CustomAttribute attribute)
    {
        EntityHandle typeHandle;

        if (attribute.Constructor.Kind == HandleKind.MemberReference)
        {
            MemberReference constructor = metadataReader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            typeHandle = constructor.Parent;
        }
        else if (attribute.Constructor.Kind == HandleKind.MethodDefinition)
        {
            MethodDefinition constructor = metadataReader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);
            typeHandle = constructor.GetDeclaringType();
        }
        else
        {
            return null;
        }

        if (typeHandle.Kind == HandleKind.TypeReference)
        {
            TypeReference typeReference = metadataReader.GetTypeReference((TypeReferenceHandle)typeHandle);
            return $"{metadataReader.GetString(typeReference.Namespace)}.{metadataReader.GetString(typeReference.Name)}";
        }

        if (typeHandle.Kind == HandleKind.TypeDefinition)
        {
            TypeDefinition typeDefinition = metadataReader.GetTypeDefinition((TypeDefinitionHandle)typeHandle);
            return $"{metadataReader.GetString(typeDefinition.Namespace)}.{metadataReader.GetString(typeDefinition.Name)}";
        }

        return null;
    }

    private static string? ReadSingleStringArgument(MetadataReader metadataReader, CustomAttribute attribute)
    {
        // Custom attribute blob: 0x0001 prolog, followed by the fixed arguments. Both attributes we
        // care about have a single string constructor argument.
        BlobReader blobReader = metadataReader.GetBlobReader(attribute.Value);
        if (blobReader.Length < 2 || blobReader.ReadUInt16() != 0x0001)
        {
            return null;
        }

        return blobReader.ReadSerializedString();
    }
}
