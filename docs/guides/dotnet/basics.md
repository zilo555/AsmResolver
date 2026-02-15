# Basic I/O

Every .NET binary interaction is done through classes defined by the `AsmResolver.DotNet` namespace:

``` csharp
using AsmResolver.DotNet;
```

## Assemblies

The root of a .NET application is an assembly, represented by `AssemblyDefinition`.

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

The first module in `Modules` is considered the main manifest module (and in most cases this is the only module that is defined in an assembly).
The following two statements are equivalent:

```csharp
var manifestModule = assembly.ManifestModule;
```
```csharp
var manifestModule = assembly.Modules[0];
```

> [!NOTE]
> Creating a new assembly definition does not automatically add a manifest module.

> [!NOTE]
> Creating a new assembly definition will not automatically add it to a `RuntimeContext`.
> See [Managing Assemblies](runtime-contexts.md#managing-assemblies) for adding the assembly to a runtime context.


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
> Each call to any of the `FromXXX` methods will result in a new `RuntimeContext` unless explicitly specified otherwise.
> For example:
> ```csharp
> var assembly = AssemblyDefinition.FromBytes(raw, createRuntimeContext: false);
> ```
> See also [Runtime Contexts](runtime-contexts.md).

For more information on customizing the reading process, see [Advanced Module Reading](advanced-module-reading.md).


### Writing a .NET assembly

Writing a .NET assembly can be done through one of the `Write` method overloads.

``` csharp
assembly.Write(@"C:\myfile.exe");
```

Note that for multi-module assemblies, this may implicitly overwrite other files in the same directory matching the names of sub-modules.
For these cases, consider writing into a separate directory, or write the individual modules instead:

``` csharp
assembly.WriteManifest(@"C:\myfile.exe");
```
``` csharp
Stream stream = ...;
assembly.WriteManifest(stream);
```
``` csharp
assembly.Modules[1].Write(@"C:\myfile.exe");
```

For more advanced options to write .NET assemblies, see [Advanced PE Image Building](advanced-pe-image-building.md).


## Modules

Modules are single compilation units within a single assembly, represented using the `ModuleDefinition` class.

### Creating a new .NET module

Creating a new `ModuleDefinition` can be done by using its constructor:

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

> [!NOTE]
> Creating a new module definition does not automatically add it to an assembly definition, and thus will also not be automatically added to a  `RuntimeContext`.
> See [Managing Assemblies](runtime-contexts.md#managing-assemblies) for adding the assembly to a runtime context.


## Opening a .NET module

Opening an existing .NET module can be done in two ways.
1. By opening an `AssemblyDefinition` and accessing its `ManifestModule` or `Modules` property.
2. By explicitly opening individual modules using the `FromXXX` methods from the `ModuleDefinition` class.

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

> [!NOTE]
> Each call to any of the `FromXXX` methods will result in a new `RuntimeContext` if the module contains an assembly manifest.
> This can be overridden by explicitly specifying not to create a runtime context.
> For example:
> ```csharp
> var module = ModuleDefinition.FromBytes(raw, createRuntimeContext: false);
> ```
> See also [Runtime Contexts](runtime-contexts.md).

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
