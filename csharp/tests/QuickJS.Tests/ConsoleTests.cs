// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for the JavaScript console object (JSConsole class).
/// </summary>
public class ConsoleTests : IDisposable
{
    private readonly JSRuntime _runtime;
    private readonly JSContext _context;
    private readonly List<ConsoleOutputEventArgs> _outputs;

    public ConsoleTests()
    {
        _runtime = new JSRuntime();
        _context = _runtime.CreateContext();
        _outputs = new List<ConsoleOutputEventArgs>();
        _context.ConsoleOutput += (sender, e) => _outputs.Add(e);
    }

    public void Dispose()
    {
        _context.Dispose();
        _runtime.Dispose();
    }

    #region Console Existence in Context

    [Fact]
    public void Console_Exists()
    {
        // Console should exist on the global object
        var console = _context.GlobalObject.Get("console");
        Assert.True(console.IsObject);
    }

    [Fact]
    public void Console_Log_Method_Exists()
    {
        var console = _context.GlobalObject.Get("console").AsObject();
        var logMethod = console.Get("log");
        Assert.True(logMethod.TryGetObject(out var obj));
        Assert.IsType<JSFunction>(obj);
    }

    [Fact]
    public void Console_HasAllMethods()
    {
        var console = _context.GlobalObject.Get("console").AsObject();

        var methods = new[]
        {
            "log", "info", "warn", "error", "debug", "trace",
            "dir", "dirxml", "table",
            "assert",
            "count", "countReset",
            "time", "timeLog", "timeEnd",
            "group", "groupCollapsed", "groupEnd",
            "clear"
        };

        foreach (var method in methods)
        {
            var m = console.Get(method);
            Assert.True(m.TryGetObject(out var obj), $"console.{method} should exist");
            Assert.IsType<JSFunction>(obj);
        }
    }

    [Fact]
    public void Context_Console_Property()
    {
        // Verify console is accessible via context.Console
        var console = _context.Console;
        Assert.NotNull(console);
        Assert.IsType<JSConsole>(console);
    }

    #endregion

    #region JSConsole Direct Tests

