using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AsmResolver.DotNet.Bundles;
using AsmResolver.DotNet.Serialized;
using AsmResolver.DotNet.Signatures;
using AsmResolver.IO;
using AsmResolver.PE;
using AsmResolver.PE.File;

namespace AsmResolver.DotNet;

/// <summary>
/// Describes a context in which a .NET runtime is active.
/// </summary>
public partial class RuntimeContext
{
    private readonly Dictionary<AssemblyDescriptor, AssemblyDefinition> _loadedAssemblies = new(
        new SignatureComparer(SignatureComparisonFlags.VersionAgnostic)
    );
    private readonly ConcurrentDictionary<ITypeDescriptor, TypeDefinition> _typeCache = new();

    /// <summary>
    /// Creates a new runtime context.
    /// </summary>
    /// <param name="targetRuntime">The target runtime version.</param>
    public RuntimeContext(DotNetRuntimeInfo targetRuntime)
        : this(targetRuntime, null, null, null)
    {
    }

    /// <summary>
    /// Creates a new runtime context.
    /// </summary>
    /// <param name="targetRuntime">The target runtime version.</param>
    /// <param name="readerParameters">The parameters to use when reading modules in this context.</param>
    public RuntimeContext(DotNetRuntimeInfo targetRuntime, ModuleReaderParameters readerParameters)
        : this(targetRuntime, null, null, readerParameters)
    {
    }

    /// <summary>
    /// Creates a new runtime context.
    /// </summary>
    /// <param name="targetRuntime">The target runtime version.</param>
    /// <param name="searchDirectories">Additional search directories to assume when resolving dependencies.</param>
    public RuntimeContext(DotNetRuntimeInfo targetRuntime, IEnumerable<string> searchDirectories)
        : this(targetRuntime, null, null, null)
    {
        var dirs = ((AssemblyResolverBase) AssemblyResolver).SearchDirectories;
        foreach (string directory in searchDirectories)
            dirs.Add(directory);
    }

    /// <summary>
    /// Creates a new runtime context.
    /// </summary>
    /// <param name="targetRuntime">The target runtime version.</param>
    /// <param name="assemblyResolver">The assembly resolver to use when resolving assemblies into this context.</param>
    public RuntimeContext(DotNetRuntimeInfo targetRuntime, IAssemblyResolver assemblyResolver)
        : this(targetRuntime, assemblyResolver, null, null)
    {
    }

    /// <summary>
    /// Creates a new runtime context.
    /// </summary>
    /// <param name="targetRuntime">The target runtime version.</param>
    /// <param name="assemblyResolver">The assembly resolver to use when resolving assemblies into this context, or the default resolver if null.</param>
    /// <param name="corLibReference">The core library for this runtime context, or the assumed one from the version if null.</param>
    /// <param name="readerParameters">The parameters to use when reading modules in this context, or the default ones if null.</param>
    public RuntimeContext(
        DotNetRuntimeInfo targetRuntime,
        IAssemblyResolver? assemblyResolver,
        AssemblyDescriptor? corLibReference,
        ModuleReaderParameters? readerParameters)
    {
        DefaultReaderParameters = readerParameters is not null
            ? new ModuleReaderParameters(readerParameters) { RuntimeContext = this }
            : new ModuleReaderParameters(new ByteArrayFileService()) { RuntimeContext = this };

        TargetRuntime = targetRuntime;
        AssemblyResolver = assemblyResolver ?? CreateAssemblyResolver(targetRuntime, DefaultReaderParameters);
        RuntimeCorLib = corLibReference ?? targetRuntime.GetAssumedImplCorLib();
        SignatureComparer = new SignatureComparer(this, SignatureComparisonFlags.VersionAgnostic);
    }

    /// <summary>
    /// Creates a new runtime context for the provided bundled application.
    /// </summary>
    /// <param name="manifest">The bundle to create the runtime context for.</param>
    public RuntimeContext(BundleManifest manifest)
        : this(manifest, new ModuleReaderParameters(new ByteArrayFileService()))
    {
    }

