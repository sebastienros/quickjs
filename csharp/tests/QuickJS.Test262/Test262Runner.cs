// Licensed under the MIT License.

using System.Globalization;
using System.Text;

namespace QuickJS.Test262;

internal sealed class Test262Runner
{
    private readonly Action<string>? _log;
    private readonly Test262Config _config;
    private readonly Test262Paths _paths;
    private readonly Test262ErrorCatalog? _errors;
    private readonly int _startIndex;
    private readonly int _stopIndex;
    private readonly string? _filter;

    private Test262Runner(
        Action<string>? log,
        Test262Config config,
        Test262Paths paths,
        Test262ErrorCatalog? errors,
        int startIndex,
        int stopIndex,
        string? filter)
    {
        _log = log;
        _config = config;
        _paths = paths;
        _errors = errors;
        _startIndex = startIndex;
        _stopIndex = stopIndex;
        _filter = filter;
    }

    public static bool TryCreateDefault(
        Action<string>? log,
        out Test262Runner runner,
        out string? skipReason)
    {
        runner = null!;
        skipReason = null;

        var repoRoot = FindRepoRoot();
        if (repoRoot == null)
        {
            skipReason = "Unable to locate repository root for test262.conf.";
            return false;
        }

        var configPath = Environment.GetEnvironmentVariable("TEST262_CONFIG");
        if (string.IsNullOrEmpty(configPath))
        {
            configPath = Path.Combine(repoRoot, "test262.conf");
        }

        if (!File.Exists(configPath))
        {
            skipReason = $"test262 config not found at '{configPath}'.";
            return false;
        }

        var config = Test262Config.Load(configPath);

        var test262Root = Environment.GetEnvironmentVariable("TEST262_DIR");
        if (string.IsNullOrEmpty(test262Root))
        {
            var defaultRoot = Path.Combine(repoRoot, "test262");
            if (Directory.Exists(defaultRoot))
            {
                test262Root = defaultRoot;
            }
        }

        var paths = new Test262Paths(Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? repoRoot, test262Root);

        var listOverride = Environment.GetEnvironmentVariable("TEST262_LIST");
        if (!string.IsNullOrEmpty(listOverride))
        {
            config.TestList.Clear();
            config.TestDirs.Clear();
            config.LoadTestList(listOverride);
        }

        if (config.TestDirs.Count == 0 && config.TestList.Count == 0)
        {
            skipReason = "No test262 test directories or test list configured.";
            return false;
        }

        var testDirExists = config.TestDirs
            .Select(paths.ToFullPath)
            .Any(Directory.Exists);

        if (!testDirExists && config.TestDirs.Count > 0)
        {
            skipReason = "test262 test directory not found. Set TEST262_DIR or update test262.conf.";
            return false;
        }

        var harnessDirDisplay = config.HarnessDir ?? "";
        if (!string.IsNullOrEmpty(harnessDirDisplay))
        {
            var harnessDir = paths.ToFullPath(harnessDirDisplay);
            if (!Directory.Exists(harnessDir))
            {
                skipReason = "test262 harness directory not found. Set TEST262_DIR or update test262.conf.";
                return false;
            }
        }

        var errorPath = Environment.GetEnvironmentVariable("TEST262_ERROR_FILE");
        if (string.IsNullOrEmpty(errorPath))
        {
            errorPath = config.ErrorFile ?? Path.Combine(repoRoot, "test262_errors.txt");
        }

        Test262ErrorCatalog? errors = null;
        var errorFullPath = paths.ToFullPath(errorPath);
        if (File.Exists(errorFullPath))
        {
            errors = Test262ErrorCatalog.Load(errorFullPath, paths);
        }

        int startIndex = 0;
        int stopIndex = -1;
        if (int.TryParse(Environment.GetEnvironmentVariable("TEST262_START"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var start))
        {
            startIndex = Math.Max(0, start);
        }
        if (int.TryParse(Environment.GetEnvironmentVariable("TEST262_STOP"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var stop))
        {
            stopIndex = stop;
        }

        var filter = Environment.GetEnvironmentVariable("TEST262_FILTER");

        runner = new Test262Runner(log, config, paths, errors, startIndex, stopIndex, filter);
        return true;
    }

    public Test262RunResult Run()
    {
        Environment.SetEnvironmentVariable("TZ", "America/Los_Angeles");

        var tests = BuildTestList();
        var excludes = Test262ExcludeFilter.Create(_config, _paths);
        var result = new Test262RunResult();

        using var reportWriter = CreateReportWriter(_config, _paths);

        int index = 0;
        foreach (var test in tests)
        {
            if (excludes.IsExcluded(test.DisplayPath))
            {
                result.Excluded++;
                index++;
                continue;
            }

            if (_filter != null && test.DisplayPath.IndexOf(_filter, StringComparison.Ordinal) < 0)
            {
                result.Skipped++;
                index++;
                continue;
            }

            if (index < _startIndex || (_stopIndex >= 0 && index > _stopIndex))
            {
                result.Skipped++;
                index++;
                continue;
            }

            var outcome = RunTest(test, index, reportWriter);
            result.Add(outcome);
            index++;
        }

        result.FinishSummary();
        return result;
    }

    private IReadOnlyList<Test262Test> BuildTestList()
    {
        var list = new List<Test262Test>();

        foreach (var testDirDisplay in _config.TestDirs)
        {
            var testDir = _paths.ToFullPath(testDirDisplay);
            if (!Directory.Exists(testDir))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(testDir, "*.js", SearchOption.AllDirectories))
            {
                if (file.EndsWith("_FIXTURE.js", StringComparison.Ordinal))
                {
                    continue;
                }

                var displayPath = _paths.ToDisplayPath(file);
                list.Add(new Test262Test(displayPath, file));
            }
        }

        foreach (var testPathDisplay in _config.TestList)
        {
            var fullPath = _paths.ToFullPath(testPathDisplay);
            list.Add(new Test262Test(Test262Paths.NormalizeDisplayPath(testPathDisplay), fullPath));
        }

        list.Sort(Test262TestComparer.Instance);
        return list;
    }

    private Test262RunOutcome RunTest(Test262Test test, int index, TextWriter? reportWriter)
    {
        string source = File.ReadAllText(test.FullPath);
        var metadata = Test262Metadata.Parse(source, _config.NewStyle);

        bool skip = false;

        var includes = new List<string> { "sta.js" };
        if (_config.NewStyle)
        {
            includes.Add("assert.js");
            includes.AddRange(metadata.Includes);
        }
        else
        {
            includes.AddRange(Test262Metadata.ParseOldStyleIncludes(source));
        }

        if (metadata.IsAsync)
        {
            includes.Add("doneprintHandle.js");
        }

        foreach (var include in includes)
        {
            if (_config.HarnessExclude.Contains(include))
            {
                skip = true;
                break;
            }
        }

        if (metadata.IsNoStrict)
        {
            skip |= _config.Mode == Test262Mode.Strict;
        }
        if (metadata.IsOnlyStrict)
        {
            skip |= _config.Mode == Test262Mode.NoStrict;
        }
        if (metadata.IsAsync)
        {
            skip |= _config.SkipAsync;
        }
        if (metadata.IsModule)
        {
            skip |= _config.SkipModule;
        }

        foreach (var feature in metadata.Features)
        {
            if (_config.Features.Contains(feature))
            {
                continue;
            }

            if (_config.SkipFeatures.Contains(feature))
            {
                skip = true;
                break;
            }

            _log?.Invoke($"{test.DisplayPath}: unknown feature: {feature}");
            skip = true;
            break;
        }

        int useStrict = 0;
        int useNoStrict = 0;
        switch (_config.Mode)
        {
            case Test262Mode.DefaultNoStrict:
                if (metadata.IsOnlyStrict)
                {
                    useStrict = 1;
                }
                else
                {
                    useNoStrict = 1;
                }
                break;
            case Test262Mode.DefaultStrict:
                if (metadata.IsNoStrict)
                {
                    useNoStrict = 1;
                }
                else
                {
                    useStrict = 1;
                }
                break;
            case Test262Mode.NoStrict:
                if (!metadata.IsOnlyStrict)
                {
                    useNoStrict = 1;
                }
                break;
            case Test262Mode.Strict:
                if (!metadata.IsNoStrict)
                {
                    useStrict = 1;
                }
                break;
            case Test262Mode.All:
                if (metadata.IsModule)
                {
                    useNoStrict = 1;
                }
                else
                {
                    if (!metadata.IsNoStrict)
                    {
                        useStrict = 1;
                    }
                    if (!metadata.IsOnlyStrict)
                    {
                        useNoStrict = 1;
                    }
                }
                break;
        }

        WriteReportLine(reportWriter, index, test, metadata, skip);

        if (skip || useStrict + useNoStrict == 0)
        {
            return Test262RunOutcome.Skipped(test.DisplayPath);
        }

        var outcome = new Test262RunOutcome(test.DisplayPath, skipped: false);
        if (useNoStrict != 0)
        {
            outcome.Merge(RunTestOnce(test, metadata, source, includes, isStrict: false));
        }
        if (useStrict != 0)
        {
            outcome.Merge(RunTestOnce(test, metadata, source, includes, isStrict: true));
        }

        return outcome;
    }

    private Test262RunOutcome RunTestOnce(
        Test262Test test,
        Test262Metadata metadata,
        string source,
        List<string> includes,
        bool isStrict)
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();
        runtime.RuntimeInfo = test.DisplayPath;

        var state = new Test262ContextState(runtime, context, _paths);
        state.InstallHelpers();
        ConfigureModuleLoader(context, state.Paths);

        var harnessDisplayDir = ResolveHarnessDir(test.DisplayPath, metadata.IsNewStyle, _config.HarnessDir);

        foreach (var include in includes.Distinct(StringComparer.Ordinal))
        {
            var harnessDisplayPath = Test262Paths.CombineDisplay(harnessDisplayDir, include, state.Paths);
            var harnessFullPath = state.Paths.ToFullPath(harnessDisplayPath);
            if (!File.Exists(harnessFullPath))
            {
                throw new InvalidOperationException($"Harness file not found: {harnessFullPath}");
            }

            var harnessSource = File.ReadAllText(harnessFullPath);
            var harnessResult = context.Evaluate(harnessSource, harnessDisplayPath);
            if (harnessResult.IsException || context.HasException)
            {
                var errorInfo = Test262ErrorInfo.FromContext(context, harnessDisplayPath);
                return EvaluateAgainstKnownErrors(test.DisplayPath, isStrict, metadata, errorInfo);
            }
        }

        string evalSource = source;
        if (isStrict && !metadata.IsModule)
        {
            evalSource = "\"use strict\";\n" + source;
        }

        JSValue resultValue = metadata.IsModule
            ? context.EvaluateModule(evalSource, test.DisplayPath)
            : context.Evaluate(evalSource, test.DisplayPath);

        if (resultValue.IsException || context.HasException)
        {
            var errorInfo = Test262ErrorInfo.FromContext(context, test.DisplayPath);
            return EvaluateAgainstKnownErrors(test.DisplayPath, isStrict, metadata, errorInfo);
        }

        if (metadata.IsAsync || metadata.IsModule)
        {
            state.RunPendingJobs();
        }

        if (metadata.IsAsync && state.AsyncDone != 1)
        {
            var asyncError = new Test262ErrorInfo(
                "TypeError",
                state.AsyncDone == 2 ? "Test262:AsyncTestFailure" : "$DONE() not called",
                1);
            return EvaluateAgainstKnownErrors(test.DisplayPath, isStrict, metadata, asyncError);
        }

        if (context.HasException)
        {
            var errorInfo = Test262ErrorInfo.FromContext(context, test.DisplayPath);
            return EvaluateAgainstKnownErrors(test.DisplayPath, isStrict, metadata, errorInfo);
        }

        return EvaluateAgainstKnownErrors(test.DisplayPath, isStrict, metadata, null);
    }

