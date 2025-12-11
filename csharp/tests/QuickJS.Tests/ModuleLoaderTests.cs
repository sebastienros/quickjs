// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for JSModule and JSModuleLoader functionality.
/// </summary>
public class ModuleLoaderTests
{
    #region JSModule Tests

    [Fact]
    public void JSModule_Constructor_SetsName()
    {
        var module = new JSModule("test-module");
        Assert.Equal("test-module", module.Name);
    }

    [Fact]
    public void JSModule_InitialStatus_IsUnlinked()
    {
        var module = new JSModule("test");
        Assert.Equal(ModuleStatus.Unlinked, module.Status);
    }

    [Fact]
    public void JSModule_AddExport_AddsExport()
    {
        var module = new JSModule("test");
        module.AddExport("foo", JSValue.FromInt32(42));

        Assert.True(module.HasExport("foo"));
        Assert.Equal(42, module.GetExport("foo").ToInt32());
    }

    [Fact]
    public void JSModule_GetExport_ReturnsUndefined_WhenNotFound()
    {
        var module = new JSModule("test");
        var result = module.GetExport("nonexistent");
        Assert.True(result.IsUndefined);
    }

    [Fact]
    public void JSModule_HasExport_ReturnsFalse_WhenNotFound()
    {
        var module = new JSModule("test");
        Assert.False(module.HasExport("nonexistent"));
    }

    [Fact]
    public void JSModule_MultipleExports()
    {
        var module = new JSModule("test");
        module.AddExport("a", JSValue.FromInt32(1));
        module.AddExport("b", JSValue.FromString("hello"));
        module.AddExport("c", JSValue.FromBoolean(true));

        Assert.Equal(3, module.Exports.Count);
        Assert.Equal(1, module.GetExport("a").ToInt32());
        Assert.True(module.GetExport("b").TryGetString(out var s) && s == "hello");
        Assert.True(module.GetExport("c").ToBoolean());
    }

    [Fact]
    public void JSModule_GetOrCreateNamespace_CreatesObject()
    {
        var module = new JSModule("test");
        module.AddExport("foo", JSValue.FromInt32(42));

        var ns = module.GetOrCreateNamespace();
        Assert.NotNull(ns);
        Assert.Equal(JSClassId.ModuleNamespace, ns.ClassId);
    }

    [Fact]
    public void JSModule_Namespace_ContainsExports()
    {
        var module = new JSModule("test");
        module.AddExport("foo", JSValue.FromInt32(42));
        module.AddExport("bar", JSValue.FromString("hello"));

        var ns = module.GetOrCreateNamespace();
        Assert.Equal(42, ns.Get("foo").ToInt32());
        Assert.True(ns.Get("bar").TryGetString(out var s) && s == "hello");
    }

    [Fact]
    public void JSModule_Namespace_IsCached()
    {
        var module = new JSModule("test");
        var ns1 = module.GetOrCreateNamespace();
        var ns2 = module.GetOrCreateNamespace();
        Assert.Same(ns1, ns2);
    }

    [Fact]
    public void JSModule_Status_CanBeSet()
    {
        var module = new JSModule("test");
        module.Status = ModuleStatus.Evaluated;
        Assert.Equal(ModuleStatus.Evaluated, module.Status);
    }

    [Fact]
    public void JSModule_EvaluationError_CanBeSet()
    {
        var module = new JSModule("test");
        module.EvaluationError = JSValue.FromString("error message");
        Assert.True(module.EvaluationError.TryGetString(out var msg) && msg == "error message");
    }

    #endregion

    #region JSModuleLoader Tests

