## Executing tests

Investigating tests should target a single framework for faster feedback loop (`-f [TFM]`)
Validating all tests are passing should be done without limiting the target framework.

- All tests on all frameworks

```shell
dotnet test --project tests/QuickJS.Tests/QuickJS.Tests.csproj 
```

- Specific framework

```shell
dotnet test --project tests/QuickJS.Tests/QuickJS.Tests.csproj 
 -f net10.0
 ```

- Specific test class

```shell
dotnet test --project tests/QuickJS.Tests/QuickJS.Tests.csproj --filter-class 'QuickJS.Tests.BuiltinsArrayTests'
```

- Specific test method

```shell
dotnet test --project tests/QuickJS.Tests/QuickJS.Tests.csproj --filter-class 'QuickJS.Tests.BuiltinsArrayTests.ArrayConstructor_WithElements_SetsLengthAndValues'
```

or using a filter with only the method name

```shell
dotnet test --project tests/QuickJS.Tests/QuickJS.Tests.csproj --filter-class '*.ArrayConstructor_WithElements_SetsLengthAndValues'
```
