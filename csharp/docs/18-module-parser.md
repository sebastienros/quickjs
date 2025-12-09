# Step 4.6: Module Parser

## Overview

This step implements ES Module import and export syntax parsing. JavaScript modules allow code organization through explicit imports and exports, enabling:

- **Code organization**: Split code across files
- **Explicit dependencies**: Clear import/export declarations
- **Static analysis**: Module structure known at parse time
- **Namespace isolation**: Each module has its own scope

## QuickJS C Implementation

QuickJS handles modules in `quickjs.c`:

```c
static __exception int js_parse_import(JSParseState *s)
{
    JSAtom module_name;
    
    if (s->token.val == TOK_STRING) {
        // import 'module' - side effect import
        module_name = ...;
    } else {
        // Parse import bindings
        if (s->token.val == TOK_IDENT) {
            // import x from 'module'
        }
        if (s->token.val == '{') {
            // import { a, b as c } from 'module'
        }
        if (s->token.val == '*') {
            // import * as ns from 'module'
        }
        // Expect 'from' keyword
        js_parse_expect(s, TOK_FROM);
    }
}

static __exception int js_parse_export(JSParseState *s)
{
    if (s->token.val == TOK_DEFAULT) {
        // export default ...
    } else if (s->token.val == '{') {
        // export { a, b as c }
    } else if (s->token.val == '*') {
        // export * from 'module'
    } else {
        // export function/class/var/let/const
    }
}
```

## C# Implementation

### Module Mode Flag

```csharp
public class Parser
{
    public bool IsModule { get; }
    public string? ModuleName { get; }
    
    public Parser(string source, string fileName, AtomTable atoms, 
                  bool isModule = false, string? moduleName = null)
    {
        IsModule = isModule;
        ModuleName = moduleName;
    }
}
```

### Module Tracking in JSFunctionDef

```csharp
public class JSFunctionDef
{
    // Module properties
    public bool IsModule { get; set; }
    public List<JSAtom> ModuleRequests { get; } = new();
    public List<(JSAtom LocalName, JSAtom ImportName, int ModuleIndex)> ImportBindings { get; } = new();
    public List<(JSAtom LocalName, JSAtom ExportName)> ExportEntries { get; } = new();
    
    public int AddModuleRequest(JSAtom moduleName)
    {
        int index = ModuleRequests.IndexOf(moduleName);
        if (index < 0)
        {
            index = ModuleRequests.Count;
            ModuleRequests.Add(moduleName);
        }
        return index;
    }
}
```

### Import Declaration Parsing

```csharp
private void ParseImportDeclaration()
{
    if (!IsModule)
        throw new JSSyntaxError("import is only valid in module code", ...);
    
    NextToken(); // consume 'import'
    
    // Dynamic import: import('module')
    if (Check(TokenType.LeftParen))
    {
        ParseDynamicImport();
        return;
    }
    
    // Side-effect import: import 'module'
    if (Check(TokenType.String))
    {
        var moduleName = _atoms.GetAtom(_current.StringValue!);
        _currentFunction.AddModuleRequest(moduleName);
        NextToken();
        ExpectSemicolon();
        return;
    }
    
    // Named imports with bindings
    ParseImportBindings();
}
```

### Export Declaration Parsing

```csharp
private void ParseExportDeclaration()
{
    if (!IsModule)
        throw new JSSyntaxError("export is only valid in module code", ...);
    
    NextToken(); // consume 'export'
    
    if (Check(TokenType.Default))
    {
        ParseExportDefault();
    }
    else if (Check(TokenType.LeftBrace))
    {
        ParseExportNamedList();
    }
    else if (Check(TokenType.Star))
    {
        ParseExportAll();
    }
    else
    {
        ParseExportDeclaration(); // function/class/var/let/const
    }
}
```

## Import Syntax Forms

### Side-Effect Import

```javascript
import 'module';
import './styles.css';
```

### Default Import

```javascript
import fs from 'fs';
import React from 'react';
```

### Named Imports

```javascript
import { foo, bar } from 'module';
import { foo as myFoo } from 'module';
```

### Namespace Import

