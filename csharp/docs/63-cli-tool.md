# QuickJS.NET CLI Tool

The QuickJS.NET CLI (`qjs-net`) provides a command-line interface for running JavaScript code, similar to the original QuickJS `qjs` command.

## Table of Contents

- [Installation](#installation)
- [Usage](#usage)
- [Command-Line Options](#command-line-options)
- [Interactive REPL](#interactive-repl)
- [Examples](#examples)
- [Differences from QuickJS](#differences-from-quickjs)

## Installation

### From Source

Build and run directly:

```bash
cd csharp
dotnet run --project src/QuickJS.Cli/QuickJS.Cli.csproj
```

### As a Global Tool

Install as a .NET global tool:

```bash
cd csharp
dotnet pack src/QuickJS.Cli/QuickJS.Cli.csproj -c Release
dotnet tool install --global --add-source src/QuickJS.Cli/bin/Release QuickJS.NET.Cli
```

After installation, use the `qjs-net` command:

```bash
qjs-net --version
```

### As a Local Tool

For project-specific installation:

```bash
dotnet new tool-manifest
dotnet tool install --add-source src/QuickJS.Cli/bin/Release QuickJS.NET.Cli
dotnet tool run qjs-net --version
```

## Usage

### Execute a JavaScript File

```bash
qjs-net script.js
```

### Evaluate an Expression

```bash
qjs-net -e "1 + 2 + 3"
# Output: 6

qjs-net -e "console.log('Hello, World!')"
# Output: Hello, World!
```

### Start Interactive REPL

```bash
qjs-net
```

### Execute File with Arguments

```bash
qjs-net script.js arg1 arg2 arg3
```

Arguments are available in JavaScript as `scriptArgs`:

```javascript
// script.js
console.log("Script:", scriptArgs[0]);
console.log("Arguments:", scriptArgs.slice(1));
```

## Command-Line Options

| Option | Description |
|--------|-------------|
| `-h`, `--help` | Show help message |
| `-v`, `--version` | Show version information |
| `-e`, `--eval <expr>` | Evaluate JavaScript expression |
| `-i`, `--interactive` | Force interactive mode after file execution |

### Option Details

#### `-e, --eval <expression>`

Evaluates a JavaScript expression and prints the result (unless undefined):

```bash
# Simple math
qjs-net -e "Math.PI * 2"
# Output: 6.283185307179586

# Object creation
qjs-net -e "JSON.stringify({a: 1, b: 2})"
# Output: "{"a":1,"b":2}"

# Multi-statement (use semicolons)
qjs-net -e "var x = 10; var y = 20; x + y"
# Output: 30
```

#### `-i, --interactive`

Forces interactive mode. Useful after running a file to inspect its state:

```bash
qjs-net -i script.js
# Runs script.js, then enters REPL with script's globals available
```

## Interactive REPL

The REPL (Read-Eval-Print Loop) provides an interactive JavaScript environment.

### Starting the REPL

```bash
qjs-net
```

Output:
```
QuickJS.NET - JavaScript Engine for .NET
Version 0.1.0
Type '.help' for help, '.exit' to quit

qjs>
```

### REPL Commands

Commands start with a dot (`.`):

| Command | Description |
|---------|-------------|
| `.help` | Show help message |
| `.exit`, `.quit` | Exit the REPL |
| `.clear` | Clear the screen |
| `.load <file>` | Load and execute a JavaScript file |
| `.reset` | Clear the current input buffer |

### Multi-line Input

The REPL automatically detects incomplete statements and continues on the next line:

```
qjs> function factorial(n) {
...>   if (n <= 1) return 1;
...>   return n * factorial(n - 1);
...> }
undefined
qjs> factorial(5)
120
```

### Result Display

Results are automatically formatted:
- Strings are shown with quotes
- Objects are displayed with property listings
- Arrays show their contents (up to 100 items)
- Functions show their name

```
qjs> "hello"
'hello'
qjs> [1, 2, 3]
[ 1, 2, 3 ]
qjs> {a: 1, b: 2}
{ a: 1, b: 2 }
qjs> function foo() {}
[Function: foo]
```

### Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `Enter` | Execute or continue multi-line input |
| `Ctrl+C` | Cancel current input |
| `Ctrl+D` | Exit REPL (EOF) |

## Examples

### Hello World

```bash
qjs-net -e "console.log('Hello, World!')"
```

### Simple Calculator

```bash
qjs-net -e "((10 + 5) * 2 - 3) / 7"
```

### Working with JSON

```bash
qjs-net -e "var obj = {name: 'Alice', age: 30}; JSON.stringify(obj, null, 2)"
```

### File Processing Script

Create `process.js`:
```javascript
// process.js
var filename = scriptArgs[1];
if (!filename) {
    console.error("Usage: qjs-net process.js <filename>");
} else {
    console.log("Would process:", filename);
}
```

Run it:
```bash
qjs-net process.js data.txt
```

### Interactive Session

```
qjs> var fruits = ['apple', 'banana', 'cherry']
undefined
qjs> fruits.map(f => f.toUpperCase())
[ 'APPLE', 'BANANA', 'CHERRY' ]
qjs> fruits.filter(f => f.length > 5)
[ 'banana', 'cherry' ]
qjs> fruits.reduce((acc, f) => acc + f.length, 0)
17
```

### Loading Files in REPL

```
qjs> .load utils.js
Loading utils.js...
File loaded successfully.
qjs> myUtilFunction()
// Result from the loaded file
```

## Differences from QuickJS

The `qjs-net` CLI aims to be compatible with the original QuickJS `qjs` command, but there are some differences:

### Supported Features

- ✅ File execution
- ✅ Expression evaluation (`-e`)
- ✅ Interactive REPL
- ✅ Script arguments (`scriptArgs`)
- ✅ Console output

### Not Yet Implemented

- ❌ Module loading (`import`/`export`)
- ❌ BigFloat/BigDecimal extensions
- ❌ Worker threads
- ❌ `std` and `os` modules
- ❌ Bytecode compilation (`-c`)
- ❌ Stack size configuration

### Planned Features

The following features are planned for future releases:

1. **Module support**: ES6 module loading
2. **Standard library**: `std` and `os` module equivalents
3. **Bytecode**: Save/load compiled bytecode
4. **Debug mode**: Step-through debugging

## Troubleshooting

### "Command not found"

If installed as a global tool but not found:

```bash
# Check if .NET tools are in PATH
dotnet tool list -g

# Add to PATH if needed (bash/zsh)
export PATH="$PATH:$HOME/.dotnet/tools"
```

### "Assembly not found"

Ensure QuickJS.Core is built:

```bash
cd csharp
dotnet build
```

### Slow Startup

First run may be slow due to JIT compilation. Subsequent runs will be faster.

## See Also

- [Public API Reference](60-public-api.md)
- [C# Interop Guide](61-csharp-interop.md)
- [Performance Guide](62-performance.md)
