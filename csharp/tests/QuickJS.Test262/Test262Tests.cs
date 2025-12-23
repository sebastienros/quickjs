// Licensed under the MIT License.

using System.Globalization;
using System.IO.Compression;
using Xunit;

namespace QuickJS.Test262;

public class Test262Tests
{
    public static IEnumerable<object[]> TestCases => Test262XunitState.Instance.TestCaseIds
        .Select(testId =>
        {
            var folder = Test262XunitState.Instance.GetFolderTrait(testId);
            var displayName = Test262XunitState.Instance.GetDisplayName(testId);
            return new object[] { new TheoryDataRow<string>(testId).WithTrait("Folder", folder).WithTestDisplayName(displayName) };
        });

    [Theory]
    [MemberData(nameof(TestCases))]
    public void Run(string testId)
    {
        var testCase = Test262XunitState.Instance.GetTestCase(testId);
        Test262XunitState.Instance.Execute(testCase);
    }
}

public sealed record Test262TestCase(
    string FullPath,
    string DisplayPath,
    string Source,
    Test262Metadata Metadata,
    bool Strict)
{
    public override string ToString()
    {
        var mode = Metadata.IsModule ? "module" : Strict ? "strict" : "nostrict";
        return $"{DisplayPath} ({mode})";
    }
}

internal sealed class Test262XunitState
{
    private static readonly Lazy<Test262XunitState> s_instance = new(() => new Test262XunitState());

    public static Test262XunitState Instance => s_instance.Value;

    public IReadOnlyList<Test262TestCase> TestCases { get; }
    public IReadOnlyList<string> TestCaseIds { get; }

    private readonly Test262Config _config;
    private readonly Test262Paths _paths;
    private readonly Test262ExcludeFilter _excludeFilter;
    private readonly Dictionary<string, string> _harnessSources;
    private readonly string? _filter;
    private readonly string _test262Root;
    private readonly HashSet<string> _runList;
    private readonly HashSet<string> _ignoreList;
    private readonly string _runListPath;
    private readonly string _ignoreListPath;
    private readonly string _newListPath;
    private readonly bool _updateNewList;
    private readonly Dictionary<string, Test262TestCase> _testCaseById;

