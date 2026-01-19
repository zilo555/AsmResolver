using AsmResolver.DotNet.Signatures;

namespace AsmResolver.DotNet
{
    /// <summary>
    /// Provides members for describing a method in a managed assembly.
    /// </summary>
    public interface IMethodDescriptor : IMemberDescriptor, IMetadataMember
    {
        /// <summary>
        /// Gets the name of the method.
        /// </summary>
        new Utf8String? Name
        {
            get;
        }

        /// <summary>
        /// Gets the signature of the method.
        /// </summary>
        MethodSignature? Signature
        {
            get;
        }

        /// <summary>
        /// Resolves the reference to a method definition, assuming the provided module as resolution context.
        /// </summary>
        /// <param name="context">The module to assume as resolution context.</param>
        /// <returns>The resolved method definition, or <c>null</c> if the method could not be resolved.</returns>
        new Result<MethodDefinition> Resolve(RuntimeContext? context);
    }
}
