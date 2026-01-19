using AsmResolver.DotNet.Signatures;

namespace AsmResolver.DotNet
{
    /// <summary>
    /// Provides members for describing a type in a managed assembly.
    /// </summary>
    public interface ITypeDescriptor : IMemberDescriptor
    {
        /// <summary>
        /// Gets the namespace the type resides in.
        /// </summary>
        string? Namespace
        {
            get;
        }

        /// <summary>
        /// Gets the resolution scope that defines the type.
        /// </summary>
        IResolutionScope? Scope
        {
            get;
        }

        bool GetIsValueType(RuntimeContext? context);

        /// <summary>
        /// Resolves the reference to a method definition, assuming the provided module as resolution context.
        /// </summary>
        /// <returns>The resolved method definition, or <c>null</c> if the method could not be resolved.</returns>
        new Result<TypeDefinition> Resolve(RuntimeContext? context);

        /// <summary>
        /// Transforms the type descriptor to an instance of a <see cref="ITypeDefOrRef"/>, which can be referenced by
        /// a metadata token.
        /// </summary>
        /// <returns>The constructed TypeDefOrRef instance.</returns>
        ITypeDefOrRef ToTypeDefOrRef();

        TypeSignature ToTypeSignature(RuntimeContext? context);
    }
}