    private Test262RunOutcome EvaluateAgainstKnownErrors(
        string displayPath,
        bool isStrict,
        Test262Metadata metadata,
        Test262ErrorInfo? errorInfo)
    {
        bool hasError = errorInfo != null;
        bool negativeExpected = metadata.IsNegative;

        string? failureMessage = null;
        if (negativeExpected)
        {
            if (!hasError)
            {
                failureMessage = "expected error";
            }
            else if (metadata.NegativeType != null && !string.Equals(errorInfo!.ErrorClass, metadata.NegativeType, StringComparison.Ordinal))
            {
                failureMessage = $"unexpected error type: {errorInfo!.Message}";
            }
        }
        else if (hasError)
        {
            failureMessage = errorInfo!.Message;
        }

        var expectedEntry = _errors?.Find(displayPath, isStrict);

        if (failureMessage == null)
        {
            if (expectedEntry != null)
            {
                return Test262RunOutcome.Fixed(displayPath, expectedEntry.Message);
            }

            return Test262RunOutcome.Passed(displayPath);
        }

        if (expectedEntry == null)
        {
            return Test262RunOutcome.NewError(displayPath, failureMessage);
        }

        if (!string.Equals(expectedEntry.Message, failureMessage, StringComparison.Ordinal))
        {
            return Test262RunOutcome.Changed(displayPath, failureMessage, expectedEntry.Message);
        }

        if (errorInfo != null && errorInfo.Line != 0 && expectedEntry.Line != 0 && errorInfo.Line != expectedEntry.Line)
        {
            return Test262RunOutcome.Changed(displayPath, failureMessage, expectedEntry.Message);
        }

        return Test262RunOutcome.ExpectedFailure(displayPath, failureMessage);
    }

