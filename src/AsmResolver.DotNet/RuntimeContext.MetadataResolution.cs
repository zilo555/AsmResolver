using System;
using System.Collections.Generic;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;

namespace AsmResolver.DotNet
{
    /// <summary>
    /// Provides a default implementation for the <see cref="IMetadataResolver"/> interface.
    /// </summary>
    public partial class RuntimeContext
    {
        /// <summary>
        /// Resolves a reference to a type.
        /// </summary>
        /// <param name="type">The type to resolve.</param>
        /// <returns>The type definition, or <c>null</c> if the type could not be resolved.</returns>
        public Result<TypeDefinition> ResolveType(ITypeDescriptor? type, ModuleDefinition? originModule = null)
        {
            return type switch
            {
                TypeDefinition definition when definition.DeclaringModule == originModule => Result.Success(definition),
                TypeDefinition definition => ResolveTypeReference(definition, originModule),
                TypeReference reference => ResolveTypeReference(reference, originModule),
                TypeSpecification specification => ResolveType(specification.Signature, originModule),
                TypeSignature signature => ResolveTypeSignature(signature, originModule),
                ExportedType exportedType => ResolveExportedType(exportedType),
                _ => Result.Fail<TypeDefinition>()
            };
        }

        private TypeDefinition? LookupInCache(ITypeDescriptor type)
        {
            if (_typeCache.TryGetValue(type, out var typeDef))
            {
                // Check if type definition has changed since last lookup.
                if (typeDef.IsTypeOf(type.Namespace, type.Name))
                    return typeDef;
                _typeCache.TryRemove(type, out _);
            }

            return null;
        }

        private Result<TypeDefinition> ResolveTypeReference(ITypeDefOrRef? reference, ModuleDefinition? originModule)
        {
            if (reference is null)
                return Result.Fail<TypeDefinition>();

            if (LookupInCache(reference) is { } cachedType)
                return Result.Success(cachedType);

            var resolution = new TypeResolution(this);
            var result = resolution.ResolveTypeReference(reference, originModule);
            if (result.IsSuccess)
                _typeCache[reference] = result.Resolved;

            return result;
        }

        private Result<TypeDefinition> ResolveExportedType(ExportedType? exportedType)
        {
            if (exportedType is null)
                return Result.Fail<TypeDefinition>();

            if (LookupInCache(exportedType) is { } cachedType)
                return Result.Success(cachedType);

            var resolution = new TypeResolution(this);
            var result = resolution.ResolveExportedType(exportedType);
            if (result.IsSuccess)
                _typeCache[exportedType] = result.Resolved;

            return result;
        }

        private Result<TypeDefinition> ResolveTypeSignature(TypeSignature? signature, ModuleDefinition? originModule)
        {
            var type = signature?.GetUnderlyingTypeDefOrRef();
            if (type is null)
                return Result.Fail<TypeDefinition>();

            return type.MetadataToken.Table switch
            {
                TableIndex.TypeDef => Result.Success((TypeDefinition) type),
                TableIndex.TypeRef => ResolveTypeReference((TypeReference) type, originModule),
                TableIndex.TypeSpec => ResolveTypeSignature(((TypeSpecification) type).Signature, originModule),
                _ => Result.Fail<TypeDefinition>()
            };
        }

        /// <summary>
        /// Resolves a reference to a method.
        /// </summary>
        /// <param name="method">The method. to resolve.</param>
        /// <returns>The method definition, or <c>null</c> if the method could not be resolved.</returns>
        public Result<MethodDefinition> ResolveMethod(IMethodDescriptor? method, ModuleDefinition? originModule = null)
        {
            if (method is null)
                return Result.Fail<MethodDefinition>();

            var result = ResolveType(method.DeclaringType, originModule);
            if (!result.IsSuccess)
                return result.Into<MethodDefinition>();

            var declaringType = result.Resolved;
            for (int i = 0; i < declaringType.Methods.Count; i++)
            {
                var candidate = declaringType.Methods[i];
                if (candidate.Name == method.Name && _comparer.Equals(method.Signature, candidate.Signature))
                    return Result.Success(candidate);
            }

            return Result.Fail<MethodDefinition>();
        }

        /// <summary>
        /// Resolves a reference to a field.
        /// </summary>
        /// <param name="field">The field to resolve.</param>
        /// <returns>The field definition, or <c>null</c> if the field could not be resolved.</returns>
        public Result<FieldDefinition> ResolveField(IFieldDescriptor? field, ModuleDefinition? originModule = null)
        {
            if (field is null)
                return Result.Fail<FieldDefinition>();

            var result = ResolveType(field.DeclaringType, originModule);
            if (!result.IsSuccess)
                return result.Into<FieldDefinition>();

            var declaringType = result.Resolved;
            for (int i = 0; i < declaringType.Fields.Count; i++)
            {
                var candidate = declaringType.Fields[i];
                if (candidate.Name == field.Name && _comparer.Equals(field.Signature, candidate.Signature))
                    return Result.Success(candidate);
            }

            return Result.Fail<FieldDefinition>();
        }

        private struct TypeResolution
        {
            private readonly RuntimeContext _context;
            private readonly Stack<IResolutionScope> _scopeStack;
            private readonly Stack<IImplementation> _implementationStack;

            public TypeResolution(RuntimeContext context)
            {
                _context = context;
                _scopeStack = new Stack<IResolutionScope>();
                _implementationStack = new Stack<IImplementation>();
            }

