// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace QuickJS;

/// <summary>
/// Delegate for resolving module specifiers to file paths or URLs.
/// </summary>
/// <param name="baseModuleName">The base module name (the importing module).</param>
/// <param name="moduleName">The module specifier to resolve.</param>
/// <returns>The resolved module path, or null if resolution fails.</returns>
public delegate string? ModuleResolverDelegate(string? baseModuleName, string moduleName);

/// <summary>
/// Delegate for loading module source code.
/// </summary>
/// <param name="modulePath">The resolved module path.</param>
/// <returns>The module source code, or null if loading fails.</returns>
public delegate string? ModuleLoaderDelegate(string modulePath);

/// <summary>
/// Status of a module in the loading/evaluation lifecycle.
/// </summary>
public enum ModuleStatus
{
    /// <summary>Module is new and unlinked.</summary>
    Unlinked,
    
    /// <summary>Module is currently linking.</summary>
    Linking,
    
    /// <summary>Module has been linked.</summary>
    Linked,
    
    /// <summary>Module is currently evaluating.</summary>
    Evaluating,
    
    /// <summary>Module has been evaluated successfully.</summary>
    Evaluated,
    
    /// <summary>Module evaluation failed with an error.</summary>
    EvaluationError
}

/// <summary>
/// Represents an ES module definition.
/// </summary>
public sealed class JSModule
{
    private readonly Dictionary<string, JSValue> _exports = new Dictionary<string, JSValue>(StringComparer.Ordinal);
    private readonly Dictionary<string, JSModule> _requestedModules = new Dictionary<string, JSModule>(StringComparer.Ordinal);
    private JSObject? _namespace;
    private ModuleStatus _status = ModuleStatus.Unlinked;
    private JSValue _evaluationError = JSValue.Undefined;

    /// <summary>
    /// Gets the module specifier/name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the resolved path of the module.
    /// </summary>
    public string? ResolvedPath { get; internal set; }

    /// <summary>
    /// Gets the module source code.
    /// </summary>
    public string? SourceCode { get; internal set; }

    /// <summary>
    /// Gets the module status.
    /// </summary>
    public ModuleStatus Status
    {
        get => _status;
        internal set => _status = value;
    }

    /// <summary>
    /// Gets the evaluation error if status is EvaluationError.
    /// </summary>
    public JSValue EvaluationError
    {
        get => _evaluationError;
        internal set => _evaluationError = value;
    }

    /// <summary>
    /// Gets the module namespace object.
    /// </summary>
    public JSObject? Namespace => _namespace;

    /// <summary>
    /// Gets the module exports.
    /// </summary>
    public IReadOnlyDictionary<string, JSValue> Exports => _exports;

    /// <summary>
    /// Gets the requested (imported) modules.
    /// </summary>
    public IReadOnlyDictionary<string, JSModule> RequestedModules => _requestedModules;

    /// <summary>
    /// Creates a new module definition.
    /// </summary>
    /// <param name="name">The module name/specifier.</param>
    public JSModule(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Adds an export to the module.
    /// </summary>
    /// <param name="name">The export name.</param>
    /// <param name="value">The exported value.</param>
    public void AddExport(string name, JSValue value)
    {
        _exports[name] = value;
    }

    /// <summary>
    /// Gets an export value.
    /// </summary>
    /// <param name="name">The export name.</param>
    /// <returns>The exported value, or undefined if not found.</returns>
    public JSValue GetExport(string name)
    {
        return _exports.TryGetValue(name, out var value) ? value : JSValue.Undefined;
    }

    /// <summary>
    /// Checks if an export exists.
    /// </summary>
    /// <param name="name">The export name.</param>
    /// <returns>True if the export exists.</returns>
    public bool HasExport(string name)
    {
        return _exports.ContainsKey(name);
    }

    /// <summary>
    /// Adds a requested module dependency.
    /// </summary>
    /// <param name="specifier">The module specifier.</param>
    /// <param name="module">The resolved module.</param>
    internal void AddRequestedModule(string specifier, JSModule module)
    {
        _requestedModules[specifier] = module;
    }

    /// <summary>
    /// Creates and returns the module namespace object.
    /// </summary>
    /// <returns>The module namespace object.</returns>
    public JSObject GetOrCreateNamespace()
    {
        if (_namespace == null)
        {
            _namespace = new JSObject(null, JSClassId.ModuleNamespace);
            
            // Add all exports to the namespace
            foreach (var export in _exports)
            {
                _namespace.Set(export.Key, export.Value);
            }
            
            // Mark as module namespace (frozen, no prototype)
            _namespace.PreventExtensions();
        }
        return _namespace;
    }
}

/// <summary>
/// Manages ES module loading, resolution, and evaluation.
/// </summary>
public class JSModuleLoader
{
    private readonly JSContext _context;
    private readonly Dictionary<string, JSModule> _moduleCache = new Dictionary<string, JSModule>(StringComparer.Ordinal);
    private ModuleResolverDelegate? _resolver;
    private ModuleLoaderDelegate? _loader;
    private string? _basePath;

    /// <summary>
    /// Gets or sets the base path for module resolution.
    /// </summary>
    public string? BasePath
    {
        get => _basePath;
        set => _basePath = value;
    }

    /// <summary>
    /// Gets or sets the custom module resolver.
    /// </summary>
    public ModuleResolverDelegate? Resolver
    {
        get => _resolver;
        set => _resolver = value;
    }

