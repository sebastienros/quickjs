// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Text;
using QuickJS;

namespace QuickJS.Cli;

/// <summary>
/// Interactive Read-Eval-Print Loop for QuickJS.NET.
/// </summary>
public class Repl
{
    private readonly JSContext _context;
    private bool _running = true;
    private readonly StringBuilder _multiLineBuffer = new();
    private bool _inMultiLine;
    private int _openBraces;
    private int _openBrackets;
    private int _openParens;

    private const string Prompt = "qjs> ";
    private const string ContinuationPrompt = "...> ";

    public Repl(JSContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Runs the REPL loop until the user exits.
    /// </summary>
    /// <returns>Exit code (0 for normal exit).</returns>
    public int Run()
    {
        while (_running)
        {
            try
            {
                var prompt = _inMultiLine ? ContinuationPrompt : Prompt;
                Console.Write(prompt);

                var line = Console.ReadLine();
                if (line == null)
                {
                    // EOF (Ctrl+D)
                    Console.WriteLine();
                    break;
                }

                ProcessLine(line);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                ResetMultiLine();
            }
        }

        return 0;
    }

    private void ProcessLine(string line)
    {
        // Check for REPL commands (only at start of input, not in multi-line)
        if (!_inMultiLine && line.StartsWith("."))
        {
            ProcessCommand(line);
            return;
        }

        // Add to multi-line buffer
        if (_inMultiLine)
        {
            _multiLineBuffer.AppendLine(line);
        }
        else
        {
            _multiLineBuffer.Clear();
            _multiLineBuffer.Append(line);
        }

        // Update bracket tracking
        UpdateBracketCount(line);

        // Check if we have a complete statement
        if (IsCompleteStatement())
        {
            EvaluateBuffer();
            ResetMultiLine();
        }
        else
        {
            _inMultiLine = true;
        }
    }

    private void ProcessCommand(string line)
    {
        var parts = line.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();
        var arg = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        switch (command)
        {
            case ".help":
                ShowHelp();
                break;

            case ".exit":
            case ".quit":
                _running = false;
                break;

            case ".clear":
                Console.Clear();
                break;

            case ".load":
                LoadFile(arg);
                break;

            case ".reset":
                ResetMultiLine();
                Console.WriteLine("Input buffer cleared.");
                break;

            default:
                Console.WriteLine($"Unknown command: {command}");
                Console.WriteLine("Type '.help' for available commands.");
                break;
        }
    }

    private void ShowHelp()
    {
        Console.WriteLine(@"
REPL Commands:
  .help           Show this help message
  .exit, .quit    Exit the REPL
  .clear          Clear the screen
  .load <file>    Load and execute a JavaScript file
  .reset          Clear the current input buffer

Tips:
  - Multi-line input is supported (press Enter to continue)
  - Use Ctrl+C to cancel current input
  - Use Ctrl+D to exit
  - Results are automatically printed unless undefined
");
    }

    private void LoadFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            Console.WriteLine("Usage: .load <filename>");
            return;
        }

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File not found: {filePath}");
            return;
        }

        try
        {
            var source = File.ReadAllText(filePath);
            Console.WriteLine($"Loading {filePath}...");

            var result = _context.Evaluate(source, filePath);
            if (result.IsException)
            {
                PrintException();
            }
            else
            {
                Console.WriteLine("File loaded successfully.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading file: {ex.Message}");
        }
    }

