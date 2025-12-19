# QuickJS.NET

This repository contains two implementations of QuickJS
1- The original C based QuickJS implementation, at the root
2- The .NET version, QuickJS.NET, in the `csharp` folder

When implementing a feature in QuickJS.NET always try to follow the original C implementation design.

## Building

The whole QuickJS.NET solution

```shell
dotnet build ./csharp/QuickJS.NET.slnx
```

The QuickJS.Core project

```shell
dotnet build ./csharp/src/QuickJS.Core/QuickJS.Core.csproj
```

## Testing

All tests

```shell
cd csharp && dotnet test
```

A specific test

```shell
cd csharp && dotnet test --filter-method '*[MethodName]*'
```

## Benchmarking

QuickJS.NET is benchmarked against Jint, a popular .NET JavaScript interpreter.

Benchmarks for QuickJS.NET are implemented in `./csharp/benchmarks/QuickJS.Benchmarks`.

To run a single benchmark:

```shell
dotnet run --project ./csharp/benchmarks/QuickJS.Benchmarks/QuickJS.Benchmarks.csproj -c release --filter `*QuickJS_Arithmetic*`
```

To run all QuickJS.NET benchmarks

```shell
dotnet run --project ./csharp/benchmarks/QuickJS.Benchmarks/QuickJS.Benchmarks.csproj -c release --filter `*QuickJS_*`
```