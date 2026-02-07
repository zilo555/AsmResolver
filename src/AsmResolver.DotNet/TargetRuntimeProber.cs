using System.IO;
using AsmResolver.DotNet.Serialized;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE;
using AsmResolver.PE.DotNet.Metadata;
using AsmResolver.PE.DotNet.Metadata.Tables;

namespace AsmResolver.DotNet;

/// <summary>
/// Provides helper methods for inferring the .NET target runtime of an existing PE image.
/// </summary>
public static class TargetRuntimeProber
{
    /// <summary>
    /// Obtians the name and version of the .NET runtime a provided PE image likely is targeting.
    /// </summary>
    /// <param name="image">The image to classify.</param>
    /// <returns>The likely target .NET runtime version.</returns>
    public static DotNetRuntimeInfo GetLikelyTargetRuntime(PEImage image)
    {
        if (image.DotNetDirectory?.Metadata is not { } metadata)
            return DotNetRuntimeInfo.NetFramework(4, 0);

        var streams = metadata.GetImpliedStreamSelection();
        if (streams is not {TablesStream: not null, StringsStream: not null})
            return DotNetRuntimeInfo.NetFramework(4, 0);

        var bestMatch = DotNetRuntimeInfo.NetFramework(0, 0);

        // Check if we're corlib ourselves, then we can infer it directly from the definition.
        TraverseAssemblyDefinitions(ref streams, ref bestMatch);
        if (bestMatch.Version.Major != 0)
            return bestMatch;

        // Try inferring based on assembly references and TargetFrameworkAttribute on the assemblydef.
        TraverseAssemblyReferences(ref streams, ref bestMatch);
        TraverseTargetRuntimeAttribute(ref streams, ref bestMatch);

        if (bestMatch.Version.Major == 0)
            bestMatch = DotNetRuntimeInfo.NetFramework(4, 0);

        return bestMatch;
    }

    private static void TraverseAssemblyDefinitions(ref readonly MetadataStreamSelection streams, ref DotNetRuntimeInfo bestMatch)
    {
        var assemblyDefTable = streams.TablesStream!.GetTable<AssemblyDefinitionRow>(TableIndex.Assembly);
        if (assemblyDefTable.Count == 0)
            return;

        var row = assemblyDefTable.GetByRid(1);
        var name = streams.StringsStream!.GetStringByIndex(row.Name);
        if (Utf8String.IsNullOrEmpty(name))
            return;

        if (!KnownCorLibs.KnownCorLibNames.Contains(name))
            return;

        var newMatch = ToDotNetRuntimeInfo(
            name,
            row.MajorVersion,
            row.MinorVersion,
            row.BuildNumber,
            row.RevisionNumber
        );

        if (bestMatch.Version < newMatch.Version)
            bestMatch = newMatch;
    }

    private static void TraverseAssemblyReferences(ref readonly MetadataStreamSelection streams, ref DotNetRuntimeInfo dotNetRuntimeInfo)
    {
        var assemblyRefTable = streams.TablesStream!.GetTable<AssemblyReferenceRow>(TableIndex.AssemblyRef);
        for (uint rid = 1; rid <= assemblyRefTable.Count; rid++)
        {
            var row = assemblyRefTable.GetByRid(rid);
            var name = streams.StringsStream!.GetStringByIndex(row.Name);

            if (Utf8String.IsNullOrEmpty(name))
                continue;

            if (!KnownCorLibs.KnownCorLibNames.Contains(name))
                continue;

            var newMatch = ToDotNetRuntimeInfo(
                name,
                row.MajorVersion,
                row.MinorVersion,
                row.BuildNumber,
                row.RevisionNumber
            );

            if (dotNetRuntimeInfo.Version < newMatch.Version)
                dotNetRuntimeInfo = newMatch;
        }
    }