    private void UpdateBracketCount(string line)
    {
        bool inString = false;
        bool inTemplate = false;
        char stringChar = '\0';
        bool escape = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (escape)
            {
                escape = false;
                continue;
            }

            if (c == '\\')
            {
                escape = true;
                continue;
            }

            if (inString)
            {
                if (c == stringChar)
                {
                    inString = false;
                }
                continue;
            }

            if (inTemplate)
            {
                if (c == '`')
                {
                    inTemplate = false;
                }
                continue;
            }

            // Check for strings
            if (c == '"' || c == '\'')
            {
                inString = true;
                stringChar = c;
                continue;
            }

            if (c == '`')
            {
                inTemplate = true;
                continue;
            }

            // Check for comments (skip rest of line)
            if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
            {
                break;
            }

            // Track brackets
            switch (c)
            {
                case '{': _openBraces++; break;
                case '}': _openBraces--; break;
                case '[': _openBrackets++; break;
                case ']': _openBrackets--; break;
                case '(': _openParens++; break;
                case ')': _openParens--; break;
            }
        }
    }

    private bool IsCompleteStatement()
    {
        // Not complete if any brackets are unclosed
        if (_openBraces > 0 || _openBrackets > 0 || _openParens > 0)
        {
            return false;
        }

        var input = _multiLineBuffer.ToString().Trim();

        // Empty input is complete (no-op)
        if (string.IsNullOrEmpty(input))
        {
            return true;
        }

        // Single-line that doesn't end with operators is likely complete
        if (!_inMultiLine)
        {
            // Check for continuation operators
            var lastChar = input[^1];
            if (lastChar == ',' || lastChar == '+' || lastChar == '-' ||
                lastChar == '*' || lastChar == '/' || lastChar == '=' ||
                lastChar == '?' || lastChar == ':' || lastChar == '&' ||
                lastChar == '|' || lastChar == '\\')
            {
                return false;
            }
        }

        return true;
    }

    private void EvaluateBuffer()
    {
        var input = _multiLineBuffer.ToString().Trim();

        if (string.IsNullOrEmpty(input))
        {
            return;
        }

        try
        {
            var result = _context.Evaluate(input, "<repl>");

            if (result.IsException)
            {
                PrintException();
            }
            else if (!result.IsUndefined)
            {
                // Print the result
                PrintResult(result);
            }
        }
        catch (JSException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
        }
    }

    private void PrintResult(in JSValue value)
    {
        var formatted = FormatValue(value);
        Console.WriteLine(formatted);
    }

    private string FormatValue(in JSValue value)
    {
        if (value.IsUndefined)
        {
            return "undefined";
        }

        if (value.IsNull)
        {
            return "null";
        }

        if (value.IsString)
        {
            // Show strings with quotes and escape special chars
            var str = value.ToString() ?? "";
            return $"'{EscapeString(str)}'";
        }

        if (value.IsObject)
        {
            var obj = value.AsObject();

            if (obj is JSFunction func)
            {
                return $"[Function: {func.Name ?? "(anonymous)"}]";
            }

            if (obj is JSArray || obj.ClassId == JSClassId.Array)
            {
                return FormatArray(obj);
            }

            // Regular object - try to format as JSON-like
            return FormatObject(obj);
        }

        return value.ToString() ?? "undefined";
    }

    private string FormatArray(JSObject arr)
    {
        var sb = new StringBuilder();
        sb.Append("[ ");

        var length = arr.Get("length").ToInt32();
        var maxItems = Math.Min(length, 100); // Limit display

        for (int i = 0; i < maxItems; i++)
        {
            if (i > 0) sb.Append(", ");
            var item = arr.Get((uint)i);
            sb.Append(FormatValue(item));
        }

        if (length > maxItems)
        {
            sb.Append($", ... {length - maxItems} more items");
        }

        sb.Append(" ]");
        return sb.ToString();
    }

    private string FormatObject(JSObject obj)
    {
        var sb = new StringBuilder();
        sb.Append("{ ");

        var keys = obj.GetOwnPropertyNames().ToList();
        var maxKeys = Math.Min(keys.Count, 20); // Limit display

        for (int i = 0; i < maxKeys; i++)
        {
            if (i > 0) sb.Append(", ");
            var key = keys[i];
            var value = obj.Get(key);
            sb.Append(key);
            sb.Append(": ");
            sb.Append(FormatValue(value));
        }

        if (keys.Count > maxKeys)
        {
            sb.Append($", ... {keys.Count - maxKeys} more properties");
        }

        sb.Append(" }");
        return sb.ToString();
    }

    private static string EscapeString(string s)
    {
        return s
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private void PrintException()
    {
        var ex = _context.CurrentException;
        if (!ex.IsUndefined)
        {
            Console.Error.WriteLine(ex.ToString());
        }
        else
        {
            Console.Error.WriteLine("Unknown error");
        }
    }

    private void ResetMultiLine()
    {
        _multiLineBuffer.Clear();
        _inMultiLine = false;
        _openBraces = 0;
        _openBrackets = 0;
        _openParens = 0;
    }
}
