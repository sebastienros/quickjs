# Tests

This folder contains the C# test projects for QuickJS.

## Projects

- `QuickJS.Tests`: unit tests for the C# engine.
- `QuickJS.Test262`: xUnit runner for the Test262 suite.

## Running tests

From `csharp/`:

```bash
dotnet test
```

Run the Test262 project only:

```bash
dotnet test tests/QuickJS.Test262/QuickJS.Test262.csproj
```

Update `test262.new` explicitly (opt-in) when running Test262:

```bash
dotnet test tests/QuickJS.Test262/QuickJS.Test262.csproj -- --update-new
```

## Test262 update workflow

The Test262 runner uses three list files under `csharp/tests/QuickJS.Test262/`:

- `test262.run`: allowlist of tests to execute.
- `test262.ignore`: explicit skip list.
- `test262.new`: discovered tests not covered by `run`/`ignore` (only updated with `--update-new`).

To update the Test262 commit hash, edit the root `test262.commit` file to the new commit SHA. The next run will use that hash to fetch or reuse the cached Test262 archive.
