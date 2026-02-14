# Basic I/O

Every .NET binary interaction is done through classes defined by the `AsmResolver.DotNet` namespace:

``` csharp
using AsmResolver.DotNet;
```

## Assemblies

The root of a .NET application is an assembly definition, represented by `AssemblyDefinition`.

### Creating a new .NET assembly

To create a new assembly definition, use its constructor:

``` csharp
var assembly = new AssemblyDefinition("MyAssembly", new Version(1, 0, 0, 0));
```

Modules can be added to an assembly:

```csharp
var module = new ModuleDefinition("MyAssembly.dll");
assembly.Modules.Add(module);
```

The first module in `Modules` is considered the main manifest module.
The following two statements are equivalent:

```csharp
var manifestModule = assembly.ManifestModule;
```
```csharp
var manifestModule = assembly.Modules[0];
```


### Opening a .NET assembly

Opening existing .NET assemblies can be done using one of the `FromXXX` methods:

``` csharp
byte[] raw = ...
var assembly = AssemblyDefinition.FromBytes(raw);
```

``` csharp
var assembly = AssemblyDefinition.FromFile(@"C:\myfile.exe");
```

``` csharp
PEFile peFile = ...
var assembly = AssemblyDefinition.FromFile(peFile);
```

``` csharp
BinaryStreamReader reader = ...
var assembly = AssemblyDefinition.FromReader(reader);
```

``` csharp
PEImage peImage = ...
var assembly = AssemblyDefinition.FromImage(peImage);
```

If you want to read large files (+100MB), consider using memory mapped I/O instead for better performance and memory usage:

``` csharp
using var service = new MemoryMappedFileService();
var assembly = AssemblyDefinition.FromFile(service.OpenFile(@"C:\myfile.exe"));
```

> [!NOTE]
> Each call to any of the `FromXXX` methods will result in a new `RuntimeContext` by default (see below).

For more information on customizing the reading process, see [Advanced Module Reading](advanced-module-reading.md).


### Writing a .NET assembly

Writing a .NET assembly can be done through one of the `Write` method overloads.

``` csharp
assembly.Write(@"C:\myfile.exe");
```

> [!WARNING]
> Note that for multi-module assemblies, this may overwrite other files in the same directory matching the names of the sub-modules.

Individual modules can also be rebuild.

``` csharp
assembly.WriteManifest(@"C:\myfile.exe");
```
``` csharp
assembly.Modules[0].Write(@"C:\myfile.exe");
```

For more advanced options to write .NET assemblies, see 
[Advanced PE Image Building](advanced-pe-image-building.md).



## Modules

Modules are single compilation units within a single assembly.

### Creating a new .NET module

Creating a new module can be done by instantiating a `ModuleDefinition` class:

``` csharp
var module = new ModuleDefinition("MyModule.exe");
```

By default, the new module will target .NET Framework 4.x.
If another version is needed, use one of the overloads of the constructor.

``` csharp
var module = new ModuleDefinition("MyModule.dll", DotNetRuntimeInfo.NetFramework(2, 0));
```
``` csharp
var module = new ModuleDefinition("MyModule.dll", DotNetRuntimeInfo.Parse(".NETCoreApp,Version=v3.1"));
```
``` csharp
RuntimeContext context = ...;
var module = new ModuleDefinition("MyModule.dll", context.TargetRuntime);
```
``` csharp
var module = new ModuleDefinition("MyModule.dll", KnownCorLibs.SystemRuntime_v4_2_2_0);
```
``` csharp
AssemblyReference customCorLib = ...;
var module = new ModuleDefinition("MyModule.dll", customCorLib);
```
``` csharp
var module = new ModuleDefinition("MyModule.dll", null); // Create a new corlib module.
```


## Opening a .NET module

Opening an existing .NET module can be done, either by opening an `AssemblyDefinition` and accessing its `ManifestModule` or `Modules` property.
Alternatively, individual modules can be opened using the `FromXXX` methods
from the `ModuleDefinition` class:

``` csharp
byte[] raw = ...
var module = ModuleDefinition.FromBytes(raw);
```

``` csharp
var module = ModuleDefinition.FromFile(@"C:\myfile.exe");
```

``` csharp
PEFile peFile = ...
var module = ModuleDefinition.FromFile(peFile);
```

``` csharp
BinaryStreamReader reader = ...
var module = ModuleDefinition.FromReader(reader);
```