    private static string ResolveHarnessDir(string testDisplayPath, bool newStyle, string? configuredHarnessDir)
    {
        if (!string.IsNullOrEmpty(configuredHarnessDir))
        {
            return Test262Paths.NormalizeDisplayPath(configuredHarnessDir);
        }

        var marker = "test/";
        var index = testDisplayPath.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return "";
        }

        var prefix = testDisplayPath.Substring(0, index);
        return newStyle ? prefix + "harness" : prefix + "test/harness";
    }

    private void ConfigureModuleLoader(JSContext context, Test262Paths paths)
    {
        var loader = context.ModuleLoader;
        loader.Resolver = (baseModuleName, moduleName) => ResolveModuleName(baseModuleName, moduleName, paths);
        loader.Loader = modulePath =>
        {
            var fullPath = paths.ToFullPath(modulePath);
            if (!File.Exists(fullPath))
            {
                return null;
            }
            return File.ReadAllText(fullPath);
        };
    }

    private static string ResolveModuleName(string? baseModuleName, string moduleName, Test262Paths paths)
    {
        var baseDisplayPath = string.IsNullOrEmpty(baseModuleName)
            ? paths.DisplayRoot
            : Test262Paths.NormalizeDisplayPath(baseModuleName);

        var baseDirFull = paths.ToFullPath(baseDisplayPath);
        var baseDir = Path.GetDirectoryName(baseDirFull) ?? baseDirFull;

        string resolvedDisplay;
        if (moduleName.StartsWith("./", StringComparison.Ordinal) || moduleName.StartsWith("../", StringComparison.Ordinal))
        {
            var full = Path.GetFullPath(Path.Combine(baseDir, moduleName));
            resolvedDisplay = paths.ToDisplayPath(full);
        }
        else if (!moduleName.Contains("/", StringComparison.Ordinal))
        {
            var full = Path.GetFullPath(Path.Combine(baseDir, moduleName));
            resolvedDisplay = paths.ToDisplayPath(full);
        }
        else
        {
            resolvedDisplay = Test262Paths.NormalizeDisplayPath(moduleName);
        }

        if (!resolvedDisplay.EndsWith(".js", StringComparison.Ordinal))
        {
            resolvedDisplay += ".js";
        }

        return resolvedDisplay;
    }

    private static TextWriter? CreateReportWriter(Test262Config config, Test262Paths paths)
    {
        if (string.IsNullOrEmpty(config.ReportFile) || string.Equals(config.ReportFile, "none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(config.ReportFile, "-", StringComparison.Ordinal))
        {
            return Console.Out;
        }

        var reportPath = paths.ToFullPath(config.ReportFile);
        return new StreamWriter(reportPath, false, new UTF8Encoding(false));
    }

    private static void WriteReportLine(TextWriter? writer, int index, Test262Test test, Test262Metadata metadata, bool skipped)
    {
        if (writer == null)
        {
            return;
        }

        writer.Write(index);
        writer.Write(": ");
        writer.Write(test.DisplayPath);
        if (metadata.IsNoStrict)
        {
            writer.Write("  @noStrict");
        }
        if (metadata.IsOnlyStrict)
        {
            writer.Write("  @onlyStrict");
        }
        if (metadata.IsAsync)
        {
            writer.Write("  async");
        }
        if (metadata.IsModule)
        {
            writer.Write("  module");
        }
        if (metadata.IsNegative)
        {
            writer.Write("  @negative");
        }
        if (skipped)
        {
            writer.Write("  SKIPPED");
        }
        writer.WriteLine();
        writer.Flush();
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "test262.conf")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        return null;
    }
}