    [Fact]
    public void JSConsole_Log_OutputsMessage()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        // Call log via the internal method by getting the function and calling it
        var logFunc = console.Get("log").AsObject() as JSFunction;
        Assert.NotNull(logFunc);
        logFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("Hello, World!") });

        Assert.Single(outputs);
        Assert.Equal(ConsoleLogLevel.Log, outputs[0].Level);
        Assert.Equal("Hello, World!", outputs[0].Message);
    }

    [Fact]
    public void JSConsole_Log_MultipleArgs()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var logFunc = console.Get("log").AsObject() as JSFunction;
        logFunc!.CallNative(JSValue.Undefined, new[]
        {
            JSValue.FromString("a"),
            JSValue.FromString("b"),
            JSValue.FromString("c")
        });

        Assert.Single(outputs);
        Assert.Equal("a b c", outputs[0].Message);
    }

    [Fact]
    public void JSConsole_Info_HasCorrectLevel()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var infoFunc = console.Get("info").AsObject() as JSFunction;
        infoFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("Information") });

        Assert.Single(outputs);
        Assert.Equal(ConsoleLogLevel.Info, outputs[0].Level);
    }

    [Fact]
    public void JSConsole_Warn_HasCorrectLevel()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var warnFunc = console.Get("warn").AsObject() as JSFunction;
        warnFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("Warning") });

        Assert.Single(outputs);
        Assert.Equal(ConsoleLogLevel.Warn, outputs[0].Level);
    }

    [Fact]
    public void JSConsole_Error_HasCorrectLevel()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var errorFunc = console.Get("error").AsObject() as JSFunction;
        errorFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("Error!") });

        Assert.Single(outputs);
        Assert.Equal(ConsoleLogLevel.Error, outputs[0].Level);
    }

    [Fact]
    public void JSConsole_Debug_HasCorrectLevel()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var debugFunc = console.Get("debug").AsObject() as JSFunction;
        debugFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("Debug info") });

        Assert.Single(outputs);
        Assert.Equal(ConsoleLogLevel.Debug, outputs[0].Level);
    }

    [Fact]
    public void JSConsole_Trace_HasCorrectLevel()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var traceFunc = console.Get("trace").AsObject() as JSFunction;
        traceFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("Trace") });

        Assert.Single(outputs);
        Assert.Equal(ConsoleLogLevel.Trace, outputs[0].Level);
    }

    #endregion

    #region Value Formatting

    [Fact]
    public void JSConsole_Log_FormatsNumber()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var logFunc = console.Get("log").AsObject() as JSFunction;
        logFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(42) });

        Assert.Single(outputs);
        Assert.Equal("42", outputs[0].Message);
    }

    [Fact]
    public void JSConsole_Log_FormatsFloat()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var logFunc = console.Get("log").AsObject() as JSFunction;
        logFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromDouble(3.14) });

        Assert.Single(outputs);
        Assert.Equal("3.14", outputs[0].Message);
    }

    [Fact]
    public void JSConsole_Log_FormatsBoolean()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var logFunc = console.Get("log").AsObject() as JSFunction;
        logFunc!.CallNative(JSValue.Undefined, new[] { JSValue.True, JSValue.False });

        Assert.Single(outputs);
        Assert.Equal("true false", outputs[0].Message);
    }

    [Fact]
    public void JSConsole_Log_FormatsNull()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var logFunc = console.Get("log").AsObject() as JSFunction;
        logFunc!.CallNative(JSValue.Undefined, new[] { JSValue.Null });

        Assert.Single(outputs);
        Assert.Equal("null", outputs[0].Message);
    }

    [Fact]
    public void JSConsole_Log_FormatsUndefined()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var logFunc = console.Get("log").AsObject() as JSFunction;
        logFunc!.CallNative(JSValue.Undefined, new[] { JSValue.Undefined });

        Assert.Single(outputs);
        Assert.Equal("undefined", outputs[0].Message);
    }

    [Fact]
    public void JSConsole_Log_FormatsArray()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var array = new JSArray();
        array.Push(JSValue.FromInt32(1));
        array.Push(JSValue.FromInt32(2));
        array.Push(JSValue.FromInt32(3));

        var logFunc = console.Get("log").AsObject() as JSFunction;
        logFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(array) });

        Assert.Single(outputs);
        Assert.Equal("[1, 2, 3]", outputs[0].Message);
    }

    [Fact]
    public void JSConsole_Log_FormatsObject()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var obj = new JSObject();
        obj.Set("x", JSValue.FromInt32(1));

        var logFunc = console.Get("log").AsObject() as JSFunction;
        logFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(obj) });

        Assert.Single(outputs);
        Assert.Contains("x: 1", outputs[0].Message);
    }

    [Fact]
    public void JSConsole_Log_FormatsFunction()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var func = new JSFunction((thisVal, args) => JSValue.Undefined, "myFunc", 0);

        var logFunc = console.Get("log").AsObject() as JSFunction;
        logFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(func) });

        Assert.Single(outputs);
        Assert.Contains("[Function: myFunc]", outputs[0].Message);
    }

    #endregion

    #region Assert

    [Fact]
    public void JSConsole_Assert_NoOutputWhenTrue()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var assertFunc = console.Get("assert").AsObject() as JSFunction;
        assertFunc!.CallNative(JSValue.Undefined, new[] { JSValue.True, JSValue.FromString("Should not appear") });

        Assert.Empty(outputs);
    }

    [Fact]
    public void JSConsole_Assert_OutputsWhenFalse()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var assertFunc = console.Get("assert").AsObject() as JSFunction;
        assertFunc!.CallNative(JSValue.Undefined, new[] { JSValue.False, JSValue.FromString("Assertion failed!") });

        Assert.Single(outputs);
        Assert.Equal(ConsoleLogLevel.Error, outputs[0].Level);
        Assert.Contains("Assertion failed", outputs[0].Message);
    }

    #endregion

    #region Count

    [Fact]
    public void JSConsole_Count_IncrementsCounter()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var countFunc = console.Get("count").AsObject() as JSFunction;
        countFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("myCounter") });
        countFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("myCounter") });
        countFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("myCounter") });

        Assert.Equal(3, outputs.Count);
        Assert.Contains("myCounter: 1", outputs[0].Message);
        Assert.Contains("myCounter: 2", outputs[1].Message);
        Assert.Contains("myCounter: 3", outputs[2].Message);
    }

    [Fact]
    public void JSConsole_Count_DefaultLabel()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var countFunc = console.Get("count").AsObject() as JSFunction;
        countFunc!.CallNative(JSValue.Undefined, Array.Empty<JSValue>());
        countFunc!.CallNative(JSValue.Undefined, Array.Empty<JSValue>());

        Assert.Equal(2, outputs.Count);
        Assert.Contains("default: 1", outputs[0].Message);
        Assert.Contains("default: 2", outputs[1].Message);
    }

    [Fact]
    public void JSConsole_CountReset_ResetsCounter()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var countFunc = console.Get("count").AsObject() as JSFunction;
        var countResetFunc = console.Get("countReset").AsObject() as JSFunction;

        countFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("test") });
        countFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("test") });
        countResetFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("test") });
        countFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("test") });

        Assert.Contains("test: 1", outputs[0].Message);
        Assert.Contains("test: 2", outputs[1].Message);
        // countReset doesn't produce output when counter exists, so index 2 is the third count call
        Assert.Contains("test: 1", outputs[2].Message); // After reset
    }

    #endregion

    #region Time

    [Fact]
    public void JSConsole_Time_StartsTimer()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var timeFunc = console.Get("time").AsObject() as JSFunction;
        var timeEndFunc = console.Get("timeEnd").AsObject() as JSFunction;

        timeFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("test") });
        timeEndFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("test") });

        Assert.Single(outputs);
        Assert.Equal(ConsoleLogLevel.TimeEnd, outputs[0].Level);
        Assert.Contains("test:", outputs[0].Message);
        Assert.Contains("ms", outputs[0].Message);
    }

    [Fact]
    public void JSConsole_TimeLog_LogsIntermediateTime()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var timeFunc = console.Get("time").AsObject() as JSFunction;
        var timeLogFunc = console.Get("timeLog").AsObject() as JSFunction;
        var timeEndFunc = console.Get("timeEnd").AsObject() as JSFunction;

        timeFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("test") });
        timeLogFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("test") });
        timeEndFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("test") });

        Assert.Equal(2, outputs.Count);
        Assert.Equal(ConsoleLogLevel.TimeLog, outputs[0].Level);
        Assert.Equal(ConsoleLogLevel.TimeEnd, outputs[1].Level);
    }

    [Fact]
    public void JSConsole_TimeEnd_WarnsIfNotStarted()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var timeEndFunc = console.Get("timeEnd").AsObject() as JSFunction;
        timeEndFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("nonexistent") });

        Assert.Single(outputs);
        Assert.Equal(ConsoleLogLevel.Warn, outputs[0].Level);
        Assert.Contains("does not exist", outputs[0].Message);
    }

    #endregion

    #region Group

    [Fact]
    public void JSConsole_Group_IncreasesIndent()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var logFunc = console.Get("log").AsObject() as JSFunction;
        var groupFunc = console.Get("group").AsObject() as JSFunction;
        var groupEndFunc = console.Get("groupEnd").AsObject() as JSFunction;

        logFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("before") });
        groupFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("myGroup") });
        logFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("inside") });
        groupEndFunc!.CallNative(JSValue.Undefined, Array.Empty<JSValue>());
        logFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("after") });

        Assert.Equal(4, outputs.Count);
        Assert.Equal("before", outputs[0].Message);
        Assert.Contains("myGroup", outputs[1].Message);
        Assert.StartsWith("  ", outputs[2].Message); // Indented
        Assert.Equal("after", outputs[3].Message);
    }

    [Fact]
    public void JSConsole_GroupCollapsed_IncreasesIndent()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var logFunc = console.Get("log").AsObject() as JSFunction;
        var groupCollapsedFunc = console.Get("groupCollapsed").AsObject() as JSFunction;
        var groupEndFunc = console.Get("groupEnd").AsObject() as JSFunction;

        groupCollapsedFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("collapsed") });
        logFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("inside") });
        groupEndFunc!.CallNative(JSValue.Undefined, Array.Empty<JSValue>());

        Assert.Equal(2, outputs.Count);
        Assert.StartsWith("  ", outputs[1].Message);
    }

    [Fact]
    public void JSConsole_Group_NestedGroups()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var logFunc = console.Get("log").AsObject() as JSFunction;
        var groupFunc = console.Get("group").AsObject() as JSFunction;
        var groupEndFunc = console.Get("groupEnd").AsObject() as JSFunction;

        groupFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("outer") });
        groupFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("inner") });
        logFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("deep") });
        groupEndFunc!.CallNative(JSValue.Undefined, Array.Empty<JSValue>());
        logFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("middle") });
        groupEndFunc!.CallNative(JSValue.Undefined, Array.Empty<JSValue>());

        // Find log outputs
        var deepLog = outputs.Find(o => o.Message.Contains("deep"))!;
        var middleLog = outputs.Find(o => o.Message.Contains("middle"))!;

        // Deep is indented twice (4 spaces), middle once (2 spaces)
        Assert.StartsWith("    ", deepLog.Message);
        Assert.StartsWith("  ", middleLog.Message);
        Assert.False(middleLog.Message.StartsWith("    "));
    }

    #endregion

    #region Clear

    [Fact]
    public void JSConsole_Clear_OutputsClearLevel()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var clearFunc = console.Get("clear").AsObject() as JSFunction;
        clearFunc!.CallNative(JSValue.Undefined, Array.Empty<JSValue>());

        Assert.Single(outputs);
        Assert.Equal(ConsoleLogLevel.Clear, outputs[0].Level);
    }

    #endregion

    #region Dir

    [Fact]
    public void JSConsole_Dir_OutputsObject()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var obj = new JSObject();
        obj.Set("a", JSValue.FromInt32(1));
        obj.Set("b", JSValue.FromInt32(2));

        var dirFunc = console.Get("dir").AsObject() as JSFunction;
        dirFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(obj) });

        Assert.Single(outputs);
        Assert.Equal(ConsoleLogLevel.Dir, outputs[0].Level);
        Assert.Contains("a", outputs[0].Message);
    }

    [Fact]
    public void JSConsole_DirXml_OutputsObject()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var obj = new JSObject();
        obj.Set("x", JSValue.FromString("test"));

        var dirxmlFunc = console.Get("dirxml").AsObject() as JSFunction;
        dirxmlFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(obj) });

        Assert.Single(outputs);
        // DirXml in non-browser environment behaves like dir
        Assert.Contains("x", outputs[0].Message);
    }

    #endregion

    #region Table

    [Fact]
    public void JSConsole_Table_OutputsArray()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var array = new JSArray();
        array.Push(JSValue.FromInt32(1));
        array.Push(JSValue.FromInt32(2));
        array.Push(JSValue.FromInt32(3));

        var tableFunc = console.Get("table").AsObject() as JSFunction;
        tableFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(array) });

        Assert.Single(outputs);
        Assert.Equal(ConsoleLogLevel.Table, outputs[0].Level);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void JSConsole_Log_NoArgs()
    {
        var console = new JSConsole();
        var outputs = new List<ConsoleOutputEventArgs>();
        console.Output += (s, e) => outputs.Add(e);

        var logFunc = console.Get("log").AsObject() as JSFunction;
        logFunc!.CallNative(JSValue.Undefined, Array.Empty<JSValue>());

        Assert.Single(outputs);
        Assert.Equal(string.Empty, outputs[0].Message);
    }

    [Fact]
    public void JSConsole_WithoutSubscriber_UsesDefaultOutput()
    {
        var console = new JSConsole();

        // Set DefaultOutput to null to prevent actual console output during tests
        console.DefaultOutput = null;
        console.DefaultError = null;

        // Should not throw even without subscriber
        var logFunc = console.Get("log").AsObject() as JSFunction;
        logFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("test") });
    }

    [Fact]
    public void JSConsole_MethodsReturnUndefined()
    {
        var console = new JSConsole();

        var logFunc = console.Get("log").AsObject() as JSFunction;
        var result = logFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("test") });

        Assert.True(result.IsUndefined);
    }

    #endregion

    #region Context Integration

    [Fact]
    public void Context_Console_OutputEvents()
    {
        // Use the context from constructor which has ConsoleOutput subscribed
        var console = _context.Console;

        var logFunc = console.Get("log").AsObject() as JSFunction;
        logFunc!.CallNative(JSValue.Undefined, new[] { JSValue.FromString("Context test") });

        Assert.Single(_outputs);
        Assert.Equal("Context test", _outputs[0].Message);
    }

    #endregion
}