    private static void TraverseTargetRuntimeAttribute(ref readonly MetadataStreamSelection streams, ref DotNetRuntimeInfo bestMatch)
    {
        var tablesStream = streams.TablesStream!;
        var stringsStream = streams.StringsStream!;

        var blobStream = streams.BlobStream;
        if (blobStream is null)
            return;

        // Get relevant tables.
        var caTable = tablesStream.GetTable<CustomAttributeRow>(TableIndex.CustomAttribute);
        var memberTable = tablesStream.GetTable<MemberReferenceRow>(TableIndex.MemberRef);
        var typeTable = tablesStream.GetTable<TypeReferenceRow>(TableIndex.TypeRef);

        // Get relevant index decoders.
        var typeDecoder = tablesStream.GetIndexEncoder(CodedIndex.CustomAttributeType);
        var parentDecoder = tablesStream.GetIndexEncoder(CodedIndex.MemberRefParent);
        var hasCaDecoder = tablesStream.GetIndexEncoder(CodedIndex.HasCustomAttribute);

        // Find CAs that are owned by the assembly def.
        uint expectedOwner = hasCaDecoder.EncodeToken(new MetadataToken(TableIndex.Assembly, 1));
        if (!caTable.TryGetRidByKey(0 /* Parent */, expectedOwner, out uint startRid))
            return;

        // We may not have found the first one (TryGetRidByKey performs binary search).
        // Move back until we are at the first CA of the assembly.
        while (startRid > 1 && caTable.GetByRid(startRid - 1).Parent == expectedOwner)
            startRid--;

        // Traverse all CAs.
        for (uint rid = startRid; rid <= caTable.Count; rid++)
        {
            // Check if we're still a CA of the current assembly def.
            var row = caTable.GetByRid(rid);
            if (row.Parent != expectedOwner)
                break;

            // Look up CA constructor.
            var ctorToken = typeDecoder.DecodeIndex(row.Type);
            if (ctorToken.Table != TableIndex.MemberRef || !memberTable.TryGetByRid(ctorToken.Rid, out var memberRow))
                continue;

            // Look up declaring type of CA constructor.
            var typeToken = parentDecoder.DecodeIndex(memberRow.Parent);
            if (typeToken.Table != TableIndex.TypeRef || !typeTable.TryGetByRid(typeToken.Rid, out var typeRow))
                continue;

            // Compare namespace and name of attribute type.
            var ns = stringsStream.GetStringByIndex(typeRow.Namespace);
            var name = stringsStream.GetStringByIndex(typeRow.Name);
            if (ns != SerializedAssemblyDefinition.SystemRuntimeVersioningNamespace || name != SerializedAssemblyDefinition.TargetFrameworkAttributeName)
                continue;

            // Can we read the CA signature?
            if (!blobStream.TryGetBlobReaderByIndex(row.Value, out var reader))
                continue;

            // Verify magic header of CA blob.
            ushort prologue = reader.ReadUInt16();
            if (prologue != CustomAttributeSignature.CustomAttributeSignaturePrologue)
                continue;

            // Read first argument (target runtime string).
            var element = reader.ReadSerString();
            if (!Utf8String.IsNullOrEmpty(element) && DotNetRuntimeInfo.TryParse(element, out var info))
                bestMatch = info;
        }
    }

    /// <summary>
    /// Maps the corlib reference to the appropriate .NET or .NET Core version.
    /// </summary>
    /// <returns>The runtime information.</returns>
    public static DotNetRuntimeInfo ExtractDotNetRuntimeInfo(IResolutionScope corLibScope)
    {
        var assembly = corLibScope.GetAssembly();

        if (assembly is null)
            return DotNetRuntimeInfo.NetFramework(4, 0);

        string? name = assembly.Name?.Value;
        if (string.IsNullOrEmpty(name))
            return DotNetRuntimeInfo.NetFramework(4, 0);

        return ToDotNetRuntimeInfo(
            name!,
            assembly.Version.Major,
            assembly.Version.Minor,
            assembly.Version.Build,
            assembly.Version.Revision
        );
    }

    private static DotNetRuntimeInfo ToDotNetRuntimeInfo(string name, int major, int minor, int build, int revision)
    {
        if (major >= 5)
            return DotNetRuntimeInfo.NetCoreApp(major, minor);

        return name switch
        {
            "mscorlib" => DotNetRuntimeInfo.NetFramework(major, minor),
            "netstandard" => DotNetRuntimeInfo.NetStandard(major, minor),
            "System.Private.CoreLib" => DotNetRuntimeInfo.NetCoreApp(1, 0),
            "System.Runtime" => (major, minor, build, revision) switch
            {
                (4, 0, 20, 0) => DotNetRuntimeInfo.NetStandard(1, 3),
                (4, 1, 0, 0) => DotNetRuntimeInfo.NetStandard(1, 5),
                (4, 2, 1, 0) => DotNetRuntimeInfo.NetCoreApp(2, 1),
                _ => DotNetRuntimeInfo.NetCoreApp(3, 1),
            },
            _ => DotNetRuntimeInfo.NetCoreApp(major, minor)
        };
    }
}
