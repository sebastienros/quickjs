# Module Loader

This document describes the implementation of ES module loading in the QuickJS C# port.

## Overview

The module loader provides support for ES modules including:
- **Module Resolution** - Resolving module specifiers to file paths
- **Module Loading** - Loading module source code from files or custom sources
- **Module Caching** - Caching loaded modules to avoid duplicate work
- **Synthetic Modules** - Creating modules programmatically from C#
- **Native Modules** - Registering native C# functionality as modules

## Architecture

### JSModule Class

Represents an ES module definition:

```
JSModule
├── Name (string) - Module specifier
├── ResolvedPath (string?) - Resolved file path
├── SourceCode (string?) - Module source
├── Status (ModuleStatus) - Current lifecycle status
├── Exports (Dictionary<string, JSValue>) - Exported values
├── RequestedModules (Dictionary<string, JSModule>) - Dependencies
└── Namespace (JSObject?) - Module namespace object
```

### JSModuleLoader Class

Manages module loading and resolution:

```
JSModuleLoader
├── _context - Owner context
├── _moduleCache - Cached modules by path
├── _resolver - Custom resolver delegate
├── _loader - Custom loader delegate
└── BasePath - Default base path for resolution
```

## Module Status

The `ModuleStatus` enum tracks the module lifecycle:

| Status | Description |
|--------|-------------|
| `Unlinked` | Module is new, not yet linked |
| `Linking` | Module is currently linking |
| `Linked` | Module has been linked |
| `Evaluating` | Module is currently evaluating |
| `Evaluated` | Module has been evaluated successfully |
| `EvaluationError` | Module evaluation failed |

## Using the Module Loader

### Accessing the Loader

```csharp
using var runtime = new JSRuntime();
using var context = runtime.CreateContext();

var moduleLoader = context.ModuleLoader;
```

### Setting the Base Path

```csharp
// Set base path for relative module resolution
context.ModuleLoader.BasePath = "/path/to/project/src";
```

### Creating Synthetic Modules

Create modules programmatically from C# code:

```csharp
var exports = new Dictionary<string, JSValue>
{
    { "PI", JSValue.FromDouble(3.14159) },
    { "E", JSValue.FromDouble(2.71828) },
    { "add", JSValue.FromObject(addFunction) }
};

var mathModule = context.ModuleLoader.CreateSyntheticModule("math-utils", exports);
```

### Registering Native Modules

Register C# functionality as importable modules:

```csharp
var exports = new Dictionary<string, JSValue>
{
    { "readFile", JSValue.FromObject(readFileFunction) },
    { "writeFile", JSValue.FromObject(writeFileFunction) }
};

context.ModuleLoader.RegisterNativeModule("fs", exports);

// Now JavaScript can import from "fs":
// import { readFile, writeFile } from 'fs';
```

### Custom Module Resolution

Override how module specifiers are resolved:

```csharp
context.ModuleLoader.Resolver = (baseModule, moduleName) =>
{
    // Handle node_modules style imports
    if (!moduleName.StartsWith(".") && !moduleName.StartsWith("/"))
    {
        return $"/node_modules/{moduleName}/index.js";
    }
    
    // Fall back to default resolution
    return null;
};
```

### Custom Module Loading

Override how module source code is loaded:

```csharp
context.ModuleLoader.Loader = (modulePath) =>
{
    // Load from database, remote URL, etc.
    if (TryGetFromDatabase(modulePath, out var source))
    {
        return source;
    }
    
    // Fall back to default file-based loading
    return null;
};
```

### Importing Modules

Import a module by specifier:

```csharp
// Import absolute path
var mainModule = context.ModuleLoader.ImportModule("/app/main.js");

// Import relative to another module
var utilsModule = context.ModuleLoader.ImportModule("./utils.js", mainModule);
```

### Getting the Module Namespace

Get the namespace object for `import * as name` style imports:

```csharp
var module = context.ModuleLoader.ImportModule("mymodule");
var namespace = context.ModuleLoader.GetModuleNamespace(module);

// Access exports via namespace
var value = namespace.Get("exportedValue");
```

## Module Resolution

### Resolution Order

1. Try custom resolver if set
2. If custom resolver returns null, use default resolution
3. Default resolution handles:
   - Relative paths (`./`, `../`)
   - Absolute paths (`/path/to/module`)
   - Bare specifiers (relative to base path)

### Path Normalization

- `.js` extension added automatically if missing
- Paths are normalized using `Path.GetFullPath`
- Relative paths resolved against base path or current directory

### Example Resolution

```csharp
// Given BasePath = "/project/src"

// Relative import
ResolveModule(null, "./utils")
// → /project/src/utils.js

// Relative import from another module
ResolveModule("/project/src/main.js", "./lib/helper")
// → /project/src/lib/helper.js

// Parent directory
ResolveModule("/project/src/views/home.js", "../models/user")
// → /project/src/models/user.js

// Bare specifier
ResolveModule(null, "lodash")
// → /project/src/lodash.js (or custom resolution)
```