            public Result<TypeDefinition> ResolveTypeReference(ITypeDefOrRef? reference, ModuleDefinition? originModule)
            {
                if (reference is null)
                    return Result.Fail<TypeDefinition>();

                var scope = reference.Scope ?? reference.ContextModule;
                if (reference.Name is null || scope is null || _scopeStack.Contains(scope))
                    return Result.Fail<TypeDefinition>();

                _scopeStack.Push(scope);

                switch (scope.MetadataToken.Table)
                {
                    case TableIndex.AssemblyRef:
                        var assemblyRefScope = scope.GetAssembly();

                        // Are we referencing the current assembly the reference was declared in?
                        if (reference.ContextModule?.Assembly is { } referenceAssembly
                            && SignatureComparer.Default.Equals(assemblyRefScope, referenceAssembly))
                        {
                            return FindTypeInModule(reference.ContextModule, reference.Namespace, reference.Name);
                        }

                        // Are we referencing the current assembly of the resolver itself?
                        if (originModule?.Assembly is { } resolverAssembly
                            && SignatureComparer.Default.Equals(assemblyRefScope, resolverAssembly))
                        {
                            return FindTypeInModule(originModule, reference.Namespace, reference.Name);
                        }

                        // Otherwise, resolve the assembly first.
                        var assemblyDefScope= _context.AssemblyResolver.Resolve((AssemblyReference) scope);
                        return assemblyDefScope.IsSuccess
                            ? FindTypeInAssembly(assemblyDefScope.Resolved, reference.Namespace, reference.Name)
                            : assemblyDefScope.Into<TypeDefinition>();

                    case TableIndex.Module:
                        return FindTypeInModule((ModuleDefinition) scope, reference.Namespace, reference.Name);

                    case TableIndex.TypeRef:
                        var typeDefScope = ResolveTypeReference((TypeReference) scope, originModule);
                        return typeDefScope.IsSuccess
                            ? FindTypeInType(typeDefScope.Resolved, reference.Name)
                            : typeDefScope.Into<TypeDefinition>();

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            public Result<TypeDefinition> ResolveExportedType(ExportedType? exportedType)
            {
                var implementation = exportedType?.Implementation;
                if (exportedType?.Name is null || implementation is null || _implementationStack.Contains(implementation))
                    return Result.Fail<TypeDefinition>();

                _implementationStack.Push(implementation);

                switch (implementation.MetadataToken.Table)
                {
                    case TableIndex.AssemblyRef:
                        var assembly = _context.AssemblyResolver.Resolve((AssemblyReference) implementation);
                        return assembly.IsSuccess
                            ? FindTypeInAssembly(assembly.Resolved, exportedType.Namespace, exportedType.Name)
                            : assembly.Into<TypeDefinition>();

                    case TableIndex.File when !string.IsNullOrEmpty(implementation.Name):
                        var module = FindModuleInAssembly(exportedType.ContextModule!.Assembly!, implementation.Name!);
                        return module.IsSuccess
                            ? FindTypeInModule(module.Resolved, exportedType.Namespace, exportedType.Name)
                            : module.Into<TypeDefinition>();

                    case TableIndex.ExportedType:
                        var exportedDeclaringType = (ExportedType) implementation;
                        var declaringType = ResolveExportedType(exportedDeclaringType);
                        return declaringType.IsSuccess
                            ? FindTypeInType(declaringType.Resolved, exportedType.Name)
                            : declaringType.Into<TypeDefinition>();

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            private Result<TypeDefinition> FindTypeInAssembly(AssemblyDefinition assembly, Utf8String? ns, Utf8String name)
            {
                for (int i = 0; i < assembly.Modules.Count; i++)
                {
                    var module = assembly.Modules[i];
                    var type = FindTypeInModule(module, ns, name);
                    if (type.IsSuccess)
                        return type;
                }

                return Result.Fail<TypeDefinition>();
            }

            private Result<TypeDefinition> FindTypeInModule(ModuleDefinition module, Utf8String? ns, Utf8String name)
            {
                for (int i = 0; i < module.TopLevelTypes.Count; i++)
                {
                    var type = module.TopLevelTypes[i];
                    if (type.IsTypeOfUtf8(ns, name))
                        return Result.Success(type);
                }

                for (int i = 0; i < module.ExportedTypes.Count; i++)
                {
                    var exportedType = module.ExportedTypes[i];
                    if (exportedType.IsTypeOfUtf8(ns, name))
                        return ResolveExportedType(exportedType);
                }

                return Result.Fail<TypeDefinition>();
            }

            private static Result<TypeDefinition> FindTypeInType(TypeDefinition enclosingType, Utf8String name)
            {
                for (int i = 0; i < enclosingType.NestedTypes.Count; i++)
                {
                    var type = enclosingType.NestedTypes[i];
                    if (type.Name == name)
                        return Result.Success(type);
                }

                return Result.Fail<TypeDefinition>();
            }

            private static Result<ModuleDefinition> FindModuleInAssembly(AssemblyDefinition assembly, Utf8String name)
            {
                for (int i = 0; i < assembly.Modules.Count; i++)
                {
                    var module = assembly.Modules[i];
                    if (module.Name == name)
                        return Result.Success(module);
                }

                return Result.Fail<ModuleDefinition>();
            }
        }
    }
}
