using System;
using System.Linq;
using AsmResolver.DotNet.Signatures;
using Xunit;

namespace AsmResolver.DotNet.Tests
{
    public class MemberReferenceTest
    {
        [Fact]
        public void ResolveForwardedMethod()
        {
            // TODO: load forwarder and library into rt context.
            throw new NotImplementedException();

            var module = ModuleDefinition.FromBytes(Properties.Resources.ForwarderRefTest, TestReaderParameters);
            var forwarder = ModuleDefinition.FromBytes(Properties.Resources.ForwarderLibrary, TestReaderParameters).Assembly!;
            var library = ModuleDefinition.FromBytes(Properties.Resources.ActualLibrary, TestReaderParameters).Assembly!;

            module.RuntimeContext.AssemblyResolver.AddToCache(forwarder, forwarder);
            module.RuntimeContext.AssemblyResolver.AddToCache(library, library);

            var reference = module
                .GetImportedMemberReferences()
                .First(m => m.IsMethod && m.Name == "StaticMethod");

            _ = reference.Resolve(module.RuntimeContext).Unwrap();
        }

        [Fact]
        public void MemberReferenceUnimportedParentIsNotImported()
        {
            var module = new ModuleDefinition("Dummy");

            var freeFloatingTypeDef = new TypeDefinition(null, "TypeName", default);

            var genericType = module.CorLibTypeFactory.CorLibScope.CreateTypeReference("System", "Action`1");

            var genericInstance = genericType.MakeGenericInstanceType(false, freeFloatingTypeDef.ToTypeSignature(false));

            var member = genericInstance.ToTypeDefOrRef().CreateMemberReference("SomeMethod",
                MethodSignature.CreateStatic(module.CorLibTypeFactory.Void));

            Assert.False(member.IsImportedInModule(module));
        }
    }
}