    /// <summary>
    /// Creates a new runtime context.
    /// </summary>
    /// <param name="manifest">The bundle to create the runtime context for.</param>
    /// <param name="readerParameters">The parameters to use when reading modules in this context.</param>
    public RuntimeContext(BundleManifest manifest, ModuleReaderParameters readerParameters)
    {
        TargetRuntime = manifest.GetTargetRuntime();
        DefaultReaderParameters = new ModuleReaderParameters(readerParameters) {RuntimeContext = this};
        AssemblyResolver = new BundleAssemblyResolver(manifest, DefaultReaderParameters);

        if (ResolveAssembly(TargetRuntime.GetDefaultCorLib(), null, out var corlib) == ResolutionStatus.Success
            && corlib!.ManifestModule?.CorLibTypeFactory.Object.TryResolve(this, out var type) is true)
        {
            RuntimeCorLib = type.DeclaringModule?.Assembly;
            ;
        }

        SignatureComparer = new SignatureComparer(this, SignatureComparisonFlags.VersionAgnostic);
    }

    /// <summary>
    /// Gets the runtime version this context is targeting.
    /// </summary>
    public DotNetRuntimeInfo TargetRuntime
    {
        get;
    }

    /// <summary>
    /// Gets the default parameters that are used for reading .NET modules in the context.
    /// </summary>
    public ModuleReaderParameters DefaultReaderParameters
    {
        get;
    }

    /// <summary>
    /// Gets the assembly resolver that the context uses to resolve assemblies.
    /// </summary>
    public IAssemblyResolver AssemblyResolver
    {
        get;
    }

    /// <summary>
    /// Gets the corlib for this runtime
    /// </summary>
    public AssemblyDescriptor? RuntimeCorLib
    {
        get;
    }

    /// <summary>
    /// Gets the default signature comparer to use in this runtime context.
    /// </summary>
    public SignatureComparer SignatureComparer
    {
        get;
    }

    private static AssemblyResolverBase CreateAssemblyResolver(
        DotNetRuntimeInfo runtime,
        ModuleReaderParameters readerParameters)
    {
        switch (runtime.Name)
        {
            case DotNetRuntimeInfo.NetFrameworkName:
            case DotNetRuntimeInfo.NetStandardName when string.IsNullOrEmpty(DotNetCorePathProvider.DefaultInstallationPath):
                return new DotNetFrameworkAssemblyResolver(readerParameters, MonoPathProvider.Default);

            case DotNetRuntimeInfo.NetStandardName when DotNetCorePathProvider.Default.TryGetLatestStandardCompatibleVersion(runtime.Version, out var coreVersion):
                return new DotNetCoreAssemblyResolver(coreVersion, readerParameters);

            case DotNetRuntimeInfo.NetCoreAppName:
                return new DotNetCoreAssemblyResolver(runtime.Version, readerParameters);

            default:
                return new DotNetFrameworkAssemblyResolver(readerParameters, MonoPathProvider.Default);
        }
    }

    /// <summary>
    /// Loads a .NET assembly into the context from the provided input buffer.
    /// </summary>
    /// <param name="buffer">The raw contents of the executable file to load.</param>
    /// <returns>The module.</returns>
    /// <exception cref="BadImageFormatException">Occurs when the image does not contain a valid .NET metadata directory.</exception>
    public AssemblyDefinition LoadAssembly(byte[] buffer)
        => GetOrAddAssembly(AssemblyDefinition.FromBytes(buffer, DefaultReaderParameters));

    /// <summary>
    /// Loads a .NET assembly into the context from the provided input stream.
    /// </summary>
    /// <param name="stream">The raw contents of the executable file to load.</param>
    /// <returns>The module.</returns>
    /// <exception cref="BadImageFormatException">Occurs when the image does not contain a valid .NET metadata directory.</exception>
    public AssemblyDefinition LoadAssembly(Stream stream)
        => GetOrAddAssembly(AssemblyDefinition.FromStream(stream, DefaultReaderParameters));

