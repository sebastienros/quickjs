// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace QuickJS;

/// <summary>
/// Represents the JavaScript console object for logging and debugging.
/// </summary>
/// <remarks>
/// <para>
/// The console object provides access to the browser/runtime debugging console.
/// It provides methods for outputting information, timing operations, and
/// grouping related log messages.
/// </para>
/// <para>
/// This implementation follows the WHATWG Console Standard:
/// https://console.spec.whatwg.org/
/// </para>
/// </remarks>
public class JSConsole : JSObject
{
    #region Fields

    private readonly Dictionary<string, int> _counters = new();
    private readonly Dictionary<string, Stopwatch> _timers = new();
    private readonly Stack<string> _groupStack = new();
    private int _indentLevel;

    /// <summary>
    /// Event raised when console output is generated.
    /// </summary>
    public event EventHandler<ConsoleOutputEventArgs>? Output;

    /// <summary>
    /// The default output writer. If Output event is not subscribed, writes to this.
    /// </summary>
    public TextWriter? DefaultOutput { get; set; } = Console.Out;

    /// <summary>
    /// The default error writer. If Output event is not subscribed, writes errors to this.
    /// </summary>
    public TextWriter? DefaultError { get; set; } = Console.Error;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new console object.
    /// </summary>
    /// <param name="prototype">Optional prototype object.</param>
    public JSConsole(JSObject? prototype = null)
        : base(prototype, JSClassId.Object)
    {
        // Set up console methods
        SetupMethods();
    }

    private void SetupMethods()
    {
        // Logging methods
        DefineMethod("log", ConsoleLog, 0);
        DefineMethod("info", ConsoleInfo, 0);
        DefineMethod("warn", ConsoleWarn, 0);
        DefineMethod("error", ConsoleError, 0);
        DefineMethod("debug", ConsoleDebug, 0);
        DefineMethod("trace", ConsoleTrace, 0);
        DefineMethod("dir", ConsoleDir, 1);
        DefineMethod("dirxml", ConsoleDirXml, 1);

        // Assertion
        DefineMethod("assert", ConsoleAssert, 1);

        // Counting
        DefineMethod("count", ConsoleCount, 0);
        DefineMethod("countReset", ConsoleCountReset, 0);

        // Timing
        DefineMethod("time", ConsoleTime, 0);
        DefineMethod("timeLog", ConsoleTimeLog, 0);
        DefineMethod("timeEnd", ConsoleTimeEnd, 0);

        // Grouping
        DefineMethod("group", ConsoleGroup, 0);
        DefineMethod("groupCollapsed", ConsoleGroupCollapsed, 0);
        DefineMethod("groupEnd", ConsoleGroupEnd, 0);

        // Other
        DefineMethod("clear", ConsoleClear, 0);
        DefineMethod("table", ConsoleTable, 1);
    }

    private void DefineMethod(string name, JSCFunction func, int length)
    {
        Set(name, JSValue.FromObject(new JSFunction(func, name, length)));
    }

    #endregion

    #region Output Helpers

    private void WriteOutput(ConsoleLogLevel level, string message)
    {
        var indentedMessage = GetIndentedMessage(message);

        if (Output != null)
        {
            Output.Invoke(this, new ConsoleOutputEventArgs(level, indentedMessage));
        }
        else
        {
            var writer = level == ConsoleLogLevel.Error || level == ConsoleLogLevel.Warn
                ? DefaultError
                : DefaultOutput;

            writer?.WriteLine(indentedMessage);
        }
    }

    private string GetIndentedMessage(string message)
    {
        if (_indentLevel <= 0)
            return message;

        var indent = new string(' ', _indentLevel * 2);
        return indent + message.Replace("\n", "\n" + indent);
    }

    private static JSValue[] SliceArgs(JSValue[] args, int start)
    {
        if (start >= args.Length)
            return Array.Empty<JSValue>();
        var result = new JSValue[args.Length - start];
        Array.Copy(args, start, result, 0, result.Length);
        return result;
    }

