using System;
using System.Collections.Generic;
using System.IO;
using AsmResolver.DotNet.Serialized;
using AsmResolver.DotNet.Signatures;
using AsmResolver.IO;
using AsmResolver.Shims;

namespace AsmResolver.DotNet
{
    /// <summary>
    /// Provides a base implementation of an assembly resolver, that includes a collection of search directories to look
    /// into for probing assemblies.
    /// </summary>
    public abstract class AssemblyResolverBase : IAssemblyResolver
    {
        private static readonly string[] BinaryFileExtensions = {".dll", ".exe"};
        private static readonly SignatureComparer Comparer = new(SignatureComparisonFlags.AcceptNewerVersions);

        /// <summary>
        /// Initializes the base of an assembly resolver.
        /// </summary>
        /// <param name="fileService">The service to use for reading files from the disk.</param>
        protected AssemblyResolverBase(IFileService fileService)
        {
            ReaderParameters = new ModuleReaderParameters(fileService);
        }

        /// <summary>
        /// Initializes the base of an assembly resolver.
        /// </summary>
        /// <param name="readerParameters">The reader parameters used for reading new resolved assemblies.</param>
        protected AssemblyResolverBase(ModuleReaderParameters readerParameters)
        {
            ReaderParameters = readerParameters;
        }

        /// <summary>
        /// Gets the file service that is used for reading files from the disk.
        /// </summary>
        public IFileService FileService => ReaderParameters.PEReaderParameters.FileService;

        /// <summary>
        /// Gets the reader parameters used for reading new resolved assemblies.
        /// </summary>
        public ModuleReaderParameters ReaderParameters
        {
            get;
        }

        /// <summary>
        /// Gets a collection of custom search directories that are probed upon resolving a reference
        /// to an assembly.
        /// </summary>
        public IList<string> SearchDirectories
        {
            get;
        } = new List<string>();

        /// <inheritdoc />
        public Result<AssemblyDefinition> Resolve(AssemblyDescriptor assembly, ModuleDefinition? originModule = null)
        {
            // Prefer assemblies in the search directories, in case .NET libraries are shipped with the application.
            string? path = ProbeSearchDirectories(assembly, originModule);

            if (string.IsNullOrEmpty(path))
            {
                // If failed, probe the runtime installation directories.
                if (assembly.GetPublicKeyToken() is not null)
                    path = ProbeRuntimeDirectories(assembly);

                // If still no suitable file was found, abort.
                if (string.IsNullOrEmpty(path))
                {
                    return Result.Fail<AssemblyDefinition>(new FileNotFoundException(
                        $"Could not find the assembly file for {assembly.SafeToString()}."
                    ));
                }
            }

            // Attempt to load the file.
            try
            {
                return Result.Success(LoadAssemblyFromFile(path!));
            }
            catch (Exception ex)
            {
                // Wrap into error object.
                return Result.Fail<AssemblyDefinition>(ex);
            }
        }

        /// <summary>
        /// Attempts to read an assembly from its file path.
        /// </summary>
        /// <param name="path">The path to the assembly.</param>
        /// <returns>The assembly.</returns>
        protected virtual AssemblyDefinition LoadAssemblyFromFile(string path)
        {
            return AssemblyDefinition.FromFile(FileService.OpenFile(path), ReaderParameters);
        }

        /// <summary>
        /// Probes all search directories in <see cref="SearchDirectories"/> for the provided assembly.
        /// </summary>
        /// <param name="assembly">The assembly descriptor to search.</param>
        /// <param name="originModule">The module to assume the assembly was referenced in.</param>
        /// <returns>The path to the assembly, or <c>null</c> if none was found.</returns>
        protected string? ProbeSearchDirectories(AssemblyDescriptor assembly, ModuleDefinition? originModule)
        {
            // Try directory of module as a search path.
            if (originModule is {FilePath: { } filePath}
                && Path.GetDirectoryName(filePath) is { } moduleDirectory
                && ProbeDirectory(assembly, moduleDirectory) is { } moduleDirectoryPath)
            {
                return moduleDirectoryPath;
            }

            // Try working directory as specified in reader parameters.
            if (ReaderParameters.WorkingDirectory is { } workingDirectory
                && ProbeDirectory(assembly, workingDirectory) is { } workingDirectoryPath)
            {
                return workingDirectoryPath;
            }

            // Probe other custom search paths.
            for (int i = 0; i < SearchDirectories.Count; i++)
            {
                string? path = ProbeDirectory(assembly, SearchDirectories[i]);
                if (!string.IsNullOrEmpty(path))
                    return path;
            }

            return null;
        }

        /// <summary>
        /// Probes all known runtime directories for the provided assembly.
        /// </summary>
        /// <param name="assembly">The assembly descriptor to search.</param>
        /// <returns>The path to the assembly, or <c>null</c> if none was found.</returns>
        protected abstract string? ProbeRuntimeDirectories(AssemblyDescriptor assembly);

        /// <summary>
        /// Probes a directory for the provided assembly.
        /// </summary>
        /// <param name="assembly">The assembly descriptor to search.</param>
        /// <param name="directory">The path to the directory to probe.</param>
        /// <returns>The path to the assembly, or <c>null</c> if none was found.</returns>
        protected static string? ProbeDirectory(AssemblyDescriptor assembly, string directory)
        {
            if (assembly.Name is null)
                return null;

            string path;

            // If culture is set, prefer the subdirectory with the culture.
            if (!string.IsNullOrEmpty(assembly.Culture))
            {
                path = PathShim.Combine(directory, assembly.Culture!, assembly.Name);
                string? result = ProbeFileFromFilePathWithoutExtension(path)
                                 ?? ProbeFileFromFilePathWithoutExtension(Path.Combine(path, assembly.Name));
                if (result is not null)
                    return result;
            }

            // If that fails, assume neutral culture.
            path = Path.Combine(directory, assembly.Name);
            return ProbeFileFromFilePathWithoutExtension(path)
                   ?? ProbeFileFromFilePathWithoutExtension(Path.Combine(path, assembly.Name));
        }

        internal static string? ProbeFileFromFilePathWithoutExtension(string baseFilePath)
        {
            foreach (string extension in BinaryFileExtensions)
            {
                string path = baseFilePath + extension;
                if (File.Exists(path))
                    return path;
            }

            return null;
        }
    }
}
