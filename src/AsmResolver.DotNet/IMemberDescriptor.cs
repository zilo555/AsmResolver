namespace AsmResolver.DotNet
{
    /// <summary>
    /// Provides members for describing a (reference to a) member defined in a .NET assembly.
    /// </summary>
    public interface IMemberDescriptor : IFullNameProvider, IModuleProvider, IImportable
    {
        /// <summary>
        /// When this member is defined in a type, gets the enclosing type.
        /// </summary>
        ITypeDescriptor? DeclaringType
        {
            get;
        }

        /// <summary>
        /// Resolves the reference to a member definition, assuming the provided module as resolution context.
        /// </summary>
        /// <param name="context">The module to assume as resolution context.</param>
        /// <returns>The resolved member definition, or <c>null</c> if the member could not be resolved.</returns>
        Result<IMemberDefinition> Resolve(RuntimeContext? context);
    }
}