    private static string FormatArgs(JSValue[] args)
    {
        if (args.Length == 0)
            return string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0)
                sb.Append(' ');
            sb.Append(FormatValue(args[i]));
        }
        return sb.ToString();
    }

    private static string FormatValue(in JSValue value)
    {
        if (value.IsUndefined)
            return "undefined";
        if (value.IsNull)
            return "null";
        if (value.IsBool)
            return value.IsTrue ? "true" : "false";
        if (value.TryGetDouble(out var d))
            return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (value.TryGetInt32(out var i))
            return i.ToString();
        if (value.TryGetString(out var s))
            return s;
        if (value.TryGetSymbol(out var sym))
            return sym.ToString();
        if (value.TryGetObject(out var obj))
            return FormatObject(obj);

        return value.ToString();
    }

    private static string FormatObject(JSObject obj)
    {
        if (obj is JSArray array)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            for (uint i = 0; i < Math.Min(array.Length, 100); i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(FormatValue(array[i]));
            }
            if (array.Length > 100)
                sb.Append(", ...");
            sb.Append(']');
            return sb.ToString();
        }

        if (obj is JSFunction func)
        {
            return $"[Function: {func.Name ?? "anonymous"}]";
        }

        if (obj is JSDate date)
        {
            return date.ToISOString();
        }

        // Check for RegExp by class ID
        if (obj.ClassId == JSClassId.RegExp)
        {
            var source = obj.Get("source");
            var flags = obj.Get("flags");
            return $"/{source}/{flags}";
        }

        // Generic object
        var props = obj.GetOwnPropertyNames();
        var propList = new List<string>();
        int count = 0;
        foreach (var prop in props)
        {
            if (count >= 10)
            {
                propList.Add("...");
                break;
            }
            var val = obj.Get(prop);
            propList.Add($"{prop}: {FormatValue(val)}");
            count++;
        }

        return $"{{ {string.Join(", ", propList)} }}";
    }

    #endregion

    #region Logging Methods

    private JSValue ConsoleLog(JSValue thisArg, JSValue[] args)
    {
        WriteOutput(ConsoleLogLevel.Log, FormatArgs(args));
        return JSValue.Undefined;
    }

    private JSValue ConsoleInfo(JSValue thisArg, JSValue[] args)
    {
        WriteOutput(ConsoleLogLevel.Info, FormatArgs(args));
        return JSValue.Undefined;
    }

    private JSValue ConsoleWarn(JSValue thisArg, JSValue[] args)
    {
        WriteOutput(ConsoleLogLevel.Warn, FormatArgs(args));
        return JSValue.Undefined;
    }

    private JSValue ConsoleError(JSValue thisArg, JSValue[] args)
    {
        WriteOutput(ConsoleLogLevel.Error, FormatArgs(args));
        return JSValue.Undefined;
    }

    private JSValue ConsoleDebug(JSValue thisArg, JSValue[] args)
    {
        WriteOutput(ConsoleLogLevel.Debug, FormatArgs(args));
        return JSValue.Undefined;
    }

    private JSValue ConsoleTrace(JSValue thisArg, JSValue[] args)
    {
        var message = args.Length > 0 ? FormatArgs(args) : "Trace";
        var stackTrace = Environment.StackTrace;
        WriteOutput(ConsoleLogLevel.Trace, $"{message}\n{stackTrace}");
        return JSValue.Undefined;
    }

    private JSValue ConsoleDir(JSValue thisArg, JSValue[] args)
    {
        if (args.Length > 0)
        {
            WriteOutput(ConsoleLogLevel.Dir, FormatValue(args[0]));
        }
        return JSValue.Undefined;
    }

    private JSValue ConsoleDirXml(JSValue thisArg, JSValue[] args)
    {
        // In browser, displays XML/HTML tree; here we just format like dir
        return ConsoleDir(thisArg, args);
    }

    #endregion

    #region Assertion

    private JSValue ConsoleAssert(JSValue thisArg, JSValue[] args)
    {
        if (args.Length == 0 || !JSValueConversion.ToBoolean(args[0]))
        {
            var message = "Assertion failed";
            if (args.Length > 1)
            {
                var msgArgs = new JSValue[args.Length - 1];
                Array.Copy(args, 1, msgArgs, 0, args.Length - 1);
                message = $"Assertion failed: {FormatArgs(msgArgs)}";
            }
            WriteOutput(ConsoleLogLevel.Error, message);
        }
        return JSValue.Undefined;
    }

    #endregion

    #region Counting

    private JSValue ConsoleCount(JSValue thisArg, JSValue[] args)
    {
        var label = args.Length > 0 && args[0].TryGetString(out var s) ? s : "default";

        if (!_counters.TryGetValue(label, out var count))
            count = 0;

        count++;
        _counters[label] = count;

        WriteOutput(ConsoleLogLevel.Count, $"{label}: {count}");
        return JSValue.Undefined;
    }

    private JSValue ConsoleCountReset(JSValue thisArg, JSValue[] args)
    {
        var label = args.Length > 0 && args[0].TryGetString(out var s) ? s : "default";

        if (_counters.ContainsKey(label))
        {
            _counters[label] = 0;
        }
        else
        {
            WriteOutput(ConsoleLogLevel.Warn, $"Count for '{label}' does not exist");
        }
        return JSValue.Undefined;
    }

    #endregion

    #region Timing

    private JSValue ConsoleTime(JSValue thisArg, JSValue[] args)
    {
        var label = args.Length > 0 && args[0].TryGetString(out var s) ? s : "default";

        if (_timers.ContainsKey(label))
        {
            WriteOutput(ConsoleLogLevel.Warn, $"Timer '{label}' already exists");
        }
        else
        {
            var sw = new Stopwatch();
            sw.Start();
            _timers[label] = sw;
        }
        return JSValue.Undefined;
    }

    private JSValue ConsoleTimeLog(JSValue thisArg, JSValue[] args)
    {
        var label = args.Length > 0 && args[0].TryGetString(out var s) ? s : "default";

        if (_timers.TryGetValue(label, out var sw))
        {
            var elapsed = sw.Elapsed.TotalMilliseconds;
            var extra = args.Length > 1
                ? " " + FormatArgs(SliceArgs(args, 1))
                : string.Empty;
            WriteOutput(ConsoleLogLevel.TimeLog, $"{label}: {elapsed:F3}ms{extra}");
        }
        else
        {
            WriteOutput(ConsoleLogLevel.Warn, $"Timer '{label}' does not exist");
        }
        return JSValue.Undefined;
    }

    private JSValue ConsoleTimeEnd(JSValue thisArg, JSValue[] args)
    {
        var label = args.Length > 0 && args[0].TryGetString(out var s) ? s : "default";

        if (_timers.TryGetValue(label, out var sw))
        {
            sw.Stop();
            var elapsed = sw.Elapsed.TotalMilliseconds;
            WriteOutput(ConsoleLogLevel.TimeEnd, $"{label}: {elapsed:F3}ms");
            _timers.Remove(label);
        }
        else
        {
            WriteOutput(ConsoleLogLevel.Warn, $"Timer '{label}' does not exist");
        }
        return JSValue.Undefined;
    }

    #endregion

    #region Grouping

    private JSValue ConsoleGroup(JSValue thisArg, JSValue[] args)
    {
        var label = args.Length > 0 ? FormatArgs(args) : string.Empty;
        if (!string.IsNullOrEmpty(label))
        {
            WriteOutput(ConsoleLogLevel.Group, label);
        }
        _groupStack.Push(label);
        _indentLevel++;
        return JSValue.Undefined;
    }

    private JSValue ConsoleGroupCollapsed(JSValue thisArg, JSValue[] args)
    {
        // In console implementations, collapsed groups start hidden
        // We treat it the same as group() in text output
        return ConsoleGroup(thisArg, args);
    }

    private JSValue ConsoleGroupEnd(JSValue thisArg, JSValue[] args)
    {
        if (_groupStack.Count > 0)
        {
            _groupStack.Pop();
            _indentLevel = Math.Max(0, _indentLevel - 1);
        }
        return JSValue.Undefined;
    }

    #endregion

    #region Other Methods

    private JSValue ConsoleClear(JSValue thisArg, JSValue[] args)
    {
        WriteOutput(ConsoleLogLevel.Clear, string.Empty);
        _indentLevel = 0;
        _groupStack.Clear();
        return JSValue.Undefined;
    }

    private JSValue ConsoleTable(JSValue thisArg, JSValue[] args)
    {
        if (args.Length == 0)
        {
            return JSValue.Undefined;
        }

        var data = args[0];
        if (!data.TryGetObject(out var obj))
        {
            WriteOutput(ConsoleLogLevel.Log, FormatValue(data));
            return JSValue.Undefined;
        }

        // Simple table formatting
        var sb = new StringBuilder();

        if (obj is JSArray array)
        {
            // Array table
            sb.AppendLine("┌───────┬────────────────────┐");
            sb.AppendLine("│ Index │ Value              │");
            sb.AppendLine("├───────┼────────────────────┤");

            for (uint i = 0; i < Math.Min(array.Length, 20); i++)
            {
                var val = FormatValue(array[i]);
                if (val.Length > 18) val = val.Substring(0, 15) + "...";
                sb.AppendLine($"│ {i,5} │ {val,-18} │");
            }

            if (array.Length > 20)
                sb.AppendLine($"│  ...  │ ({array.Length - 20} more rows) │");

            sb.AppendLine("└───────┴────────────────────┘");
        }
        else
        {
            // Object table
            sb.AppendLine("┌──────────────────┬────────────────────┐");
            sb.AppendLine("│ Key              │ Value              │");
            sb.AppendLine("├──────────────────┼────────────────────┤");

            int count = 0;
            foreach (var key in obj.GetOwnPropertyNames())
            {
                if (count >= 20)
                {
                    sb.AppendLine("│       ...        │      (more rows)   │");
                    break;
                }

                var val = FormatValue(obj.Get(key));
                var keyStr = key.Length > 16 ? key.Substring(0, 13) + "..." : key;
                if (val.Length > 18) val = val.Substring(0, 15) + "...";
                sb.AppendLine($"│ {keyStr,-16} │ {val,-18} │");
                count++;
            }

            sb.AppendLine("└──────────────────┴────────────────────┘");
        }

        WriteOutput(ConsoleLogLevel.Table, sb.ToString().TrimEnd());
        return JSValue.Undefined;
    }

    #endregion

    #region Public API for Testing

    /// <summary>
    /// Gets the current count for a label.
    /// </summary>
    public int GetCount(string label = "default")
    {
        return _counters.TryGetValue(label, out var count) ? count : 0;
    }

    /// <summary>
    /// Gets whether a timer is running.
    /// </summary>
    public bool HasTimer(string label = "default")
    {
        return _timers.ContainsKey(label);
    }

    /// <summary>
    /// Gets the current indent level.
    /// </summary>
    public int IndentLevel => _indentLevel;

    /// <summary>
    /// Gets the current group depth.
    /// </summary>
    public int GroupDepth => _groupStack.Count;

    #endregion
}

