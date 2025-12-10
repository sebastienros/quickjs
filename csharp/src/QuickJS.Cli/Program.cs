// Licensed under the MIT License.

using System;
using System.IO;
using QuickJS;

namespace QuickJS.Cli;

/// <summary>
/// QuickJS.NET command-line interface.
/// </summary>
/// <remarks>
/// Provides functionality similar to the original qjs command:
/// - Execute JavaScript files
/// - Interactive REPL mode
/// - Evaluate expressions from command line
/// </remarks>
public static class Program
{
    private const string Version = "0.1.0";
    private const string Banner = @"
QuickJS.NET - JavaScript Engine for .NET
Version {0}
Type '.help' for help, '.exit' to quit
";

    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        // Parse arguments
        var options = ParseArguments(args);

        if (options.ShowHelp)
        {
            ShowHelp();
            return 0;
        }

        if (options.ShowVersion)
        {
            Console.WriteLine($"QuickJS.NET version {Version}");
            return 0;
        }

        // Create runtime and context
        using var runtime = new JSRuntime();
        var context = runtime.CreateContext();

        // Set up console output handler
        context.ConsoleOutput += (_, e) =>
        {
            var output = e.Level == ConsoleLogLevel.Error || e.Level == ConsoleLogLevel.Warn
                ? Console.Error
                : Console.Out;
            output.WriteLine(e.Message);
        };

        // Execute based on options
        if (!string.IsNullOrEmpty(options.Expression))
        {
            // -e "expression" mode
            return ExecuteExpression(context, options.Expression);
        }

        if (!string.IsNullOrEmpty(options.FilePath))
        {
            // File execution mode
            return ExecuteFile(context, options.FilePath, options.ScriptArgs);
        }

        // Interactive REPL mode
        return RunRepl(context, options.Interactive);
    }

    private static int ExecuteExpression(JSContext context, string expression)
    {
        try
        {
            var result = context.Evaluate(expression, "<cmdline>");
            if (result.IsException)
            {
                PrintException(context);
                return 1;
            }

            // Print result unless undefined
            if (!result.IsUndefined)
            {
                Console.WriteLine(FormatValue(result));
            }
            return 0;
        }
        catch (JSException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int ExecuteFile(JSContext context, string filePath, string[] scriptArgs)
    {
        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Error: File not found: {filePath}");
            return 1;
        }

        try
        {
            // Set up scriptArgs in global scope
            SetScriptArgs(context, filePath, scriptArgs);

            var source = File.ReadAllText(filePath);
            var result = context.Evaluate(source, filePath);

            if (result.IsException)
            {
                PrintException(context);
                return 1;
            }

            return 0;
        }
        catch (JSException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Error reading file: {ex.Message}");
            return 1;
        }
    }

    private static int RunRepl(JSContext context, bool showBanner)
    {
        if (showBanner)
        {
            Console.WriteLine(string.Format(Banner, Version));
        }

        var repl = new Repl(context);
        return repl.Run();
    }

    private static void SetScriptArgs(JSContext context, string filePath, string[] args)
    {
        // Create scriptArgs array with [scriptPath, ...args]
        var arrayProto = context.GetClassPrototype(JSClassId.Array);
        var argsArray = new JSObject(arrayProto, JSClassId.Array);

        argsArray.Set(0u, JSValue.FromString(Path.GetFullPath(filePath)));
        for (uint i = 0; i < args.Length; i++)
        {
            argsArray.Set(i + 1, JSValue.FromString(args[i]));
        }

        context.SetGlobalProperty("scriptArgs", JSValue.FromObject(argsArray));
    }

    private static void PrintException(JSContext context)
    {
        var ex = context.CurrentException;
        if (!ex.IsUndefined)
        {
            Console.Error.WriteLine(ex.ToString());
        }
        else
        {
            Console.Error.WriteLine("Unknown error");
        }
    }

    private static string FormatValue(JSValue value)
    {
        if (value.IsString)
        {
            // Show strings with quotes in REPL
            return $"\"{value}\"";
        }
        return value.ToString() ?? "undefined";
    }

    private static void ShowHelp()
    {
        Console.WriteLine(@"Usage: qjs-net [options] [file] [args...]

Options:
  -h, --help          Show this help message
  -v, --version       Show version information
  -e, --eval <expr>   Evaluate expression
  -i, --interactive   Force interactive mode after file execution
  
Examples:
  qjs-net                     Start interactive REPL
  qjs-net script.js           Execute a JavaScript file
  qjs-net -e ""1 + 2""          Evaluate an expression
  qjs-net script.js arg1 arg2 Execute with arguments (available as scriptArgs)

REPL Commands:
  .help               Show REPL help
  .exit               Exit the REPL
  .clear              Clear the screen
  .load <file>        Load and execute a file

For more information, visit: https://github.com/nicohund/quickjs
");
    }

    private static CliOptions ParseArguments(string[] args)
    {
        var options = new CliOptions();
        var i = 0;

        while (i < args.Length)
        {
            var arg = args[i];

            switch (arg)
            {
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    return options;

                case "-v":
                case "--version":
                    options.ShowVersion = true;
                    return options;

                case "-e":
                case "--eval":
                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException("-e requires an expression argument");
                    }
                    options.Expression = args[++i];
                    break;

                case "-i":
                case "--interactive":
                    options.Interactive = true;
                    break;

                default:
                    if (arg.StartsWith("-"))
                    {
                        throw new ArgumentException($"Unknown option: {arg}");
                    }

                    // First non-option argument is the file
                    options.FilePath = arg;

                    // Remaining arguments are script args
                    var scriptArgs = new string[args.Length - i - 1];
                    Array.Copy(args, i + 1, scriptArgs, 0, scriptArgs.Length);
                    options.ScriptArgs = scriptArgs;
                    return options;
            }

            i++;
        }

        // No file specified - interactive mode
        options.Interactive = true;
        return options;
    }

    private class CliOptions
    {
        public bool ShowHelp { get; set; }
        public bool ShowVersion { get; set; }
        public string? Expression { get; set; }
        public string? FilePath { get; set; }
        public string[] ScriptArgs { get; set; } = Array.Empty<string>();
        public bool Interactive { get; set; }
    }
}