internal enum Test262Mode
{
    DefaultNoStrict,
    DefaultStrict,
    NoStrict,
    Strict,
    All,
}

internal sealed class Test262Config
{
    public bool NewStyle { get; private set; }
    public Test262Mode Mode { get; private set; } = Test262Mode.DefaultNoStrict;
    public bool SkipAsync { get; private set; }
    public bool SkipModule { get; private set; }
    public bool Verbose { get; private set; }
    public string? HarnessDir { get; private set; }
    public string? ErrorFile { get; private set; }
    public string? ReportFile { get; private set; }
    public List<string> TestDirs { get; } = new();
    public List<string> TestList { get; } = new();
    public HashSet<string> ExcludeList { get; } = new(StringComparer.Ordinal);
    public HashSet<string> HarnessExclude { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Features { get; } = new(StringComparer.Ordinal);
    public HashSet<string> SkipFeatures { get; } = new(StringComparer.Ordinal);

    private readonly string _baseDir;

    private Test262Config(string baseDir)
    {
        _baseDir = baseDir;
    }

    public static Test262Config Load(string path)
    {
        var baseDir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? Directory.GetCurrentDirectory();
        var config = new Test262Config(baseDir);
        config.LoadFromFile(path);
        return config;
    }

    public void LoadTestList(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.StartsWith(';'))
            {
                continue;
            }
            TestList.Add(Test262Paths.NormalizeDisplayPath(trimmed));
        }
    }

    private void LoadFromFile(string path)
    {
        var section = Test262Section.None;

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
            {
                continue;
            }

            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                var name = line.Substring(1, line.Length - 2);
                section = name switch
                {
                    "config" => Test262Section.Config,
                    "exclude" => Test262Section.Exclude,
                    "features" => Test262Section.Features,
                    "tests" => Test262Section.Tests,
                    _ => Test262Section.None,
                };
                continue;
            }

            string? key = null;
            string? value = null;
            var splitIndex = line.IndexOf('=');
            if (splitIndex >= 0)
            {
                key = line.Substring(0, splitIndex).Trim();
                value = line.Substring(splitIndex + 1).Trim();
            }

            switch (section)
            {
                case Test262Section.Config:
                    if (key == null || value == null)
                    {
                        continue;
                    }
                    ApplyConfig(key, value);
                    break;
                case Test262Section.Exclude:
                    ExcludeList.Add(Test262Paths.NormalizeDisplayPath(line));
                    break;
                case Test262Section.Features:
                    ApplyFeature(key ?? line, value);
                    break;
                case Test262Section.Tests:
                    TestList.Add(Test262Paths.NormalizeDisplayPath(line));
                    break;
            }
        }
    }

    private void ApplyConfig(string key, string value)
    {
        if (string.Equals(key, "style", StringComparison.Ordinal))
        {
            NewStyle = string.Equals(value, "new", StringComparison.OrdinalIgnoreCase);
            return;
        }

        if (string.Equals(key, "testdir", StringComparison.Ordinal))
        {
            TestDirs.Add(Test262Paths.NormalizeDisplayPath(value));
            return;
        }

        if (string.Equals(key, "harnessdir", StringComparison.Ordinal))
        {
            HarnessDir = Test262Paths.NormalizeDisplayPath(value);
            return;
        }

        if (string.Equals(key, "harnessexclude", StringComparison.Ordinal))
        {
            foreach (var entry in SplitWords(value))
            {
                HarnessExclude.Add(entry);
            }
            return;
        }

        if (string.Equals(key, "features", StringComparison.Ordinal))
        {
            foreach (var entry in SplitWords(value))
            {
                Features.Add(entry);
            }
            return;
        }

        if (string.Equals(key, "skip-features", StringComparison.Ordinal))
        {
            foreach (var entry in SplitWords(value))
            {
                SkipFeatures.Add(entry);
            }
            return;
        }

        if (string.Equals(key, "mode", StringComparison.Ordinal))
        {
            Mode = value switch
            {
                "default" or "default-nostrict" => Test262Mode.DefaultNoStrict,
                "default-strict" => Test262Mode.DefaultStrict,
                "nostrict" => Test262Mode.NoStrict,
                "strict" => Test262Mode.Strict,
                "all" or "both" => Test262Mode.All,
                _ => Mode,
            };
            return;
        }

        if (string.Equals(key, "strict", StringComparison.Ordinal))
        {
            if (string.Equals(value, "skip", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
            {
                Mode = Test262Mode.NoStrict;
            }
            return;
        }

        if (string.Equals(key, "nostrict", StringComparison.Ordinal))
        {
            if (string.Equals(value, "skip", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
            {
                Mode = Test262Mode.Strict;
            }
            return;
        }

        if (string.Equals(key, "async", StringComparison.Ordinal))
        {
            SkipAsync = !string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
            return;
        }

        if (string.Equals(key, "module", StringComparison.Ordinal))
        {
            SkipModule = !string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
            return;
        }

        if (string.Equals(key, "verbose", StringComparison.Ordinal))
        {
            Verbose = string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
            return;
        }

        if (string.Equals(key, "errorfile", StringComparison.Ordinal))
        {
            ErrorFile = Test262Paths.NormalizeDisplayPath(value);
            return;
        }

        if (string.Equals(key, "excludefile", StringComparison.Ordinal))
        {
            var excludePath = ResolveRelativePath(value);
            foreach (var line in File.ReadLines(excludePath))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.StartsWith(';'))
                {
                    continue;
                }
                ExcludeList.Add(Test262Paths.NormalizeDisplayPath(trimmed));
            }
            return;
        }

        if (string.Equals(key, "reportfile", StringComparison.Ordinal))
        {
            ReportFile = Test262Paths.NormalizeDisplayPath(value);
        }
    }

    private void ApplyFeature(string key, string? value)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        if (value == null || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
        {
            Features.Add(key);
            return;
        }

        SkipFeatures.Add(key);
    }

    private string ResolveRelativePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.Combine(_baseDir, path);
    }

    private static IEnumerable<string> SplitWords(string value)
    {
        var start = 0;
        for (int i = 0; i <= value.Length; i++)
        {
            if (i == value.Length || char.IsWhiteSpace(value[i]) || value[i] == ',')
            {
                if (i > start)
                {
                    var word = value.Substring(start, i - start).Trim();
                    if (word.Length > 0)
                    {
                        yield return word;
                    }
                }
                start = i + 1;
            }
        }
    }
}