/// <summary>
/// Log level for console output.
/// </summary>
public enum ConsoleLogLevel
{
    /// <summary>General log output.</summary>
    Log,
    /// <summary>Informational message.</summary>
    Info,
    /// <summary>Warning message.</summary>
    Warn,
    /// <summary>Error message.</summary>
    Error,
    /// <summary>Debug message.</summary>
    Debug,
    /// <summary>Stack trace output.</summary>
    Trace,
    /// <summary>Directory listing.</summary>
    Dir,
    /// <summary>Counter output.</summary>
    Count,
    /// <summary>Timer log output.</summary>
    TimeLog,
    /// <summary>Timer end output.</summary>
    TimeEnd,
    /// <summary>Group start.</summary>
    Group,
    /// <summary>Table output.</summary>
    Table,
    /// <summary>Console clear.</summary>
    Clear
}

/// <summary>
/// Event arguments for console output.
/// </summary>
public class ConsoleOutputEventArgs : EventArgs
{
    /// <summary>
    /// Gets the log level.
    /// </summary>
    public ConsoleLogLevel Level { get; }

    /// <summary>
    /// Gets the message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Creates a new console output event args.
    /// </summary>
    public ConsoleOutputEventArgs(ConsoleLogLevel level, string message)
    {
        Level = level;
        Message = message;
    }
}