``` csharp
PEImage peImage = ...
var module = ModuleDefinition.FromImage(peImage);
```

If you want to read large files (+100MB), consider using memory mapped I/O instead for better performance and memory usage:

``` csharp
using var service = new MemoryMappedFileService();
var module = ModuleDefinition.FromFile(service.OpenFile(@"C:\myfile.exe"));
```

On Windows, if a module is loaded and mapped in memory (e.g. as a dependency defined in Metadata or by the means of `System.Reflection`), it is possible to load the module from memory by using `FromModule`, or by transforming the module into a `HINSTANCE` and then providing it to the `FromModuleBaseAddress` method:

``` csharp
Module module = ...;
var module = ModuleDefinition.FromModule(module);
```

``` csharp
Module module = ...;
IntPtr hInstance = Marshal.GetHINSTANCE(module);
var module = ModuleDefinition.FromModuleBaseAddress(hInstance);
```

For more information on customizing the reading process, see [Advanced Module Reading](advanced-module-reading.md).


## Writing a .NET module

Writing a .NET module can be done through one of the `Write` method
overloads.

``` csharp
module.Write(@"C:\myfile.patched.exe");
```

``` csharp
Stream stream = ...;
module.Write(stream);
```

For more advanced options to write .NET modules, see 
[Advanced PE Image Building](advanced-pe-image-building.md).



## Runtime Contexts

.NET assemblies rarely are fully self-contained and reference code in DLLs that are either stored in the same directory, or in one of the runtime installation directories on the system.

AsmResolver mimics the lifetime of a .NET process using `RuntimeContext`s.
A `RuntimeContext` implements the same assembly resolution and management logic as seen at runtime, and maintains metadata caches for fast lookup and traversal of external references.


### Creating Runtime Contexts

By default, when opening an existing assembly or module, AsmResolver automatically creates a new runtime context that is tuned to the original target runtime of the input file:

```csharp
var assembly = AssemblyDefinition.FromFile(@"C:\Path\To\File.exe");
var context = assembly.RuntimeContext; // Automatically detected and configured.
```

You can also explicitly create a new (empty) runtime context, targeting a specific runtime:

```csharp
// Create empty .NET Core 3.1 context.
var context = new RuntimeContext(DotNetRuntimeInfo.NetCoreApp(3, 1));
```
```csharp
// Create based on the contents of a runtime config JSON file.
var config = RuntimeConfiguration.FromFile(@"C:\Path\To\File.runtimeconfig.json");
var context = new RuntimeContext(config);
```
```csharp
// Create based on the contents of a .NET PE image.
PEImage baseImage = ...
var context = new RuntimeContext(baseImage);
```
```csharp
// Create based based on the contents of a single-file bundle.
BundleManifest bundle = ...
var context = new RuntimeContext(bundle);
```

A `RuntimeContext` can also be configured with a custom assembly resolver:
```csharp
IAssemblyResolver resolver = ...;
var context = new RuntimeContext(
    targetRuntime: DotNetRuntimeInfo.NetCoreApp(3, 1), 
    assemblyResolver: resolver
);
```

### Managing Assemblies

Assemblies can be loaded directly into the context:

```csharp
AssemblyDefinition assembly = context.LoadAssembly(@"C:\Path\To\File.exe");
```

When an assembly is not added to a context yet (e.g., new assemblies or manually read assemblies using `FromXXX` with `createReaderContext: false`), they can be added manually:
```csharp
var assembly = new AssemblyDefinition("Foo", new Version(1, 0, 0, 0));
context.AddAssembly(assembly);
```

Multiple assemblies can be loaded in the same context:

```csharp
// Load other assemblies  within the context.
var dependency1 = context.LoadAssembly(@"C:\Path\To\Dependency1.dll");
var dependency2 = context.LoadAssembly(@"C:\Path\To\Dependency2.dll");
var dependency3 = context.LoadAssembly(@"C:\Path\To\Dependency3.dll");
...
```

Loading an assembly with the same name as a previously loaded assembly will result in the same assembly definition instance:

```csharp
var assembly = context.LoadAssembly(@"C:\Path\To\Dependency1.dll");
var assembly2 = context.LoadAssembly(@"C:\Path\To\Dependency1.dll"); // returns same instance as `assembly`.
```

All currently loaded assemblies can be enumerated:
```csharp
foreach (var assembly in context.GetLoadedAssemblies())
    Console.WriteLine(assembly.FullName);
```