internal enum Test262Section
{
    None,
    Config,
    Exclude,
    Features,
    Tests,
}

internal sealed record Test262Test(string DisplayPath, string FullPath);

internal sealed class Test262TestComparer : IComparer<Test262Test>
{
    public static readonly Test262TestComparer Instance = new();

    public int Compare(Test262Test? x, Test262Test? y)
    {
        if (x == null || y == null)
        {
            return x == y ? 0 : x == null ? -1 : 1;
        }
        return Test262Paths.CompareNatural(x.DisplayPath, y.DisplayPath);
    }
}

internal sealed class Test262ExcludeFilter
{
    private readonly HashSet<string> _excludeList;
    private readonly List<string> _excludeDirs;

    private Test262ExcludeFilter(HashSet<string> excludeList, List<string> excludeDirs)
    {
        _excludeList = excludeList;
        _excludeDirs = excludeDirs;
    }

    public static Test262ExcludeFilter Create(Test262Config config, Test262Paths paths)
    {
        var list = new HashSet<string>(StringComparer.Ordinal);
        var dirs = new List<string>();

        foreach (var entry in config.ExcludeList)
        {
            var normalized = Test262Paths.NormalizeDisplayPath(entry);
            if (normalized.EndsWith("/", StringComparison.Ordinal))
            {
                dirs.Add(normalized);
            }
            else
            {
                list.Add(normalized);
            }
        }

        dirs.Sort(StringComparer.Ordinal);
        return new Test262ExcludeFilter(list, dirs);
    }