    /// <summary>
    /// Gets or sets the custom module loader.
    /// </summary>
    public ModuleLoaderDelegate? Loader
    {
        get => _loader;
        set => _loader = value;
    }

    /// <summary>
    /// Gets the number of cached modules.
    /// </summary>
    public int CachedModuleCount => _moduleCache.Count;

    /// <summary>
    /// Creates a new module loader.
    /// </summary>
    /// <param name="context">The JavaScript context.</param>
    internal JSModuleLoader(JSContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Resolves a module specifier to a path.
    /// </summary>
    /// <param name="baseModuleName">The base module name (importing module).</param>
    /// <param name="moduleName">The module specifier to resolve.</param>
    /// <returns>The resolved path.</returns>
    public string ResolveModule(string? baseModuleName, string moduleName)
    {
        // Try custom resolver first
        if (_resolver != null)
        {
            var resolved = _resolver(baseModuleName, moduleName);
            if (resolved != null)
                return resolved;
        }

        // Default resolution logic
        return DefaultResolve(baseModuleName, moduleName);
    }

    private string DefaultResolve(string? baseModuleName, string moduleName)
    {
        // Handle relative imports
        if (moduleName.StartsWith("./") || moduleName.StartsWith("../"))
        {
            string? baseDir = null;
            if (!string.IsNullOrEmpty(baseModuleName))
            {
                baseDir = Path.GetDirectoryName(baseModuleName);
            }
            else if (!string.IsNullOrEmpty(_basePath))
            {
                baseDir = _basePath;
            }
            else
            {
                baseDir = Directory.GetCurrentDirectory();
            }

            var combined = Path.Combine(baseDir ?? "", moduleName);
            var normalized = Path.GetFullPath(combined);
            
            // Add .js extension if not present
            if (!Path.HasExtension(normalized))
            {
                normalized += ".js";
            }
            
            return normalized;
        }

        // Handle absolute paths
        if (Path.IsPathRooted(moduleName))
        {
            if (!Path.HasExtension(moduleName))
            {
                moduleName += ".js";
            }
            return moduleName;
        }

        // Handle bare specifiers (node_modules style)
        // For now, just use base path
        var basePath = _basePath ?? Directory.GetCurrentDirectory();
        var result = Path.Combine(basePath, moduleName);
        if (!Path.HasExtension(result))
        {
            result += ".js";
        }
        return Path.GetFullPath(result);
    }

    /// <summary>
    /// Loads module source code.
    /// </summary>
    /// <param name="modulePath">The module path.</param>
    /// <returns>The source code.</returns>
    public string? LoadModuleSource(string modulePath)
    {
        // Try custom loader first
        if (_loader != null)
        {
            var source = _loader(modulePath);
            if (source != null)
                return source;
        }

        // Default file-based loading
        if (File.Exists(modulePath))
        {
            return File.ReadAllText(modulePath, Encoding.UTF8);
        }

        return null;
    }

    /// <summary>
    /// Imports a module by specifier.
    /// </summary>
    /// <param name="specifier">The module specifier.</param>
    /// <param name="baseModule">The importing module (for relative resolution).</param>
    /// <returns>The module object.</returns>
    public JSModule ImportModule(string specifier, JSModule? baseModule = null)
    {
        var resolvedPath = ResolveModule(baseModule?.ResolvedPath, specifier);

        // Check cache
        if (_moduleCache.TryGetValue(resolvedPath, out var cached))
        {
            return cached;
        }

        // Create new module
        var module = new JSModule(specifier)
        {
            ResolvedPath = resolvedPath
        };

        // Add to cache before loading to handle circular imports
        _moduleCache[resolvedPath] = module;

        // Load source
        var source = LoadModuleSource(resolvedPath);
        if (source == null)
        {
            throw new JSException($"Cannot find module '{specifier}' at path '{resolvedPath}'");
        }

        module.SourceCode = source;

        return module;
    }

    /// <summary>
    /// Creates a synthetic module with pre-defined exports.
    /// </summary>
    /// <param name="name">The module name.</param>
    /// <param name="exports">The exports dictionary.</param>
    /// <returns>The created module.</returns>
    public JSModule CreateSyntheticModule(string name, Dictionary<string, JSValue> exports)
    {
        var module = new JSModule(name)
        {
            ResolvedPath = name,
            Status = ModuleStatus.Evaluated
        };

        foreach (var export in exports)
        {
            module.AddExport(export.Key, export.Value);
        }

        _moduleCache[name] = module;
        return module;
    }

    /// <summary>
    /// Registers a native module.
    /// </summary>
    /// <param name="name">The module name.</param>
    /// <param name="exports">The exports.</param>
    public void RegisterNativeModule(string name, Dictionary<string, JSValue> exports)
    {
        CreateSyntheticModule(name, exports);
    }

    /// <summary>
    /// Gets a cached module by path.
    /// </summary>
    /// <param name="path">The module path.</param>
    /// <returns>The module, or null if not cached.</returns>
    public JSModule? GetCachedModule(string path)
    {
        return _moduleCache.TryGetValue(path, out var module) ? module : null;
    }

    /// <summary>
    /// Clears the module cache.
    /// </summary>
    public void ClearCache()
    {
        _moduleCache.Clear();
    }

    /// <summary>
    /// Gets the namespace object for a module (for import * as name).
    /// </summary>
    /// <param name="module">The module.</param>
    /// <returns>The namespace object.</returns>
    public JSObject GetModuleNamespace(JSModule module)
    {
        return module.GetOrCreateNamespace();
    }
}
