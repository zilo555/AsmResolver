using System.Linq;
using AsmResolver.DotNet.Serialized;
using AsmResolver.DotNet.TestCases.Methods;
using AsmResolver.DotNet.TestCases.Types;
using AsmResolver.IO;
using Xunit;

namespace AsmResolver.DotNet.Tests
{
    public class RuntimeContextTest
    {
        [Fact]
        public void LoadAssemblyShouldCreateContextWithAssembly()
        {
            var assembly = AssemblyDefinition.FromBytes(Properties.Resources.HelloWorld, TestReaderParameters);

            Assert.NotNull(assembly.RuntimeContext);
            Assert.Contains(assembly, assembly.RuntimeContext.GetLoadedAssemblies());
        }

        [Fact]
        public void ResolveDependencyShouldUseSameRuntimeContext()
        {
            var main = AssemblyDefinition.FromBytes(Properties.Resources.HelloWorld, TestReaderParameters);
            var dependency = main.ManifestModule!.CorLibTypeFactory.CorLibScope.GetAssembly()!.Resolve(main.RuntimeContext);

            Assert.Same(main.RuntimeContext, dependency.RuntimeContext);

            var loadedAssemblies = main.RuntimeContext!.GetLoadedAssemblies().ToArray();
            Assert.Contains(main, loadedAssemblies);
            Assert.Contains(dependency, loadedAssemblies);
        }

        [Fact]
        public void ResolveDependencyShouldUseSameFileService()
        {
            var service = new ByteArrayFileService();
            service.OpenBytesAsFile("HelloWorld.dll", Properties.Resources.HelloWorld);

            var main = ModuleDefinition.FromFile("HelloWorld.dll", new ModuleReaderParameters(service));
            var dependency = main.CorLibTypeFactory.CorLibScope.GetAssembly()!.Resolve(main.RuntimeContext).ManifestModule!;

            Assert.Contains(main.FilePath, service.GetOpenedFiles());
            Assert.Contains(dependency.FilePath, service.GetOpenedFiles());
        }

        [Fact]
        public void DetectNetFrameworkContext()
        {
            var module = ModuleDefinition.FromBytes(Properties.Resources.HelloWorld, TestReaderParameters);
            Assert.Equal(
                DotNetRuntimeInfo.NetFramework(4, 0),
                module.RuntimeContext.TargetRuntime
            );
        }

        [Fact]
        public void DetectNetCoreAppContext()
        {
            var module = ModuleDefinition.FromBytes(Properties.Resources.HelloWorld_NetCore, TestReaderParameters);
            Assert.Equal(
                DotNetRuntimeInfo.NetCoreApp(2, 2),
                module.RuntimeContext.TargetRuntime
            );
        }

        [Fact]
        public void ForceNetFXLoadAsNetCore()
        {
            var context = new RuntimeContext(DotNetRuntimeInfo.NetCoreApp(3, 1));
            var module = ModuleDefinition.FromBytes(Properties.Resources.HelloWorld, new ModuleReaderParameters(context));

            Assert.Equal(context.TargetRuntime, module.RuntimeContext.TargetRuntime);
            Assert.IsAssignableFrom<DotNetCoreAssemblyResolver>(module.RuntimeContext.AssemblyResolver);
        }

        [Fact]
        public void ForceNetStandardLoadAsNetFx()
        {
            var context = new RuntimeContext(DotNetRuntimeInfo.NetFramework(4, 8));
            var module = ModuleDefinition.FromFile(typeof(Class).Assembly.Location, new ModuleReaderParameters(context));

            Assert.Equal(context.TargetRuntime, module.RuntimeContext.TargetRuntime);
            Assert.Equal("mscorlib", module.CorLibTypeFactory.Object.Resolve(module.RuntimeContext).DeclaringModule?.Assembly?.Name);
        }

        [Fact]
        public void ForceNetStandardLoadAsNetCore()
        {
            var context = new RuntimeContext(DotNetRuntimeInfo.NetCoreApp(8, 0));
            var module = ModuleDefinition.FromFile(typeof(Class).Assembly.Location, new ModuleReaderParameters(context));

            Assert.Equal(context.TargetRuntime, module.RuntimeContext.TargetRuntime);
            Assert.Equal("System.Private.CoreLib", module.CorLibTypeFactory.Object.Resolve(module.RuntimeContext).DeclaringModule?.Assembly?.Name);
        }

        [Fact]
        public void ResolveSameDependencyInSameContextShouldResultInSameAssembly()
        {
            var module1 = ModuleDefinition.FromFile(typeof(Class).Assembly.Location, TestReaderParameters);
            var context = module1.RuntimeContext;
            var module2 = context.LoadAssembly(typeof(SingleMethod).Assembly.Location).ManifestModule!;

            var object1 = module1.CorLibTypeFactory.Object.Resolve(context);
            var object2 = module2.CorLibTypeFactory.Object.Resolve(module2.RuntimeContext);

            Assert.Same(object1, object2);
        }
    }
}
