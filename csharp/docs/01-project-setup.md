# Step 1.1: Project Setup

This document explains the initial project structure for QuickJS.NET, a C# implementation of the QuickJS JavaScript engine.

## Overview

QuickJS.NET is organized as a standard .NET solution with the following structure:

```
csharp/
├── QuickJS.NET.sln           # Solution file
├── README.md                  # Project overview and implementation plan
├── .gitignore                # Git ignore rules for .NET projects
├── docs/                     # Tutorial documentation
│   └── 01-project-setup.md   # This file
├── src/
│   └── QuickJS.Core/         # Main library project
│       ├── QuickJS.Core.csproj
│       └── Engine.cs         # Placeholder entry point
└── tests/
    └── QuickJS.Tests/        # Unit tests
        ├── QuickJS.Tests.csproj
        └── EngineTests.cs    # Initial test
```

## Target Frameworks

The library targets multiple .NET versions to maximize compatibility:

### netstandard2.0

- **Purpose**: Broad compatibility with .NET Framework 4.6.1+, .NET Core 2.0+, Mono, Xamarin, Unity
- **Why**: Many enterprise applications still run on .NET Framework 4.8.1
- **Trade-offs**: Some modern C# features require polyfills or alternative implementations

### net8.0

- **Purpose**: Current Long-Term Support (LTS) release
- **Why**: Production applications typically target LTS releases
- **Benefits**: Full access to modern APIs, better performance, Span<T> native support

### net10.0

- **Purpose**: Latest features and performance improvements
- **Why**: Early adopters and applications that want cutting-edge performance
- **Benefits**: Newest runtime optimizations, language features

## Project Configuration

### QuickJS.Core.csproj

Key configuration choices:

```xml
<TargetFrameworks>netstandard2.0;net8.0;net10.0</TargetFrameworks>
```

Multi-targeting allows a single codebase to produce binaries for all target frameworks.

```xml
<LangVersion>latest</LangVersion>
<Nullable>enable</Nullable>
```

We use the latest C# language version and enable nullable reference types for better null safety.

```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

Documentation is generated from XML comments, and warnings are treated as errors to maintain code quality.

### Polyfills for netstandard2.0

Several NuGet packages provide modern functionality on older frameworks:

| Package | Purpose |
|---------|---------|
| `System.Memory` | Provides `Span<T>`, `Memory<T>`, `ReadOnlySpan<T>` |
| `Microsoft.Bcl.HashCode` | Provides `HashCode` struct for combining hash codes |
| `Nullable` | Enables nullable reference type annotations |
| `IsExternalInit` | Enables `init` accessors for properties |

### QuickJS.Tests.csproj

The test project uses xUnit as the testing framework:

- **xUnit**: Modern, extensible test framework for .NET
- **Microsoft.NET.Test.Sdk**: Test host for running tests
- **coverlet.collector**: Code coverage collection

Note: Tests only target `net8.0` and `net10.0` since xUnit doesn't support `netstandard2.0` as a test target (test projects must target a specific runtime).

## Building the Project

### Command Line

```bash
# Restore dependencies
dotnet restore

# Build all targets
dotnet build

# Run tests
dotnet test

# Build release
dotnet build -c Release
```

### Visual Studio / Rider

Open `QuickJS.NET.sln` and build using the IDE.

## Next Steps

With the project structure in place, the next step is to define the core value types that represent JavaScript values in C#. See [02-value-types.md](02-value-types.md).

## References

- [.NET Standard](https://docs.microsoft.com/en-us/dotnet/standard/net-standard)
- [Multi-targeting](https://docs.microsoft.com/en-us/dotnet/standard/library-guidance/cross-platform-targeting)
- [xUnit Documentation](https://xunit.net/)