    public bool IsExcluded(string displayPath)
    {
        if (_excludeList.Contains(displayPath))
        {
            return true;
        }

        foreach (var dir in _excludeDirs)
        {
            if (displayPath.StartsWith(dir, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class Test262Metadata
{
    public bool IsNoStrict { get; private set; }
    public bool IsOnlyStrict { get; private set; }
    public bool IsNegative { get; private set; }
    public bool IsAsync { get; private set; }
    public bool IsModule { get; private set; }
    public bool CanBlockIsFalse { get; private set; }
    public bool IsRaw { get; private set; }
    public string? NegativeType { get; private set; }
    public bool IsNewStyle { get; private set; }
    public List<string> Includes { get; } = new();
    public List<string> Features { get; } = new();

    public static Test262Metadata Parse(string source, bool newStyle)
    {
        var metadata = new Test262Metadata { IsNewStyle = newStyle };

        if (newStyle)
        {
            var desc = ExtractDesc(source, '-');
            if (desc != null)
            {
                foreach (var include in ParseTagOptions(desc, "includes:"))
                {
                    metadata.Includes.Add(include);
                }

                foreach (var flag in ParseTagOptions(desc, "flags:"))
                {
                    switch (flag)
                    {
                        case "noStrict":
                        case "raw":
                            metadata.IsNoStrict = true;
                            if (flag == "raw")
                            {
                                metadata.IsRaw = true;
                            }
                            break;
                        case "onlyStrict":
                            metadata.IsOnlyStrict = true;
                            break;
                        case "async":
                            metadata.IsAsync = true;
                            break;
                        case "module":
                            metadata.IsModule = true;
                            break;
                        case "CanBlockIsFalse":
                            metadata.CanBlockIsFalse = true;
                            break;
                    }
                }

                var negativeSection = FindTag(desc, "negative:");
                if (negativeSection != null)
                {
                    metadata.IsNegative = true;
                    var typeSection = FindTag(negativeSection, "type:");
                    if (typeSection != null)
                    {
                        metadata.NegativeType = ReadFirstToken(typeSection);
                    }
                }

                foreach (var feature in ParseTagOptions(desc, "features:"))
                {
                    metadata.Features.Add(feature);
                }
            }
        }
        else
        {
            var desc = ExtractDesc(source, '*');
            if (desc != null)
            {
                metadata.IsNoStrict = desc.Contains("@noStrict", StringComparison.Ordinal);
                metadata.IsOnlyStrict = desc.Contains("@onlyStrict", StringComparison.Ordinal);
                metadata.IsNegative = desc.Contains("@negative", StringComparison.Ordinal);
            }
        }

        return metadata;
    }

    public static List<string> ParseOldStyleIncludes(string source)
    {
        var includes = new List<string>();
        var index = 0;
        while (index < source.Length)
        {
            index = source.IndexOf("$INCLUDE(", index, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            index += "$INCLUDE(".Length;
            if (index >= source.Length)
            {
                break;
            }

            var quote = source[index];
            if (quote != '\'' && quote != '"')
            {
                index++;
                continue;
            }

            index++;
            var start = index;
            while (index < source.Length && source[index] != quote)
            {
                index++;
            }

            if (index > start)
            {
                includes.Add(source.Substring(start, index - start));
            }
        }

        return includes;
    }

    private static string? ExtractDesc(string source, char style)
    {
        for (int i = 0; i < source.Length - 3; i++)
        {
            if (source[i] == '/' && source[i + 1] == '*' && source[i + 2] == style && source[i + 3] != '/')
            {
                var start = i + 3;
                var end = source.IndexOf("*/", start, StringComparison.Ordinal);
                if (end < 0)
                {
                    return null;
                }
                return source.Substring(start, end - start);
            }
        }

        return null;
    }

    private static string? FindTag(string desc, string tag)
    {
        var index = desc.IndexOf(tag, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        return desc.Substring(index + tag.Length);
    }

    private static List<string> ParseTagOptions(string desc, string tag)
    {
        var options = new List<string>();
        var section = FindTag(desc, tag);
        if (section == null)
        {
            return options;
        }

        int state = 0;
        int index = 0;
        while (index < section.Length)
        {
            var option = GetOption(section, ref index, ref state);
            if (option == null)
            {
                break;
            }
            options.Add(option);
        }

        return options;
    }

    private static string? GetOption(string text, ref int index, ref int state)
    {
        while (index < text.Length)
        {
            var ch = text[index];
            switch (ch)
            {
                case '[':
                    state++;
                    index++;
                    continue;
                case ']':
                    state--;
                    index++;
                    if (state > 0)
                    {
                        continue;
                    }
                    return null;
                case ' ':
                case '\t':
                case '\r':
                case ',':
                case '-':
                    index++;
                    continue;
                case '\n':
                    if (state > 0 || (index + 1 < text.Length && text[index + 1] == ' '))
                    {
                        index++;
                        continue;
                    }
                    return null;
                default:
                    var start = index;
                    while (index < text.Length && !IsOptionTerminator(text[index]))
                    {
                        index++;
                    }
                    return text.Substring(start, index - start);
            }
        }

        return null;
    }

    private static bool IsOptionTerminator(char ch)
    {
        return ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n' || ch == ',' || ch == ']';
    }

    private static string? ReadFirstToken(string text)
    {
        var index = 0;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        if (index >= text.Length)
        {
            return null;
        }

        var start = index;
        while (index < text.Length && !char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return text.Substring(start, index - start);
    }
}

internal sealed class Test262ContextState
{
    private readonly JSRuntime _runtime;
    private readonly JSContext _context;
    private readonly List<JSContext> _realms = new();

    public int AsyncDone { get; private set; }
    public Test262Paths Paths { get; }

    public Test262ContextState(JSRuntime runtime, JSContext context, Test262Paths paths)
    {
        _runtime = runtime;
        _context = context;
        Paths = paths;
    }

    public void InstallHelpers()
    {
        var functionProto = _context.GetClassPrototype(JSClassId.CFunction);

        _context.RegisterGlobalFunction("print", Print, 1);

        var obj262 = new JSObject();
        obj262.Set("detachArrayBuffer", JSValue.FromObject(new JSFunction(DetachArrayBuffer, "detachArrayBuffer", 1, functionProto)));
        obj262.Set("evalScript", JSValue.FromObject(new JSFunction(EvalScript, "evalScript", 1, functionProto)));
        obj262.Set("codePointRange", JSValue.FromObject(new JSFunction(CodePointRange, "codePointRange", 2, functionProto)));
        obj262.Set("global", JSValue.FromObject(_context.GlobalObject));
        obj262.Set("createRealm", JSValue.FromObject(new JSFunction(CreateRealm, "createRealm", 0, functionProto)));
        obj262.Set("IsHTMLDDA", JSValue.FromObject(new JSFunction(IsHTMLDDA, "IsHTMLDDA", 0, functionProto)));
        obj262.Set("gc", JSValue.FromObject(new JSFunction(GC, "gc", 0, functionProto)));

        _context.SetGlobalProperty("$262", JSValue.FromObject(obj262));
    }

    public void RunPendingJobs()
    {
        const int maxIterations = 1000;
        for (int i = 0; i < maxIterations; i++)
        {
            _context.RunMicrotasks();
            var didWork = _context.EventLoop.RunOnce();
            if (!didWork && !_context.EventLoop.HasPendingJobs)
            {
                break;
            }
        }
    }

    private JSValue Print(JSValue thisVal, JSValue[] args)
    {
        if (args.Length > 0)
        {
            var message = JSValueConversion.ToString(args[0]);
            if (string.Equals(message, "Test262:AsyncTestComplete", StringComparison.Ordinal))
            {
                AsyncDone++;
            }
            else if (message.StartsWith("Test262:AsyncTestFailure", StringComparison.Ordinal))
            {
                AsyncDone = 2;
            }
        }
        return JSValue.Undefined;
    }

    private JSValue DetachArrayBuffer(JSValue thisVal, JSValue[] args)
    {
        if (args.Length == 0 || !args[0].IsObject || args[0].AsObject() is not JSArrayBuffer buffer)
        {
            return JSValue.Undefined;
        }

        try
        {
            buffer.Detach();
        }
        catch (InvalidOperationException ex)
        {
            return _context.ThrowTypeError(ex.Message);
        }

        return JSValue.Undefined;
    }

    private JSValue EvalScript(JSValue thisVal, JSValue[] args)
    {
        var source = args.Length > 0 ? JSValueConversion.ToString(args[0]) : string.Empty;
        return _context.Evaluate(source, "<evalScript>");
    }

    private JSValue CodePointRange(JSValue thisVal, JSValue[] args)
    {
        if (args.Length < 2)
        {
            return JSValue.FromString(string.Empty);
        }

        var start = JSValueConversion.ToUInt32(args[0]);
        var end = JSValueConversion.ToUInt32(args[1]);
        end = Math.Min(end, 0x10ffff + 1u);
        if (start > end)
        {
            start = end;
        }

        var sb = new StringBuilder();
        for (uint cp = start; cp < end; cp++)
        {
            sb.Append(char.ConvertFromUtf32((int)cp));
        }

        return JSValue.FromString(sb.ToString());
    }

    private JSValue CreateRealm(JSValue thisVal, JSValue[] args)
    {
        var realmContext = _runtime.CreateContext();
        _realms.Add(realmContext);

        var realmState = new Test262ContextState(_runtime, realmContext, Paths);
        realmState.InstallHelpers();

        var realm262 = realmContext.GetGlobalProperty("$262");
        return realm262.IsObject ? realm262 : JSValue.Undefined;
    }

    private JSValue IsHTMLDDA(JSValue thisVal, JSValue[] args)
    {
        return JSValue.Null;
    }

    private JSValue GC(JSValue thisVal, JSValue[] args)
    {
        _runtime.RunGC();
        return JSValue.Undefined;
    }
}

internal sealed class Test262ErrorInfo
{
    public string ErrorClass { get; }
    public string Message { get; }
    public int Line { get; }

    public Test262ErrorInfo(string errorClass, string message, int line)
    {
        ErrorClass = errorClass;
        Message = message;
        Line = line;
    }

    public static Test262ErrorInfo FromContext(JSContext context, string filename)
    {
        var exception = context.GetAndClearException();
        if (!exception.IsObject)
        {
            var msg = JSValueConversion.ToString(exception);
            return new Test262ErrorInfo("Error", msg, 1);
        }

        var exObj = exception.AsObject();
        var nameVal = exObj.Get("name");
        var messageVal = exObj.Get("message");
        var name = JSValueConversion.ToString(nameVal);
        var message = JSValueConversion.ToString(messageVal);
        var fullMessage = string.IsNullOrEmpty(message) ? name : $"{name}: {message}";
        var line = ExtractLine(exObj, filename);

        return new Test262ErrorInfo(name, fullMessage, line);
    }

    private static int ExtractLine(JSObject exObj, string filename)
    {
        var stackVal = exObj.Get("stack");
        var stack = JSValueConversion.ToString(stackVal);
        if (string.IsNullOrEmpty(stack))
        {
            return 1;
        }

        var marker = filename + ":";
        var index = stack.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return 1;
        }

        index += marker.Length;
        var end = index;
        while (end < stack.Length && char.IsDigit(stack[end]))
        {
            end++;
        }

        if (end > index && int.TryParse(stack.Substring(index, end - index), NumberStyles.Integer, CultureInfo.InvariantCulture, out var line))
        {
            return line;
        }

        return 1;
    }
}

internal sealed class Test262ErrorCatalog
{
    private readonly Dictionary<(string, bool), Test262ErrorEntry> _entries;

    private Test262ErrorCatalog(Dictionary<(string, bool), Test262ErrorEntry> entries)
    {
        _entries = entries;
    }

    public static Test262ErrorCatalog Load(string path, Test262Paths paths)
    {
        var entries = new Dictionary<(string, bool), Test262ErrorEntry>();
        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex < 0)
            {
                continue;
            }

            var filePart = trimmed.Substring(0, colonIndex);
            var rest = trimmed.Substring(colonIndex + 1);
            var lineIndex = rest.IndexOf(':');
            if (lineIndex < 0)
            {
                continue;
            }

            if (!int.TryParse(rest.Substring(0, lineIndex), NumberStyles.Integer, CultureInfo.InvariantCulture, out var lineNumber))
            {
                continue;
            }

            var message = rest.Substring(lineIndex + 1).TrimStart();
            var strict = false;
            if (message.StartsWith("strict mode: ", StringComparison.Ordinal))
            {
                strict = true;
                message = message.Substring("strict mode: ".Length);
            }

            if (message.StartsWith("unexpected error: ", StringComparison.Ordinal))
            {
                message = message.Substring("unexpected error: ".Length);
            }

            var displayPath = Test262Paths.NormalizeDisplayPath(filePart);
            entries[(displayPath, strict)] = new Test262ErrorEntry(lineNumber, message);
        }

        return new Test262ErrorCatalog(entries);
    }

    public Test262ErrorEntry? Find(string displayPath, bool isStrict)
    {
        var key = (Test262Paths.NormalizeDisplayPath(displayPath), isStrict);
        return _entries.TryGetValue(key, out var entry) ? entry : null;
    }
}

internal sealed record Test262ErrorEntry(int Line, string Message);

internal sealed class Test262RunOutcome
{
    public string DisplayPath { get; }
    public int Runs { get; private set; }
    public int Failures { get; private set; }
    public int ExpectedFailures { get; private set; }
    public int NewErrors { get; private set; }
    public int ChangedErrors { get; private set; }
    public int FixedErrors { get; private set; }
    public bool WasSkipped { get; private set; }

    private Test262RunOutcome(string displayPath)
    {
        DisplayPath = displayPath;
    }

    public Test262RunOutcome(string displayPath, bool skipped)
    {
        DisplayPath = displayPath;
        WasSkipped = skipped;
    }

    public static Test262RunOutcome Skipped(string displayPath) => new(displayPath, true);

    public static Test262RunOutcome Passed(string displayPath)
    {
        return new Test262RunOutcome(displayPath)
        {
            Runs = 1,
        };
    }

    public static Test262RunOutcome ExpectedFailure(string displayPath, string message)
    {
        return new Test262RunOutcome(displayPath)
        {
            Runs = 1,
            Failures = 1,
            ExpectedFailures = 1,
        };
    }

    public static Test262RunOutcome NewError(string displayPath, string message)
    {
        return new Test262RunOutcome(displayPath)
        {
            Runs = 1,
            Failures = 1,
            NewErrors = 1,
        };
    }

    public static Test262RunOutcome Changed(string displayPath, string message, string expected)
    {
        return new Test262RunOutcome(displayPath)
        {
            Runs = 1,
            Failures = 1,
            ChangedErrors = 1,
        };
    }

    public static Test262RunOutcome Fixed(string displayPath, string message)
    {
        return new Test262RunOutcome(displayPath)
        {
            Runs = 1,
            FixedErrors = 1,
        };
    }

    public void Merge(Test262RunOutcome other)
    {
        Runs += other.Runs;
        Failures += other.Failures;
        ExpectedFailures += other.ExpectedFailures;
        NewErrors += other.NewErrors;
        ChangedErrors += other.ChangedErrors;
        FixedErrors += other.FixedErrors;
    }
}

internal sealed class Test262RunResult
{
    public int Runs { get; private set; }
    public int Failures { get; private set; }
    public int Skipped { get; set; }
    public int Excluded { get; set; }
    public int ExpectedFailures { get; private set; }
    public int NewErrors { get; private set; }
    public int ChangedErrors { get; private set; }
    public int FixedErrors { get; private set; }
    public string Summary { get; private set; } = string.Empty;

    public void Add(Test262RunOutcome outcome)
    {
        if (outcome.WasSkipped)
        {
            Skipped++;
            return;
        }

        Runs += outcome.Runs;
        Failures += outcome.Failures;
        ExpectedFailures += outcome.ExpectedFailures;
        NewErrors += outcome.NewErrors;
        ChangedErrors += outcome.ChangedErrors;
        FixedErrors += outcome.FixedErrors;
    }

    public void FinishSummary()
    {
        var sb = new StringBuilder();
        sb.Append("Result: ");
        sb.Append(Failures);
        sb.Append('/');
        sb.Append(Runs);
        sb.Append(Failures == 1 ? " error" : " errors");

        if (Excluded > 0)
        {
            sb.Append(", ");
            sb.Append(Excluded);
            sb.Append(" excluded");
        }

        if (Skipped > 0)
        {
            sb.Append(", ");
            sb.Append(Skipped);
            sb.Append(" skipped");
        }

        if (NewErrors > 0)
        {
            sb.Append(", ");
            sb.Append(NewErrors);
            sb.Append(" new");
        }

        if (ChangedErrors > 0)
        {
            sb.Append(", ");
            sb.Append(ChangedErrors);
            sb.Append(" changed");
        }

        if (FixedErrors > 0)
        {
            sb.Append(", ");
            sb.Append(FixedErrors);
            sb.Append(" fixed");
        }

        Summary = sb.ToString();
    }
}

internal sealed class Test262Paths
{
    public string BaseDir { get; }
    public string? Root { get; }
    public string DisplayRoot { get; } = "test262";

    public Test262Paths(string baseDir, string? root)
    {
        BaseDir = Path.GetFullPath(baseDir);
        Root = string.IsNullOrEmpty(root) ? null : Path.GetFullPath(root);
    }

    public string ToFullPath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        var normalized = NormalizeDisplayPath(path);
        if (Root != null)
        {
            var rootPrefix = DisplayRoot + "/";
            if (normalized.StartsWith(rootPrefix, StringComparison.Ordinal))
            {
                var relative = normalized.Substring(rootPrefix.Length);
                return Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
            }
        }

        return Path.Combine(BaseDir, normalized.Replace('/', Path.DirectorySeparatorChar));
    }

    public string ToDisplayPath(string fullPath)
    {
        var normalizedFull = Path.GetFullPath(fullPath);
        if (Root != null && normalizedFull.StartsWith(Root, StringComparison.Ordinal))
        {
            var relative = normalizedFull.Substring(Root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return DisplayRoot + "/" + relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }

        var relativeToBase = Path.GetRelativePath(BaseDir, normalizedFull);
        return NormalizeDisplayPath(relativeToBase);
    }

    public static string NormalizeDisplayPath(string path)
    {
        return path.Replace('\\', '/');
    }

    public static string CombineDisplay(string baseDisplay, string relative, Test262Paths paths)
    {
        var baseFull = paths.ToFullPath(baseDisplay);
        var combined = Path.GetFullPath(Path.Combine(baseFull, relative));
        return paths.ToDisplayPath(combined);
    }

    public static int CompareNatural(string a, string b)
    {
        int ia = 0, ib = 0;
        while (ia < a.Length && ib < b.Length)
        {
            var ca = a[ia];
            var cb = b[ib];
            if (char.IsDigit(ca) && char.IsDigit(cb))
            {
                long na = 0;
                while (ia < a.Length && char.IsDigit(a[ia]))
                {
                    na = na * 10 + (a[ia] - '0');
                    ia++;
                }
                long nb = 0;
                while (ib < b.Length && char.IsDigit(b[ib]))
                {
                    nb = nb * 10 + (b[ib] - '0');
                    ib++;
                }
                var cmp = na.CompareTo(nb);
                if (cmp != 0)
                {
                    return cmp;
                }
                continue;
            }

            var diff = ca.CompareTo(cb);
            if (diff != 0)
            {
                return diff;
            }

            ia++;
            ib++;
        }

        return a.Length.CompareTo(b.Length);
    }
}