## Module Caching

### How Caching Works

- Modules are cached by their resolved path
- Same module specifier from different locations may resolve to different cached modules
- Synthetic/native modules are cached by their module name

### Cache Management

```csharp
// Get number of cached modules
int count = context.ModuleLoader.CachedModuleCount;

// Get cached module by path
var module = context.ModuleLoader.GetCachedModule("/path/to/module.js");

// Clear all cached modules
context.ModuleLoader.ClearCache();
```

### Circular Import Handling

Modules are added to cache before loading source, which allows circular imports to resolve without infinite recursion. However, circular imports may see partially initialized modules.

## Working with Exports

### Adding Exports to a Module

```csharp
var module = new JSModule("mymodule");
module.AddExport("default", JSValue.FromObject(mainClass));
module.AddExport("helper", JSValue.FromObject(helperFunction));
module.AddExport("VERSION", JSValue.FromString("1.0.0"));
```

### Checking Exports

```csharp
if (module.HasExport("default"))
{
    var defaultExport = module.GetExport("default");
}

// Get all exports
foreach (var (name, value) in module.Exports)
{
    Console.WriteLine($"Export: {name}");
}
```

### Module Namespace Object

The namespace object provides read-only access to all exports:

```csharp
var ns = module.GetOrCreateNamespace();

// Namespace is frozen (no modifications allowed)
// ns.ClassId == JSClassId.ModuleNamespace

// Access exports
var value = ns.Get("exportName");
```

## Virtual File System Example

Create a complete virtual module system:

```csharp
using var runtime = new JSRuntime();
using var context = runtime.CreateContext();

// Virtual file system
var files = new Dictionary<string, string>
{
    { "/app/main.js", "import { greet } from './greet.js'; greet('World');" },
    { "/app/greet.js", "export function greet(name) { console.log('Hello, ' + name); }" }
};

// Custom resolver
context.ModuleLoader.Resolver = (baseModule, moduleName) =>
{
    if (moduleName.StartsWith("/"))
        return moduleName;
    if (moduleName.StartsWith("./"))
    {
        var basePath = baseModule != null 
            ? Path.GetDirectoryName(baseModule) 
            : "/app";
        return basePath + "/" + moduleName.Substring(2);
    }
    return "/app/" + moduleName;
};

// Custom loader
context.ModuleLoader.Loader = (path) =>
    files.TryGetValue(path, out var source) ? source : null;

// Import and use modules
var mainModule = context.ModuleLoader.ImportModule("/app/main.js");
var greetModule = context.ModuleLoader.ImportModule("./greet.js", mainModule);
```

## Integration with JSContext

### Module Loader Access

```csharp
// ModuleLoader is lazily created on first access
var loader = context.ModuleLoader;

// Same instance on subsequent accesses
var sameLoader = context.ModuleLoader;
Assert.Same(loader, sameLoader);
```

### Legacy LoadedModuleCount

The `LoadedModuleCount` property on JSContext reflects the legacy module dictionary (for backward compatibility). Use `ModuleLoader.CachedModuleCount` for the new module system.

## Best Practices

### Module Organization

```csharp
// Register core modules at startup
context.ModuleLoader.RegisterNativeModule("console", consoleExports);
context.ModuleLoader.RegisterNativeModule("fs", fsExports);
context.ModuleLoader.RegisterNativeModule("http", httpExports);

// Set base path for user modules
context.ModuleLoader.BasePath = userModulesPath;
```

### Error Handling

```csharp
try
{
    var module = context.ModuleLoader.ImportModule("unknown-module");
}
catch (JSException ex)
{
    Console.WriteLine($"Module load failed: {ex.Message}");
    // Module not found or load error
}
```

### Module Lifecycle

```csharp
// After import, check status
if (module.Status == ModuleStatus.Evaluated)
{
    // Module is ready to use
    var exports = module.Exports;
}
else if (module.Status == ModuleStatus.EvaluationError)
{
    // Handle evaluation error
    var error = module.EvaluationError;
}
```

## Limitations

### Current Limitations

- Module parsing/evaluation requires integration with parser (source stored but not parsed)
- Dynamic `import()` not yet implemented
- No `import.meta` support
- No re-export syntax support

### Future Enhancements

- Full module parsing integration
- Dynamic import support
- Import map support
- Source map support
- Module preloading

## Test Coverage

The module loader implementation includes comprehensive tests for:

- JSModule creation and export management
- Module namespace object creation
- Module loader resolution and loading
- Custom resolver and loader delegates
- Module caching behavior
- Synthetic and native module registration
- Virtual file system integration
- All ModuleStatus values

## Related Files

- `JSModuleLoader.cs` - Module loader and JSModule implementation
- `JSClassId.cs` - ModuleNamespace class ID
- `JSContext.cs` - ModuleLoader property
- `ModuleLoaderTests.cs` - Unit tests
