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
        /// Attempts to resolve the method reference its definition, assuming the provided module as resolution context.
        /// </summary>
        /// <param name="context">The context to assume when resolving the method.</param>
        /// <param name="definition">The resolved method definition, or <c>null</c> if the method could not be resolved.</param>
        /// <returns>A value describing the success or failure status of the method resolution.</returns>
        ResolutionStatus Resolve(RuntimeContext? context, out MethodDefinition? definition);
    }
}