    /// <summary>
    /// Loads a .NET assembly into the context from the provided input file.
    /// </summary>
    /// <param name="filePath">The file path to the input executable to load.</param>
    /// <returns>The module.</returns>
    /// <exception cref="BadImageFormatException">Occurs when the image does not contain a valid .NET metadata directory.</exception>
    public AssemblyDefinition LoadAssembly(string filePath)
        => GetOrAddAssembly(AssemblyDefinition.FromFile(filePath, DefaultReaderParameters));

    /// <summary>
    /// Loads a .NET assembly into the context from the provided input file.
    /// </summary>
    /// <param name="file">The portable executable file to load.</param>
    /// <returns>The module.</returns>
    /// <exception cref="BadImageFormatException">Occurs when the image does not contain a valid .NET metadata directory.</exception>
    public AssemblyDefinition LoadAssembly(PEFile file)
        => GetOrAddAssembly(AssemblyDefinition.FromFile(file, DefaultReaderParameters));

    /// <summary>
    /// Loads a .NET assembly into the context from the provided input file.
    /// </summary>
    /// <param name="file">The portable executable file to load.</param>
    /// <returns>The module.</returns>
    /// <exception cref="BadImageFormatException">Occurs when the image does not contain a valid .NET metadata directory.</exception>
    public AssemblyDefinition LoadAssembly(IInputFile file)
        => GetOrAddAssembly(AssemblyDefinition.FromFile(file, DefaultReaderParameters));

    /// <summary>
    /// Loads a .NET assembly into the context from an input stream.
    /// </summary>
    /// <param name="reader">The input stream pointing at the beginning of the executable to load.</param>
    /// <param name="mode">Indicates the input PE is mapped or unmapped.</param>
    /// <returns>The module.</returns>
    /// <exception cref="BadImageFormatException">Occurs when the image does not contain a valid .NET metadata directory.</exception>
    public AssemblyDefinition LoadAssembly(in BinaryStreamReader reader, PEMappingMode mode = PEMappingMode.Unmapped)
        => LoadAssembly(PEFile.FromReader(reader, mode));

    /// <summary>
    /// Loads a .NET assembly into the context from a PE image.
    /// </summary>
    /// <param name="peImage">The image containing the .NET metadata.</param>
    /// <returns>The module.</returns>
    /// <exception cref="BadImageFormatException">Occurs when the image does not contain a valid .NET metadata directory.</exception>
    public AssemblyDefinition LoadAssembly(PEImage peImage)
        => GetOrAddAssembly(AssemblyDefinition.FromImage(peImage, DefaultReaderParameters));

    private void AssertNoOwner(AssemblyDefinition assembly)
    {
        if (assembly.RuntimeContext is not null)
            throw new ArgumentException($"Assembly {assembly.SafeToString()} is already added to another context.");
    }

    /// <summary>
    /// Registers an assembly in the context.
    /// </summary>
    /// <param name="assembly">The assembly to add</param>
    /// <exception cref="ArgumentException">
    /// Occurs when the assembly was already added to another context, or when there already exists an assembly with
    /// the same name in the context.
    /// </exception>
    public void AddAssembly(AssemblyDefinition assembly)
    {
        lock (_loadedAssemblies)
        {
            AssertNoOwner(assembly);

            if (_loadedAssemblies.ContainsKey(assembly))
                throw new ArgumentException($"Another assembly with name {assembly.Name} was already added to this context.", nameof(assembly));

            _loadedAssemblies.Add(assembly, assembly);
            assembly.RuntimeContext = this;
        }
    }

    private AssemblyDefinition GetOrAddAssembly(AssemblyDefinition assembly)
    {
        lock (_loadedAssemblies)
        {
            if (_loadedAssemblies.TryGetValue(assembly, out var resolved))
                return resolved;

            AddAssembly(assembly);
            return assembly;
        }
    }

    /// <summary>
    /// Enumerates all assemblies that were loaded in the context.
    /// </summary>
    /// <returns>The assemblies.</returns>
    public IEnumerable<AssemblyDefinition> GetLoadedAssemblies()
    {
        lock (_loadedAssemblies)
        {
            return _loadedAssemblies.Values.ToArray();
        }
    }

}
