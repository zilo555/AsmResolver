using System;
using System.IO;
using AsmResolver.DotNet.Serialized;

namespace AsmResolver.DotNet.Bundles;

/// <summary>
/// Provides an implementation of an assembly resolver that prefers assemblies embedded in single-file-host executable.
/// </summary>
public class BundleAssemblyResolver : IAssemblyResolver
{
    private readonly BundleManifest _manifest;
    private readonly DotNetCoreAssemblyResolver _baseResolver;

    internal BundleAssemblyResolver(BundleManifest manifest, ModuleReaderParameters readerParameters)
    {
        _manifest = manifest;

        // Bundles are .NET core 3.1+ only -> we can always default to .NET Core assembly resolution.
        _baseResolver = new DotNetCoreAssemblyResolver(manifest.GetTargetRuntime().Version, readerParameters);
    }

    /// <inheritdoc />
    public Result<AssemblyDefinition> Resolve(AssemblyDescriptor assembly, ModuleDefinition? originModule)
    {
        // Prefer embedded files before we forward to the default assembly resolution algorithm.

        var result = TryResolveFromEmbeddedFiles(assembly);
        return result.IsSuccess
            ? result
            : _baseResolver.Resolve(assembly, originModule);
    }

    private Result<AssemblyDefinition> TryResolveFromEmbeddedFiles(AssemblyDescriptor assembly)
    {
        try
        {
            for (int i = 0; i < _manifest.Files.Count; i++)
            {
                var file = _manifest.Files[i];
                if (file.Type != BundleFileType.Assembly)
                    continue;

                if (Path.GetFileNameWithoutExtension(file.RelativePath) == assembly.Name)
                    return Result.Success(AssemblyDefinition.FromBytes(file.GetData(), _baseResolver.ReaderParameters));
            }
        }
        catch (Exception ex)
        {
            return Result.Fail<AssemblyDefinition>(ex);
        }

        return Result.Fail<AssemblyDefinition>();
    }
}