    [Fact]
    public void JSModuleLoader_Exists_OnContext()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var loader = context.ModuleLoader;
        Assert.NotNull(loader);
    }

    [Fact]
    public void JSModuleLoader_CachedModuleCount_StartsAtZero()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        Assert.Equal(0, context.ModuleLoader.CachedModuleCount);
    }

    [Fact]
    public void JSModuleLoader_CreateSyntheticModule_CreatesModule()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var exports = new Dictionary<string, JSValue>
        {
            { "foo", JSValue.FromInt32(42) },
            { "bar", JSValue.FromString("hello") }
        };

        var module = context.ModuleLoader.CreateSyntheticModule("mymodule", exports);

        Assert.NotNull(module);
        Assert.Equal("mymodule", module.Name);
        Assert.Equal(ModuleStatus.Evaluated, module.Status);
        Assert.Equal(42, module.GetExport("foo").ToInt32());
    }

    [Fact]
    public void JSModuleLoader_CreateSyntheticModule_CachesModule()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var exports = new Dictionary<string, JSValue>
        {
            { "foo", JSValue.FromInt32(42) }
        };

        context.ModuleLoader.CreateSyntheticModule("mymodule", exports);
        
        var cached = context.ModuleLoader.GetCachedModule("mymodule");
        Assert.NotNull(cached);
        Assert.Equal("mymodule", cached.Name);
    }

    [Fact]
    public void JSModuleLoader_RegisterNativeModule_WorksLikeSynthetic()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var exports = new Dictionary<string, JSValue>
        {
            { "version", JSValue.FromString("1.0.0") }
        };

        context.ModuleLoader.RegisterNativeModule("mylib", exports);

        var cached = context.ModuleLoader.GetCachedModule("mylib");
        Assert.NotNull(cached);
        Assert.True(cached!.GetExport("version").TryGetString(out var v) && v == "1.0.0");
    }

    [Fact]
    public void JSModuleLoader_ClearCache_RemovesAllModules()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        context.ModuleLoader.CreateSyntheticModule("mod1", new Dictionary<string, JSValue>());
        context.ModuleLoader.CreateSyntheticModule("mod2", new Dictionary<string, JSValue>());
        
        Assert.Equal(2, context.ModuleLoader.CachedModuleCount);

        context.ModuleLoader.ClearCache();
        
        Assert.Equal(0, context.ModuleLoader.CachedModuleCount);
    }

    [Fact]
    public void JSModuleLoader_GetCachedModule_ReturnsNull_WhenNotCached()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var cached = context.ModuleLoader.GetCachedModule("nonexistent");
        Assert.Null(cached);
    }

    [Fact]
    public void JSModuleLoader_GetModuleNamespace_ReturnsNamespaceObject()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var exports = new Dictionary<string, JSValue>
        {
            { "foo", JSValue.FromInt32(42) }
        };

        var module = context.ModuleLoader.CreateSyntheticModule("test", exports);
        var ns = context.ModuleLoader.GetModuleNamespace(module);

        Assert.NotNull(ns);
        Assert.Equal(42, ns.Get("foo").ToInt32());
    }

    #endregion

    #region Module Resolution Tests

    [Fact]
    public void JSModuleLoader_ResolveModule_AbsolutePath_ReturnsAsIs()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var absolutePath = Path.GetFullPath("/absolute/path/module.js");
        var resolved = context.ModuleLoader.ResolveModule(null, absolutePath);
        
        Assert.Equal(absolutePath, resolved);
    }

    [Fact]
    public void JSModuleLoader_ResolveModule_AddsJsExtension()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();
        context.ModuleLoader.BasePath = "/base";

        var resolved = context.ModuleLoader.ResolveModule(null, "./test");
        
        Assert.EndsWith(".js", resolved);
    }

    [Fact]
    public void JSModuleLoader_ResolveModule_RelativePath_UsesBasePath()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();
        context.ModuleLoader.BasePath = "/project/src";

        var resolved = context.ModuleLoader.ResolveModule(null, "./utils/helper");
        
        Assert.Contains("utils", resolved);
        Assert.Contains("helper.js", resolved);
    }

    [Fact]
    public void JSModuleLoader_ResolveModule_RelativePath_UsesBaseModule()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var resolved = context.ModuleLoader.ResolveModule("/project/src/main.js", "./utils/helper");
        
        Assert.Contains("utils", resolved);
        Assert.Contains("helper.js", resolved);
    }

    [Fact]
    public void JSModuleLoader_CustomResolver_IsUsed()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        context.ModuleLoader.Resolver = (baseModule, moduleName) =>
        {
            if (moduleName == "custom")
                return "/resolved/custom.js";
            return null;
        };

        var resolved = context.ModuleLoader.ResolveModule(null, "custom");
        Assert.Equal("/resolved/custom.js", resolved);
    }

    [Fact]
    public void JSModuleLoader_CustomResolver_FallsBackToDefault()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();
        context.ModuleLoader.BasePath = "/base";

        context.ModuleLoader.Resolver = (baseModule, moduleName) =>
        {
            // Return null to fall back to default
            return null;
        };

        var resolved = context.ModuleLoader.ResolveModule(null, "./test");
        
        // Should use default resolution
        Assert.Contains("test.js", resolved);
    }

    #endregion

    #region Module Loading Tests

    [Fact]
    public void JSModuleLoader_CustomLoader_IsUsed()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        context.ModuleLoader.Loader = (path) =>
        {
            if (path == "/test/module.js")
                return "export const x = 42;";
            return null;
        };

        var source = context.ModuleLoader.LoadModuleSource("/test/module.js");
        Assert.Equal("export const x = 42;", source);
    }

    [Fact]
    public void JSModuleLoader_LoadModuleSource_ReturnsNull_WhenNotFound()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var source = context.ModuleLoader.LoadModuleSource("/nonexistent/path/module.js");
        Assert.Null(source);
    }

    [Fact]
    public void JSModuleLoader_ImportModule_WithCustomLoader()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        // Custom resolver that returns a fixed path
        context.ModuleLoader.Resolver = (baseModule, moduleName) =>
        {
            return "/virtual/" + moduleName + ".js";
        };

        // Custom loader that provides source
        context.ModuleLoader.Loader = (path) =>
        {
            if (path == "/virtual/mymodule.js")
                return "export const value = 42;";
            return null;
        };

        var module = context.ModuleLoader.ImportModule("mymodule");

        Assert.NotNull(module);
        Assert.Equal("mymodule", module.Name);
        Assert.Equal("/virtual/mymodule.js", module.ResolvedPath);
        Assert.Equal("export const value = 42;", module.SourceCode);
    }

    [Fact]
    public void JSModuleLoader_ImportModule_CachesModule()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        context.ModuleLoader.Resolver = (b, m) => "/virtual/" + m + ".js";
        context.ModuleLoader.Loader = (p) => "export const x = 1;";

        var module1 = context.ModuleLoader.ImportModule("cached");
        var module2 = context.ModuleLoader.ImportModule("cached");

        Assert.Same(module1, module2);
    }

    [Fact]
    public void JSModuleLoader_ImportModule_Throws_WhenNotFound()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        // No custom loader, and the file doesn't exist
        Assert.Throws<JSException>(() =>
            context.ModuleLoader.ImportModule("nonexistent-module"));
    }

    #endregion

    #region BasePath Tests

    [Fact]
    public void JSModuleLoader_BasePath_CanBeSet()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        context.ModuleLoader.BasePath = "/my/project";
        Assert.Equal("/my/project", context.ModuleLoader.BasePath);
    }

    [Fact]
    public void JSModuleLoader_BasePath_DefaultsToNull()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        Assert.Null(context.ModuleLoader.BasePath);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void ModuleLoader_CompleteWorkflow()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        // Register a native utility module
        var mathExports = new Dictionary<string, JSValue>
        {
            { "PI", JSValue.FromDouble(3.14159) },
            { "E", JSValue.FromDouble(2.71828) }
        };
        context.ModuleLoader.RegisterNativeModule("math-constants", mathExports);

        // Get the module
        var mathModule = context.ModuleLoader.GetCachedModule("math-constants");
        Assert.NotNull(mathModule);

        // Get namespace for import * style
        var ns = context.ModuleLoader.GetModuleNamespace(mathModule!);
        
        // Access exports through namespace
        var pi = ns.Get("PI").ToDouble();
        Assert.True(pi > 3.14 && pi < 3.15);
    }

    [Fact]
    public void ModuleLoader_VirtualModuleSystem()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        // Set up a virtual file system
        var virtualFiles = new Dictionary<string, string>
        {
            { "/app/main.js", "import { helper } from './utils.js';" },
            { "/app/utils.js", "export function helper() { return 42; }" }
        };

        context.ModuleLoader.Resolver = (baseModule, moduleName) =>
        {
            // Handle absolute paths
            if (moduleName.StartsWith("/"))
            {
                return moduleName;
            }
            // Handle relative paths
            if (moduleName.StartsWith("./"))
            {
                // Use string manipulation instead of Path.GetDirectoryName to avoid platform-specific path separators
                var basePath = baseModule != null ? baseModule.Substring(0, baseModule.LastIndexOf('/')) : "/app";
                // Combine the base path with the relative module name (remove ./)
                return basePath + "/" + moduleName.Substring(2);
            }
            return "/app/" + moduleName;
        };

        context.ModuleLoader.Loader = (path) =>
        {
            return virtualFiles.TryGetValue(path, out var source) ? source : null;
        };

        var mainModule = context.ModuleLoader.ImportModule("/app/main.js");
        var utilsModule = context.ModuleLoader.ImportModule("./utils.js", mainModule);

        Assert.NotNull(mainModule);
        Assert.NotNull(utilsModule);
        Assert.Equal(2, context.ModuleLoader.CachedModuleCount);
    }

    #endregion

    #region ModuleStatus Tests

    [Fact]
    public void ModuleStatus_AllValuesExist()
    {
        Assert.Equal(ModuleStatus.Unlinked, (ModuleStatus)0);
        Assert.Equal(ModuleStatus.Linking, (ModuleStatus)1);
        Assert.Equal(ModuleStatus.Linked, (ModuleStatus)2);
        Assert.Equal(ModuleStatus.Evaluating, (ModuleStatus)3);
        Assert.Equal(ModuleStatus.Evaluated, (ModuleStatus)4);
        Assert.Equal(ModuleStatus.EvaluationError, (ModuleStatus)5);
    }

    #endregion
}
