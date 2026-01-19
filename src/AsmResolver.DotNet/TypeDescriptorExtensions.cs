using System;
using AsmResolver.DotNet.Signatures;

namespace AsmResolver.DotNet
{
    /// <summary>
    /// Provides convenience extension methods to instances of <see cref="ITypeDescriptor"/>.
    /// </summary>
    public static class TypeDescriptorExtensions
    {
        /// <param name="type">The element type.</param>
        extension(ITypeDescriptor type)
        {
            /// <summary>
            /// Constructs a new generic instance type signature with the provided type descriptor as element type.
            /// as element type.
            /// </summary>
            /// <param name="context">The runtime context to assume when constructing the signature, if any.</param>
            /// <param name="typeArguments">The arguments to instantiate the type with.</param>
            /// <returns>The constructed by-reference type signature.</returns>
            public GenericInstanceTypeSignature MakeGenericInstanceType(RuntimeContext? context, params TypeSignature[] typeArguments)
            {
                return type.ToTypeDefOrRef().MakeGenericInstanceType(type.GetIsValueType(context), typeArguments);
            }

            /// <summary>
            /// Determines whether a type matches a namespace and name pair.
            /// </summary>
            /// <param name="ns">The namespace.</param>
            /// <param name="name">The name.</param>
            /// <returns><c>true</c> if the name and the namespace of the type matches the provided values,
            /// <c>false</c> otherwise.</returns>
            public bool IsTypeOf(string? ns, string? name)
                => type.Name == name && type.Namespace == ns;
        }

        /// <param name="type">The element type.</param>
        extension(ITypeDefOrRef type)
        {
            /// <summary>
            /// Constructs a new generic instance type signature with the provided type descriptor as element type.
            /// as element type.
            /// </summary>
            /// <param name="isValueType"><c>true</c> if the type is a value type, <c>false</c> otherwise.</param>
            /// <param name="typeArguments">The arguments to instantiate the type with.</param>
            /// <returns>The constructed by-reference type signature.</returns>
            /// <remarks>
            /// This function can be used to avoid type resolution on type references.
            /// </remarks>
            public GenericInstanceTypeSignature MakeGenericInstanceType(bool isValueType, params TypeSignature[] typeArguments)
            {
                return new GenericInstanceTypeSignature(type, isValueType, typeArguments);
            }

            /// <summary>
            /// Transforms the type descriptor to an instance of a <see cref="TypeSignature"/>, which can be used in
            /// blob signatures.
            /// </summary>
            /// <param name="context">The runtime context to assume when constructing the signature, if any.</param>
            /// <returns>The constructed type signature instance.</returns>
            /// <remarks>
            /// This function can be used to avoid type resolution on type references.
            /// </remarks>
            public TypeSignature ToTypeSignature(RuntimeContext? context)
            {
                return type.ToTypeSignature(type.GetIsValueType(context));
            }

            /// <summary>
            /// Constructs a reference to a nested type.
            /// </summary>
            /// <param name="nestedTypeName">The name of the nested type.</param>
            /// <returns>The constructed reference.</returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Occurs when the type cannot be used as a declaring type of the reference.
            /// </exception>
            public TypeReference CreateTypeReference(string nestedTypeName)
            {
                var parent = type switch
                {
                    TypeReference reference => reference,
                    TypeDefinition definition => definition.ToTypeReference(),
                    _ => throw new ArgumentOutOfRangeException()
                };

                return new TypeReference(type.ContextModule, parent, null, nestedTypeName);
            }

            /// <summary>
            /// Constructs a reference to a nested type.
            /// </summary>
            /// <param name="nestedTypeName">The name of the nested type.</param>
            /// <returns>The constructed reference.</returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Occurs when the type cannot be used as a declaring type of the reference.
            /// </exception>
            public TypeReference CreateTypeReference(Utf8String nestedTypeName)
            {
                var parent = type switch
                {
                    TypeReference reference => reference,
                    TypeDefinition definition => definition.ToTypeReference(),
                    _ => throw new ArgumentOutOfRangeException()
                };

                return new TypeReference(type.ContextModule, parent, null, nestedTypeName);
            }

            /// <summary>
            /// Determines whether a type matches a namespace and name pair.
            /// </summary>
            /// <param name="ns">The namespace.</param>
            /// <param name="name">The name.</param>
            /// <returns><c>true</c> if the name and the namespace of the type matches the provided values,
            /// <c>false</c> otherwise.</returns>
            public bool IsTypeOfUtf8(Utf8String? ns, Utf8String? name)
                => type.Name == name && type.Namespace == ns;
        }

        /// <param name="scope">The scope the type is defined in.</param>
        extension(IResolutionScope scope)
        {
            /// <summary>
            /// Constructs a reference to a type within the provided resolution scope.
            /// </summary>
            /// <param name="ns">The namespace of the type.</param>
            /// <param name="name">The name of the type.</param>
            /// <returns>The constructed reference.</returns>
            public TypeReference CreateTypeReference(string? ns, string name)
            {
                return new TypeReference(scope.ContextModule, scope, ns, name);
            }

            /// <summary>
            /// Constructs a reference to a type within the provided resolution scope.
            /// </summary>
            /// <param name="ns">The namespace of the type.</param>
            /// <param name="name">The name of the type.</param>
            /// <returns>The constructed reference.</returns>
            public TypeReference CreateTypeReference(Utf8String? ns, Utf8String name)
            {
                return new TypeReference(scope.ContextModule, scope, ns, name);
            }
        }

        /// <param name="parent">The declaring member.</param>
        extension(IMemberRefParent parent)
        {
            /// <summary>
            /// Constructs a reference to a member declared within the provided parent member.
            /// </summary>
            /// <param name="memberName">The name of the member to reference.</param>
            /// <param name="signature">The signature of the member to reference.</param>
            /// <returns>The constructed reference.</returns>
            public MemberReference CreateMemberReference(string? memberName, MemberSignature? signature)
            {
                return new MemberReference(parent, memberName, signature);
            }

            /// <summary>
            /// Constructs a reference to a member declared within the provided parent member.
            /// </summary>
            /// <param name="memberName">The name of the member to reference.</param>
            /// <param name="signature">The signature of the member to reference.</param>
            /// <returns>The constructed reference.</returns>
            public MemberReference CreateMemberReference(Utf8String? memberName, MemberSignature? signature)
            {
                return new MemberReference(parent, memberName, signature);
            }
        }

    }
}
