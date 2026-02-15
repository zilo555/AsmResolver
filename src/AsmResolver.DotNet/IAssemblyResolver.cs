namespace AsmResolver.DotNet
{
    /// <summary>
    /// Provides members for resolving references to external .NET assemblies.
    /// </summary>
    public interface IAssemblyResolver
    {
        /// <summary>
        /// Resolves a reference to an assembly.
        /// </summary>
        /// <param name="assembly">The reference to the assembly.</param>
        /// <param name="originModule">The module the assembly is assumed to be referenced in.</param>
        /// <param name="result">The resolved assembly, or <c>null</c> if resolution failed.</param>
        /// <returns>A value describing the success or failure status of the assembly resolution.</returns>
        ResolutionStatus Resolve(AssemblyDescriptor assembly, ModuleDefinition? originModule, out AssemblyDefinition? result);
    }
}