    private Test262XunitState()
    {
        var repoRoot = FindRepoRoot() ?? throw new InvalidOperationException("Unable to locate repository root for test262.conf.");
        var configPath = Environment.GetEnvironmentVariable("TEST262_CONFIG");
        if (string.IsNullOrEmpty(configPath))
        {
            configPath = Path.Combine(repoRoot, "test262.conf");
        }

        if (!File.Exists(configPath))
        {
            throw new InvalidOperationException($"test262 config not found at '{configPath}'.");
        }

        _config = Test262Config.Load(configPath);
        _filter = Environment.GetEnvironmentVariable("TEST262_FILTER");

        _test262Root = EnsureTest262Checkout(repoRoot);
        _paths = new Test262Paths(Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? repoRoot, _test262Root);
        _excludeFilter = Test262ExcludeFilter.Create(_config, _paths);
        _harnessSources = LoadHarnessSources(_test262Root);

        var listDir = Path.Combine(repoRoot, "csharp", "tests", "QuickJS.Test262");
        Directory.CreateDirectory(listDir);
        _runListPath = Path.Combine(listDir, "test262.run");
        _ignoreListPath = Path.Combine(listDir, "test262.ignore");
        _newListPath = Path.Combine(listDir, "test262.new");
        _runList = LoadList(_runListPath);
        _ignoreList = LoadList(_ignoreListPath);
        _updateNewList = Environment.GetCommandLineArgs()
            .Any(arg => string.Equals(arg, "--update-new", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEST262_LIST")))
        {
            _config.TestList.Clear();
            _config.TestDirs.Clear();
            _config.LoadTestList(Environment.GetEnvironmentVariable("TEST262_LIST")!);
        }

        TestCases = BuildTestCases();
        _testCaseById = new Dictionary<string, Test262TestCase>(StringComparer.Ordinal);
        foreach (var testCase in TestCases)
        {
            _testCaseById[GetTestId(testCase.DisplayPath, testCase.Strict, testCase.Metadata)] = testCase;
        }
        TestCaseIds = _testCaseById.Keys.OrderBy(id => id, Comparer<string>.Create(CompareTestId)).ToList();
    }

    public void Execute(Test262TestCase testCase)
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();
        runtime.RuntimeInfo = testCase.DisplayPath;

        var helper = new Test262QuickJsContext(runtime, context);
        ConfigureModuleLoader(context);

        if (!testCase.Metadata.IsRaw)
        {
            helper.InstallHelpers();
            EvaluateHarness(context, "sta.js");
            EvaluateHarness(context, "assert.js");

            foreach (var include in testCase.Metadata.Includes)
            {
                EvaluateHarness(context, include);
            }

            if (testCase.Metadata.IsAsync)
            {
                EvaluateHarness(context, "doneprintHandle.js");
            }
        }

        var source = testCase.Source;
        if (testCase.Strict && !testCase.Metadata.IsModule && !testCase.Metadata.IsRaw)
        {
            source = "\"use strict\";\n" + source;
        }

        JSValue result = testCase.Metadata.IsModule
            ? context.EvaluateModule(source, testCase.DisplayPath)
            : context.Evaluate(source, testCase.DisplayPath);

        var errorInfo = GetErrorInfo(context, testCase.DisplayPath);
        if ((testCase.Metadata.IsModule || testCase.Metadata.IsAsync) && errorInfo == null)
        {
            helper.RunPendingJobs();
            errorInfo = GetErrorInfo(context, testCase.DisplayPath);
        }

        if (testCase.Metadata.IsAsync && errorInfo == null && helper.AsyncDone != 1)
        {
            errorInfo = new Test262ExceptionInfo(
                "TypeError",
                helper.AsyncDone == 2 ? "Test262:AsyncTestFailure" : "$DONE() not called",
                1);
        }

        ValidateExpected(testCase, errorInfo);

        if (result.IsException)
        {
            Assert.Fail(FormatFailure(testCase, errorInfo?.Message ?? "Unexpected exception."));
        }
    }

    public Test262TestCase GetTestCase(string testId)
    {
        if (_testCaseById.TryGetValue(testId, out var testCase))
        {
            return testCase;
        }

        throw new InvalidOperationException($"Test case not found: {testId}");
    }

    public string GetFolderTrait(string testId)
    {
        if (!TryParseRunListEntry(testId, out var displayPath, out _))
        {
            return string.Empty;
        }

        const string testPrefix = "test262/test/";
        var normalized = Test262Paths.NormalizeDisplayPath(displayPath);
        if (normalized.StartsWith(testPrefix, StringComparison.Ordinal))
        {
            normalized = normalized.Substring(testPrefix.Length);
        }

        const string arrayPrefix = "annexB/built-ins/Array/";
        if (normalized.StartsWith(arrayPrefix, StringComparison.Ordinal))
        {
            return arrayPrefix;
        }

        var lastSlash = normalized.LastIndexOf('/');
        if (lastSlash >= 0)
        {
            return normalized.Substring(0, lastSlash + 1);
        }

        return normalized;
    }

    public string GetDisplayName(string testId)
    {
        if (!TryParseRunListEntry(testId, out var displayPath, out var strict))
        {
            return testId;
        }

        const string testPrefix = "test262/test/";
        var normalized = Test262Paths.NormalizeDisplayPath(displayPath);
        if (normalized.StartsWith(testPrefix, StringComparison.Ordinal))
        {
            normalized = normalized.Substring(testPrefix.Length);
        }

        var folder = GetFolderTrait(testId);
        var fileName = normalized;
        if (!string.IsNullOrEmpty(folder) && fileName.StartsWith(folder, StringComparison.Ordinal))
        {
            fileName = fileName.Substring(folder.Length);
        }

        var suffix = strict ? " (strict)" : string.Empty;
        return string.IsNullOrEmpty(folder) ? fileName + suffix : folder + fileName + suffix;
    }

    private IReadOnlyList<Test262TestCase> BuildTestCases()
    {
        if (!_updateNewList && _runList.Count > 0)
        {
            return BuildTestCasesFromRunList();
        }

        var tests = new List<Test262TestCase>();
        HashSet<string>? discoveredNew = null;
        if (_updateNewList)
        {
            discoveredNew = new HashSet<string>(StringComparer.Ordinal);
        }

        foreach (var dir in _config.TestDirs)
        {
            var fullDir = _paths.ToFullPath(dir);
            if (!Directory.Exists(fullDir))
            {
                continue;
            }

            foreach (var filePath in Directory.EnumerateFiles(fullDir, "*.js", SearchOption.AllDirectories))
            {
                if (filePath.EndsWith("_FIXTURE.js", StringComparison.Ordinal))
                {
                    continue;
                }

                AddTestCases(tests, filePath, discoveredNew);
            }
        }

        foreach (var testPath in _config.TestList)
        {
            var fullPath = _paths.ToFullPath(testPath);
            if (File.Exists(fullPath))
            {
                AddTestCases(tests, fullPath, discoveredNew);
            }
        }

        tests.Sort((left, right) => Test262Paths.CompareNatural(left.DisplayPath, right.DisplayPath));
        if (_updateNewList && discoveredNew != null)
        {
            UpdateNewList(discoveredNew);
        }
        return tests;
    }

    private IReadOnlyList<Test262TestCase> BuildTestCasesFromRunList()
    {
        var tests = new List<Test262TestCase>();
        var entries = _runList.ToList();
        entries.Sort(CompareTestId);

        foreach (var entry in entries)
        {
            if (_ignoreList.Contains(entry))
            {
                continue;
            }

            if (!TryParseRunListEntry(entry, out var displayPath, out var strict))
            {
                continue;
            }

            if (_filter != null && displayPath.IndexOf(_filter, StringComparison.Ordinal) < 0)
            {
                continue;
            }

            var fullPath = _paths.ToFullPath(displayPath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var source = File.ReadAllText(fullPath);
            var metadata = Test262Metadata.Parse(source, _config.NewStyle);
            if (!_config.NewStyle)
            {
                foreach (var include in Test262Metadata.ParseOldStyleIncludes(source))
                {
                    metadata.Includes.Add(include);
                }
            }

            if (IsSkippedCommon(displayPath, metadata))
            {
                continue;
            }

            var effectiveStrict = metadata.IsModule ? false : strict;
            if (!ShouldRunForMode(metadata, effectiveStrict))
            {
                continue;
            }

            tests.Add(new Test262TestCase(fullPath, displayPath, source, metadata, effectiveStrict));
        }

        return tests;
    }

    private static bool TryParseRunListEntry(string entry, out string displayPath, out bool strict)
    {
        const string strictSuffix = " (strict)";
        strict = false;
        displayPath = entry;
        if (entry.EndsWith(strictSuffix, StringComparison.Ordinal))
        {
            strict = true;
            displayPath = entry.Substring(0, entry.Length - strictSuffix.Length);
        }

        displayPath = Test262Paths.NormalizeDisplayPath(displayPath);
        return displayPath.Length > 0;
    }

    private void AddTestCases(List<Test262TestCase> tests, string fullPath, HashSet<string>? discoveredNew)
    {
        var displayPath = _paths.ToDisplayPath(fullPath);
        if (_filter != null && displayPath.IndexOf(_filter, StringComparison.Ordinal) < 0)
        {
            return;
        }

        var source = File.ReadAllText(fullPath);
        var metadata = Test262Metadata.Parse(source, _config.NewStyle);
        if (!_config.NewStyle)
        {
            foreach (var include in Test262Metadata.ParseOldStyleIncludes(source))
            {
                metadata.Includes.Add(include);
            }
        }

        if (IsSkippedCommon(displayPath, metadata))
        {
            return;
        }

        if (metadata.IsModule)
        {
            if (ShouldRunTest(GetTestId(displayPath, strict: false, metadata), discoveredNew))
            {
                tests.Add(new Test262TestCase(fullPath, displayPath, source, metadata, Strict: false));
            }
            return;
        }

        if (ShouldRunForMode(metadata, strict: false))
        {
            if (ShouldRunTest(GetTestId(displayPath, strict: false, metadata), discoveredNew))
            {
                tests.Add(new Test262TestCase(fullPath, displayPath, source, metadata, Strict: false));
            }
        }

        if (ShouldRunForMode(metadata, strict: true))
        {
            if (ShouldRunTest(GetTestId(displayPath, strict: true, metadata), discoveredNew))
            {
                tests.Add(new Test262TestCase(fullPath, displayPath, source, metadata, Strict: true));
            }
        }
    }

    private bool IsSkippedCommon(string displayPath, Test262Metadata metadata)
    {
        if (_excludeFilter.IsExcluded(displayPath))
        {
            return true;
        }

        if (metadata.IsModule && _config.SkipModule)
        {
            return true;
        }

        if (metadata.IsAsync && _config.SkipAsync)
        {
            return true;
        }

        if (!metadata.IsRaw)
        {
            foreach (var include in metadata.Includes)
            {
                if (_config.HarnessExclude.Contains(include))
                {
                    return true;
                }
            }
        }

        foreach (var feature in metadata.Features)
        {
            if (_config.Features.Contains(feature))
            {
                continue;
            }

            if (_config.SkipFeatures.Contains(feature))
            {
                return true;
            }

            return true;
        }

        return false;
    }

    private bool ShouldRunForMode(Test262Metadata metadata, bool strict)
    {
        if (metadata.IsModule)
        {
            return !strict;
        }

        bool isNoStrict = metadata.IsNoStrict || metadata.IsRaw;
        bool isOnlyStrict = metadata.IsOnlyStrict;

        bool useStrict = false;
        bool useNoStrict = false;

        switch (_config.Mode)
        {
            case Test262Mode.DefaultNoStrict:
                if (isOnlyStrict)
                {
                    useStrict = true;
                }
                else
                {
                    useNoStrict = true;
                }
                break;
            case Test262Mode.DefaultStrict:
                if (isNoStrict)
                {
                    useNoStrict = true;
                }
                else
                {
                    useStrict = true;
                }
                break;
            case Test262Mode.NoStrict:
                if (!isOnlyStrict)
                {
                    useNoStrict = true;
                }
                break;
            case Test262Mode.Strict:
                if (!isNoStrict)
                {
                    useStrict = true;
                }
                break;
            case Test262Mode.All:
                if (!isNoStrict)
                {
                    useStrict = true;
                }
                if (!isOnlyStrict)
                {
                    useNoStrict = true;
                }
                break;
        }

        return strict ? useStrict : useNoStrict;
    }

    private void EvaluateHarness(JSContext context, string include)
    {
        if (!_harnessSources.TryGetValue(include, out var source))
        {
            throw new InvalidOperationException($"Harness file not found: {include}");
        }

        var result = context.Evaluate(source, $"{_paths.DisplayRoot}/harness/{include}");
        if (result.IsException || context.HasException)
        {
            var errorInfo = GetErrorInfo(context, $"{_paths.DisplayRoot}/harness/{include}");
            var message = errorInfo?.Message ?? "Harness evaluation failed.";
            Assert.Fail(message);
        }
    }

    private void ConfigureModuleLoader(JSContext context)
    {
        var loader = context.ModuleLoader;
        loader.Resolver = (baseModuleName, moduleName) => ResolveModuleName(baseModuleName, moduleName);
        loader.Loader = modulePath =>
        {
            var fullPath = _paths.ToFullPath(modulePath);
            return File.Exists(fullPath) ? File.ReadAllText(fullPath) : null;
        };
    }

    private string ResolveModuleName(string? baseModuleName, string moduleName)
    {
        var baseDisplayPath = string.IsNullOrEmpty(baseModuleName)
            ? _paths.DisplayRoot
            : baseModuleName.Replace('\\', '/');

        var baseFull = _paths.ToFullPath(baseDisplayPath);
        var baseDir = Path.GetDirectoryName(baseFull) ?? baseFull;

        string resolvedDisplay;
        if (moduleName.StartsWith("./", StringComparison.Ordinal) || moduleName.StartsWith("../", StringComparison.Ordinal))
        {
            var full = Path.GetFullPath(Path.Combine(baseDir, moduleName));
            resolvedDisplay = _paths.ToDisplayPath(full);
        }
        else if (!moduleName.Contains("/", StringComparison.Ordinal))
        {
            var full = Path.GetFullPath(Path.Combine(baseDir, moduleName));
            resolvedDisplay = _paths.ToDisplayPath(full);
        }
        else
        {
            resolvedDisplay = moduleName.Replace('\\', '/');
        }

        if (!resolvedDisplay.EndsWith(".js", StringComparison.Ordinal))
        {
            resolvedDisplay += ".js";
        }

        return resolvedDisplay;
    }

    private static void ValidateExpected(Test262TestCase testCase, Test262ExceptionInfo? errorInfo)
    {
        var metadata = testCase.Metadata;
        if (!metadata.IsNegative)
        {
            if (errorInfo != null)
            {
                Assert.Fail(FormatFailure(testCase, errorInfo.Message));
            }
            return;
        }

        if (errorInfo == null)
        {
            Assert.Fail(FormatFailure(testCase, "Expected error to be thrown, but no error was thrown."));
            return;
        }

        if (!string.IsNullOrEmpty(metadata.NegativeType) &&
            !string.Equals(errorInfo.ErrorClass, metadata.NegativeType, StringComparison.Ordinal))
        {
            var message = $"Expected {metadata.NegativeType}, got {errorInfo.ErrorClass}.";
            Assert.Fail(FormatFailure(testCase, message));
        }
    }

    private static Test262ExceptionInfo? GetErrorInfo(JSContext context, string displayPath)
    {
        if (!context.HasException)
        {
            return null;
        }

        var exception = context.GetAndClearException();
        if (!exception.IsObject)
        {
            var msg = JSValueConversion.ToString(exception);
            return new Test262ExceptionInfo("Error", msg, 1);
        }

        var exObj = exception.AsObject();
        var nameVal = exObj.Get("name");
        var messageVal = exObj.Get("message");
        var name = JSValueConversion.ToString(nameVal);
        var message = JSValueConversion.ToString(messageVal);
        var fullMessage = string.IsNullOrEmpty(message) ? name : $"{name}: {message}";
        var line = ExtractLine(exObj, displayPath);

        return new Test262ExceptionInfo(name, fullMessage, line);
    }

    private static int ExtractLine(JSObject exObj, string displayPath)
    {
        var stackVal = exObj.Get("stack");
        var stack = JSValueConversion.ToString(stackVal);
        if (string.IsNullOrEmpty(stack))
        {
            return 1;
        }

        var marker = displayPath + ":";
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

    private static string FormatFailure(Test262TestCase testCase, string message)
    {
        var code = string.Join(Environment.NewLine, testCase.Source.Split('\n')
            .Select((line, index) => $"{index + 1:00}: {line}"));
        return $"{Environment.NewLine}{testCase.DisplayPath}{Environment.NewLine}{Environment.NewLine}{message}{Environment.NewLine}{code}";
    }

    private bool ShouldRunTest(string testId, HashSet<string>? discoveredNew)
    {
        if (_ignoreList.Contains(testId))
        {
            return false;
        }

        if (_runList.Contains(testId))
        {
            return true;
        }

        discoveredNew?.Add(testId);
        return false;
    }

    private static string GetTestId(string displayPath, bool strict, Test262Metadata metadata)
    {
        if (metadata.IsModule)
        {
            return displayPath;
        }

        return strict ? $"{displayPath} (strict)" : displayPath;
    }

    private void UpdateNewList(HashSet<string> discoveredNew)
    {
        var existingNew = LoadList(_newListPath);
        var combined = new HashSet<string>(existingNew, StringComparer.Ordinal);
        foreach (var entry in discoveredNew)
        {
            combined.Add(entry);
        }

        combined.RemoveWhere(entry => _runList.Contains(entry) || _ignoreList.Contains(entry));

        var sorted = combined.ToList();
        sorted.Sort(CompareTestId);

        var content = string.Join(Environment.NewLine, sorted);
        if (sorted.Count > 0)
        {
            content += Environment.NewLine;
        }

        if (!File.Exists(_newListPath) || File.ReadAllText(_newListPath) != content)
        {
            File.WriteAllText(_newListPath, content);
        }
    }

    private static HashSet<string> LoadList(string path)
    {
        var entries = new HashSet<string>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return entries;
        }

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = StripComment(rawLine).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            entries.Add(line);
        }

        return entries;
    }

    private static string StripComment(string line)
    {
        var index = line.IndexOf('#');
        return index >= 0 ? line.Substring(0, index) : line;
    }

    private static int CompareTestId(string left, string right)
    {
        var leftStrict = left.EndsWith(" (strict)", StringComparison.Ordinal);
        var rightStrict = right.EndsWith(" (strict)", StringComparison.Ordinal);

        var leftBase = leftStrict ? left.Substring(0, left.Length - " (strict)".Length) : left;
        var rightBase = rightStrict ? right.Substring(0, right.Length - " (strict)".Length) : right;

        var cmp = Test262Paths.CompareNatural(leftBase, rightBase);
        if (cmp != 0)
        {
            return cmp;
        }

        if (leftStrict == rightStrict)
        {
            return 0;
        }

        return leftStrict ? 1 : -1;
    }

    private static Dictionary<string, string> LoadHarnessSources(string test262Root)
    {
        var harnessDir = Path.Combine(test262Root, "harness");
        if (!Directory.Exists(harnessDir))
        {
            throw new InvalidOperationException("test262 harness directory not found in cached test262 checkout.");
        }

        return Directory.EnumerateFiles(harnessDir, "*.js", SearchOption.TopDirectoryOnly)
            .Select(path => new { Name = Path.GetFileName(path), Source = File.ReadAllText(path) })
            .Where(entry => !string.IsNullOrEmpty(entry.Name))
            .ToDictionary(entry => entry.Name!, entry => entry.Source, StringComparer.Ordinal);
    }

    private static string EnsureTest262Checkout(string repoRoot)
    {
        var commitFile = Environment.GetEnvironmentVariable("TEST262_COMMIT_FILE");
        if (string.IsNullOrEmpty(commitFile))
        {
            commitFile = Path.Combine(repoRoot, "test262.commit");
        }

        if (!File.Exists(commitFile))
        {
            throw new InvalidOperationException($"test262 commit file not found at '{commitFile}'.");
        }

        var commitSha = ReadCommitSha(commitFile);
        if (string.IsNullOrEmpty(commitSha))
        {
            throw new InvalidOperationException($"test262 commit file '{commitFile}' does not contain a valid commit hash.");
        }

        var cacheRoot = Environment.GetEnvironmentVariable("TEST262_CACHE_DIR");
        if (string.IsNullOrEmpty(cacheRoot))
        {
            cacheRoot = Path.Combine(Path.GetTempPath(), "quickjs-test262-cache");
        }

        Directory.CreateDirectory(cacheRoot);

        var commitDirName = $"test262-{commitSha}";
        var commitDir = Path.Combine(cacheRoot, commitDirName);
        var testDir = Path.Combine(commitDir, "test");

        if (Directory.Exists(testDir))
        {
            return commitDir;
        }

        if (Directory.Exists(commitDir))
        {
            Directory.Delete(commitDir, true);
        }

        var zipPath = Path.Combine(cacheRoot, $"{commitDirName}.zip");
        if (!File.Exists(zipPath))
        {
            DownloadTest262Zip(commitSha, zipPath);
        }

        ZipFile.ExtractToDirectory(zipPath, cacheRoot, true);

        if (!Directory.Exists(testDir))
        {
            throw new InvalidOperationException($"test262 extraction failed. Expected '{testDir}'.");
        }

        return commitDir;
    }

    private static void DownloadTest262Zip(string commitSha, string zipPath)
    {
        var uri = $"https://github.com/tc39/test262/archive/{commitSha}.zip";
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("QuickJS.Test262");

        using var responseStream = httpClient.GetStreamAsync(uri).GetAwaiter().GetResult();
        var tempPath = zipPath + ".tmp";
        using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            responseStream.CopyTo(fileStream);
        }

        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        File.Move(tempPath, zipPath);
    }