```javascript
import * as utils from './utils';
```

### Mixed Imports

```javascript
import React, { useState, useEffect } from 'react';
import fs, * as fsExtra from 'fs';
```

### Dynamic Import

```javascript
const module = await import('./dynamic.js');
```

## Export Syntax Forms

### Named Exports

```javascript
export { foo, bar };
export { foo as myFoo };
```

### Declaration Exports

```javascript
export function foo() {}
export class MyClass {}
export const x = 1;
export let y = 2;
export var z = 3;
```

### Default Export

```javascript
export default function() {}
export default class {}
export default expression;
```

### Re-exports

```javascript
export { foo, bar } from 'module';
export * from 'module';
export * as utils from 'module';
export { default } from 'module';
export { default as foo } from 'module';
```

## Contextual Keywords

The keywords `from` and `as` are contextual - they act as keywords only in import/export contexts but can be used as identifiers elsewhere:

```csharp
private bool CheckContextualKeyword(string keyword)
{
    return _current.Type == TokenType.Identifier && 
           _current.StringValue == keyword;
}

private void ExpectContextualKeyword(string keyword)
{
    if (!CheckContextualKeyword(keyword))
        throw new JSSyntaxError($"Expected '{keyword}'", ...);
    NextToken();
}
```

## Error Handling

Module syntax errors:

| Error | Description |
|-------|-------------|
| `import is only valid in module code` | Using import in script mode |
| `export is only valid in module code` | Using export in script mode |
| `Unexpected token` | Malformed import/export syntax |
| `Expected 'from'` | Missing from clause |
| `Expected string literal` | Non-string module specifier |
| `Expected identifier` | Missing binding name |

## Test Coverage

68 comprehensive tests covering:

- **Import Tests**: Side-effect, default, named, namespace, mixed imports
- **Export Tests**: Named, default, declaration, re-exports
- **Dynamic Import Tests**: Basic, await, nested expressions
- **Error Tests**: Non-module usage, malformed syntax

## Architecture Notes

### Module Request Deduplication

The same module requested multiple times only creates one entry:

```csharp
public int AddModuleRequest(JSAtom moduleName)
{
    int index = ModuleRequests.IndexOf(moduleName);
    if (index < 0)
    {
        index = ModuleRequests.Count;
        ModuleRequests.Add(moduleName);
    }
    return index; // Returns existing index if already requested
}
```

### Static vs Dynamic Imports

- **Static imports**: Parsed at compile time, hoisted
- **Dynamic imports**: Parsed as expressions, resolved at runtime

### Module Scope

Modules have implicit strict mode and their own scope:

```javascript
// This is implicitly 'use strict'
export const x = 1;
```

## What's Next

Step 4.7 will implement the **Template Literal Parser** for tagged templates and template expressions:

```javascript
const str = `Hello, ${name}!`;
const html = html`<div>${content}</div>`;
```

## File Changes

- `src/QuickJS.Core/Parser.cs` - Module parsing methods (~400 lines)
- `src/QuickJS.Core/JSFunctionDef.cs` - Module bindings region
- `tests/QuickJS.Tests/ModuleParserTests.cs` - 68 tests

## Commit

```bash
git add -A
git commit -m "step-4.6-module-parser: Implement ES module import/export syntax

- Add IsModule property and module mode tracking to Parser
- Implement ParseImportDeclaration() for all import forms:
  - Side-effect imports: import 'module'
  - Default imports: import x from 'module'
  - Named imports: import { a, b } from 'module'
  - Namespace imports: import * as ns from 'module'
  - Mixed imports: import x, { y } from 'module'
  - Dynamic imports: import('module')
- Implement ParseExportDeclaration() for all export forms:
  - Named exports: export { a, b }
  - Declaration exports: export function/class/const/let/var
  - Default exports: export default expr
  - Re-exports: export * from 'module'
- Add contextual keyword handling for 'from' and 'as'
- Add module tracking to JSFunctionDef:
  - ModuleRequests for imported modules
  - ImportBindings for import mappings
  - ExportEntries for export mappings
- Add 68 comprehensive module parser tests

Tests: 2110 passing"
```