    private static string ReadCommitSha(string commitFile)
    {
        var content = File.ReadAllText(commitFile);
        var parts = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0].Trim() : string.Empty;
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

internal sealed class Test262ExceptionInfo
{
    public string ErrorClass { get; }
    public string Message { get; }
    public int Line { get; }

    public Test262ExceptionInfo(string errorClass, string message, int line)
    {
        ErrorClass = errorClass;
        Message = message;
        Line = line;
    }
}

internal sealed class Test262QuickJsContext
{
    private readonly JSRuntime _runtime;
    private readonly JSContext _context;
    private readonly List<JSContext> _realms = new();

    public int AsyncDone { get; private set; }

    public Test262QuickJsContext(JSRuntime runtime, JSContext context)
    {
        _runtime = runtime;
        _context = context;
    }

        public void InstallHelpers()
        {
            var functionProto = _context.GetClassPrototype(JSClassId.CFunction);
            _context.RegisterGlobalFunction("print", Print, 1);
    
            // Test262 harness expects a few host-provided globals (present in browsers/node),
            // and will crash during initialization if they are missing (e.g. `setTimeout.bind(...)`).
            var hostPolyfills = @"
    var g = globalThis;

      if (typeof g.setTimeout !== 'function') {
        g.setTimeout = function (cb /*, ms */) {
          if (typeof cb === 'function') cb();
          return 0;
        };
      }
    
      if (typeof g.clearTimeout !== 'function') {
        g.clearTimeout = function (/* id */) { };
      }
    
      if (typeof g.setInterval !== 'function') {
        g.setInterval = function (cb /*, ms */) {
          if (typeof cb === 'function') cb();
          return 0;
        };
      }
    
      if (typeof g.clearInterval !== 'function') {
        g.clearInterval = function (/* id */) { };
      }
    
      if (typeof g.queueMicrotask !== 'function') {
        g.queueMicrotask = function (cb) {
          if (typeof cb === 'function') cb();
        };
      }
    
      if (typeof g.printHandle !== 'function' && typeof g.print === 'function') {
        g.printHandle = g.print;
      }
    
    ";
            var polyfillResult = _context.Evaluate(hostPolyfills, "<test262-host>");
            if (polyfillResult.IsException || _context.HasException)
            {
                var ex = _context.GetAndClearException();
                throw new InvalidOperationException($"Failed to install test262 host polyfills: {FormatException(ex)}");
            }
    
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

    public void RunPendingJobs(int maxIterations = 1000)
    {
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

        var buffer = new System.Text.StringBuilder();
        for (uint cp = start; cp < end; cp++)
        {
            buffer.Append(char.ConvertFromUtf32((int)cp));
        }

        return JSValue.FromString(buffer.ToString());
    }

    private JSValue CreateRealm(JSValue thisVal, JSValue[] args)
    {
        var realmContext = _runtime.CreateContext();
        _realms.Add(realmContext);

        var realmHelper = new Test262QuickJsContext(_runtime, realmContext);
        realmHelper.InstallHelpers();

        var realm262 = realmContext.GetGlobalProperty("$262");
        return realm262.IsObject ? realm262 : JSValue.Undefined;
    }

    private static JSValue IsHTMLDDA(JSValue thisVal, JSValue[] args)
    {
        return JSValue.Null;
    }

    private JSValue GC(JSValue thisVal, JSValue[] args)
    {
        _runtime.RunGC();
        return JSValue.Undefined;
    }

    private static string FormatException(JSValue exception)
    {
        if (!exception.IsObject)
        {
            return JSValueConversion.ToString(exception);
        }

        var exObj = exception.AsObject();
        var name = JSValueConversion.ToString(exObj.Get("name"));
        var message = JSValueConversion.ToString(exObj.Get("message"));
        var fullMessage = string.IsNullOrEmpty(message) ? name : $"{name}: {message}";
        var stack = JSValueConversion.ToString(exObj.Get("stack"));
        if (!string.IsNullOrEmpty(stack))
        {
            fullMessage = $"{fullMessage}{Environment.NewLine}{stack}";
        }

        return fullMessage;
    }
}
