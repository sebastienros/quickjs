// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for JSEventLoop, setTimeout, setInterval, and related timer functionality.
/// </summary>
public class EventLoopTests
{
    #region JSEventLoop Direct Tests

    [Fact]
    public void JSEventLoop_ActiveTimerCount_StartsAtZero()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        Assert.Equal(0, context.EventLoop.ActiveTimerCount);
    }

    [Fact]
    public void JSEventLoop_SetTimeout_ReturnsPositiveId()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var callback = new JSFunction((thisVal, args) => JSValue.Undefined);
        var id = context.EventLoop.SetTimeout(callback, 100);

        Assert.True(id > 0);
        Assert.Equal(1, context.EventLoop.ActiveTimerCount);
    }

    [Fact]
    public void JSEventLoop_SetTimeout_ExecutesCallback()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var callCount = 0;
        var callback = new JSFunction((thisVal, args) =>
        {
            callCount++;
            return JSValue.Undefined;
        });

        context.EventLoop.SetTimeout(callback, 0);
        context.EventLoop.Run(100);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void JSEventLoop_SetTimeout_PassesArguments()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        JSValue[]? receivedArgs = null;
        var callback = new JSFunction((thisVal, args) =>
        {
            receivedArgs = args;
            return JSValue.Undefined;
        });

        context.EventLoop.SetTimeout(callback, 0, JSValue.FromInt32(42), JSValue.FromString("hello"));
        context.EventLoop.Run(100);

        Assert.NotNull(receivedArgs);
        Assert.Equal(2, receivedArgs.Length);
        Assert.Equal(42, receivedArgs[0].ToInt32());
        Assert.True(receivedArgs[1].TryGetString(out var str) && str == "hello");
    }

    [Fact]
    public void JSEventLoop_ClearTimeout_CancelsTimer()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var callCount = 0;
        var callback = new JSFunction((thisVal, args) =>
        {
            callCount++;
            return JSValue.Undefined;
        });

        var id = context.EventLoop.SetTimeout(callback, 10);
        context.EventLoop.ClearTimeout(id);
        context.EventLoop.Run(50);

        Assert.Equal(0, callCount);
        Assert.Equal(0, context.EventLoop.ActiveTimerCount);
    }

    [Fact]
    public void JSEventLoop_SetInterval_ExecutesRepeatedly()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var callCount = 0;
        var callback = new JSFunction((thisVal, args) =>
        {
            callCount++;
            return JSValue.Undefined;
        });

        var id = context.EventLoop.SetInterval(callback, 0);
        
        // Run a few iterations
        for (int i = 0; i < 5; i++)
        {
            context.EventLoop.RunOnce();
        }

        context.EventLoop.ClearInterval(id);

        Assert.True(callCount >= 3, $"Expected at least 3 calls, got {callCount}");
    }

    [Fact]
    public void JSEventLoop_ClearInterval_StopsRepetition()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var callCount = 0;
        var callback = new JSFunction((thisVal, args) =>
        {
            callCount++;
            return JSValue.Undefined;
        });

        var id = context.EventLoop.SetInterval(callback, 0);
        context.EventLoop.RunOnce();
        context.EventLoop.ClearInterval(id);

        int countAfterClear = callCount;
        context.EventLoop.Run(50);

        Assert.Equal(countAfterClear, callCount);
    }

    [Fact]
    public void JSEventLoop_EnqueueMicrotask_ExecutesTask()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var executed = false;
        context.EventLoop.EnqueueMicrotask(() => executed = true);
        context.EventLoop.ProcessMicrotasks();

        Assert.True(executed);
    }

    [Fact]
    public void JSEventLoop_Microtasks_ExecuteBeforeTimers()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var executionOrder = new List<string>();

        context.EventLoop.SetTimeout(new JSFunction((thisVal, args) =>
        {
            executionOrder.Add("timer");
            return JSValue.Undefined;
        }), 0);

        context.EventLoop.EnqueueMicrotask(() => executionOrder.Add("microtask"));

        context.EventLoop.Run(100);

        Assert.Equal(2, executionOrder.Count);
        Assert.Equal("microtask", executionOrder[0]);
        Assert.Equal("timer", executionOrder[1]);
    }

    [Fact]
    public void JSEventLoop_HasPendingJobs_ReflectsState()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        Assert.False(context.EventLoop.HasPendingJobs);

        var id = context.EventLoop.SetTimeout(new JSFunction((t, a) => JSValue.Undefined), 100);
        Assert.True(context.EventLoop.HasPendingJobs);

        context.EventLoop.ClearTimeout(id);
        Assert.False(context.EventLoop.HasPendingJobs);
    }

    [Fact]
    public void JSEventLoop_ClearAllTimers_RemovesAllTimers()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        context.EventLoop.SetTimeout(new JSFunction((t, a) => JSValue.Undefined), 100);
        context.EventLoop.SetInterval(new JSFunction((t, a) => JSValue.Undefined), 100);
        Assert.Equal(2, context.EventLoop.ActiveTimerCount);

        context.EventLoop.ClearAllTimers();
        Assert.Equal(0, context.EventLoop.ActiveTimerCount);
    }

    [Fact]
    public void JSEventLoop_Run_ReturnsCallbackCount()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        context.EventLoop.SetTimeout(new JSFunction((t, a) => JSValue.Undefined), 0);
        context.EventLoop.SetTimeout(new JSFunction((t, a) => JSValue.Undefined), 0);

        var executed = context.EventLoop.Run(100);
        Assert.Equal(2, executed);
    }

    #endregion

    #region Global setTimeout/clearTimeout Tests

    [Fact]
    public void Global_SetTimeout_ExistsOnGlobal()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var setTimeout = context.GlobalObject.Get("setTimeout");
        Assert.True(setTimeout.IsObject);
        Assert.IsType<JSFunction>(setTimeout.AsObject());
    }

    [Fact]
    public void Global_ClearTimeout_ExistsOnGlobal()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var clearTimeout = context.GlobalObject.Get("clearTimeout");
        Assert.True(clearTimeout.IsObject);
        Assert.IsType<JSFunction>(clearTimeout.AsObject());
    }

    [Fact]
    public void Global_SetInterval_ExistsOnGlobal()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var setInterval = context.GlobalObject.Get("setInterval");
        Assert.True(setInterval.IsObject);
        Assert.IsType<JSFunction>(setInterval.AsObject());
    }

    [Fact]
    public void Global_ClearInterval_ExistsOnGlobal()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var clearInterval = context.GlobalObject.Get("clearInterval");
        Assert.True(clearInterval.IsObject);
        Assert.IsType<JSFunction>(clearInterval.AsObject());
    }

    [Fact]
    public void Global_QueueMicrotask_ExistsOnGlobal()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var queueMicrotask = context.GlobalObject.Get("queueMicrotask");
        Assert.True(queueMicrotask.IsObject);
        Assert.IsType<JSFunction>(queueMicrotask.AsObject());
    }

    [Fact]
    public void Global_SetTimeout_CallsCallback()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var callCount = 0;
        var callback = new JSFunction((thisVal, args) =>
        {
            callCount++;
            return JSValue.Undefined;
        });

        var setTimeoutFn = context.GlobalObject.Get("setTimeout").AsObject() as JSFunction;
        var result = setTimeoutFn!.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(callback), JSValue.FromInt32(0) });

        Assert.True(result.ToInt32() > 0);

        context.EventLoop.Run(100);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Global_SetTimeout_WithArguments()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        JSValue[]? receivedArgs = null;
        var callback = new JSFunction((thisVal, args) =>
        {
            receivedArgs = args;
            return JSValue.Undefined;
        });

        var setTimeoutFn = context.GlobalObject.Get("setTimeout").AsObject() as JSFunction;
        setTimeoutFn!.CallNative(JSValue.Undefined, new[]
        {
            JSValue.FromObject(callback),
            JSValue.FromInt32(0),
            JSValue.FromString("arg1"),
            JSValue.FromInt32(123)
        });

        context.EventLoop.Run(100);

        Assert.NotNull(receivedArgs);
        Assert.Equal(2, receivedArgs.Length);
    }

    [Fact]
    public void Global_ClearTimeout_CancelsCallback()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var callCount = 0;
        var callback = new JSFunction((thisVal, args) =>
        {
            callCount++;
            return JSValue.Undefined;
        });

        var setTimeoutFn = context.GlobalObject.Get("setTimeout").AsObject() as JSFunction;
        var clearTimeoutFn = context.GlobalObject.Get("clearTimeout").AsObject() as JSFunction;

        var timerId = setTimeoutFn!.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(callback), JSValue.FromInt32(10) });
        clearTimeoutFn!.CallNative(JSValue.Undefined, new[] { timerId });

        context.EventLoop.Run(50);
        Assert.Equal(0, callCount);
    }

    [Fact]
    public void Global_SetInterval_RepeatsCallback()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var callCount = 0;
        var callback = new JSFunction((thisVal, args) =>
        {
            callCount++;
            return JSValue.Undefined;
        });

        var setIntervalFn = context.GlobalObject.Get("setInterval").AsObject() as JSFunction;
        var clearIntervalFn = context.GlobalObject.Get("clearInterval").AsObject() as JSFunction;

        var timerId = setIntervalFn!.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(callback), JSValue.FromInt32(0) });

        for (int i = 0; i < 5; i++)
        {
            context.EventLoop.RunOnce();
        }

        clearIntervalFn!.CallNative(JSValue.Undefined, new[] { timerId });

        Assert.True(callCount >= 3);
    }

    [Fact]
    public void Global_ClearInterval_StopsRepeating()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var callCount = 0;
        var callback = new JSFunction((thisVal, args) =>
        {
            callCount++;
            return JSValue.Undefined;
        });

        var setIntervalFn = context.GlobalObject.Get("setInterval").AsObject() as JSFunction;
        var clearIntervalFn = context.GlobalObject.Get("clearInterval").AsObject() as JSFunction;

        var timerId = setIntervalFn!.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(callback), JSValue.FromInt32(0) });
        context.EventLoop.RunOnce();

        var countAfterFirst = callCount;
        clearIntervalFn!.CallNative(JSValue.Undefined, new[] { timerId });

        context.EventLoop.Run(50);
        Assert.Equal(countAfterFirst, callCount);
    }

    [Fact]
    public void Global_QueueMicrotask_ExecutesMicrotask()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var executed = false;
        var callback = new JSFunction((thisVal, args) =>
        {
            executed = true;
            return JSValue.Undefined;
        });

        var queueMicrotaskFn = context.GlobalObject.Get("queueMicrotask").AsObject() as JSFunction;
        queueMicrotaskFn!.CallNative(JSValue.Undefined, new[] { JSValue.FromObject(callback) });

        context.EventLoop.ProcessMicrotasks();
        Assert.True(executed);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void SetTimeout_WithNegativeDelay_TreatsAsZero()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var executed = false;
        var callback = new JSFunction((thisVal, args) =>
        {
            executed = true;
            return JSValue.Undefined;
        });

        context.EventLoop.SetTimeout(callback, -100);
        context.EventLoop.Run(100);

        Assert.True(executed);
    }

    [Fact]
    public void SetTimeout_WithNonFunctionArgument_ReturnsZero()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var setTimeoutFn = context.GlobalObject.Get("setTimeout").AsObject() as JSFunction;
        var result = setTimeoutFn!.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(42), JSValue.FromInt32(0) });

        Assert.Equal(0, result.ToInt32());
    }

    [Fact]
    public void ClearTimeout_WithInvalidId_DoesNotThrow()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var clearTimeoutFn = context.GlobalObject.Get("clearTimeout").AsObject() as JSFunction;
        var exception = Record.Exception(() =>
            clearTimeoutFn!.CallNative(JSValue.Undefined, new[] { JSValue.FromInt32(99999) }));

        Assert.Null(exception);
    }

    [Fact]
    public void ClearTimeout_WithNoArguments_DoesNotThrow()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var clearTimeoutFn = context.GlobalObject.Get("clearTimeout").AsObject() as JSFunction;
        var exception = Record.Exception(() =>
            clearTimeoutFn!.CallNative(JSValue.Undefined, Array.Empty<JSValue>()));

        Assert.Null(exception);
    }

    [Fact]
    public void SetTimeout_CallbackCanSetAnotherTimeout()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var callOrder = new List<int>();
        JSFunction? secondCallback = null;

        secondCallback = new JSFunction((thisVal, args) =>
        {
            callOrder.Add(2);
            return JSValue.Undefined;
        });

        var firstCallback = new JSFunction((thisVal, args) =>
        {
            callOrder.Add(1);
            context.EventLoop.SetTimeout(secondCallback!, 0);
            return JSValue.Undefined;
        });

        context.EventLoop.SetTimeout(firstCallback, 0);
        context.EventLoop.Run(100);

        Assert.Equal(new[] { 1, 2 }, callOrder);
    }

    [Fact]
    public void SetTimeout_CallbackCanClearItself()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var callCount = 0;
        int timerId = 0;

        var callback = new JSFunction((thisVal, args) =>
        {
            callCount++;
            context.EventLoop.ClearTimeout(timerId);
            return JSValue.Undefined;
        });

        timerId = context.EventLoop.SetTimeout(callback, 0);
        context.EventLoop.Run(100);

        // Should still execute once since it was already scheduled
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void SetInterval_CallbackCanClearItself()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var callCount = 0;
        int timerId = 0;

        var callback = new JSFunction((thisVal, args) =>
        {
            callCount++;
            if (callCount >= 3)
            {
                context.EventLoop.ClearInterval(timerId);
            }
            return JSValue.Undefined;
        });

        timerId = context.EventLoop.SetInterval(callback, 0);
        context.EventLoop.Run(100);

        Assert.Equal(3, callCount);
    }

    [Fact]
    public void Run_WithMaxIterations_StopsEarly()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var callCount = 0;
        var callback = new JSFunction((thisVal, args) =>
        {
            callCount++;
            return JSValue.Undefined;
        });

        // Create an interval that would run forever
        context.EventLoop.SetInterval(callback, 0);

        // Run with a small max to prevent infinite loop
        context.EventLoop.Run(maxIterations: 5);

        // Should have stopped due to max iterations
        Assert.True(callCount <= 5);

        context.EventLoop.ClearAllTimers();
    }

    [Fact]
    public void Run_ExitsWhenNoMoreWork()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var callCount = 0;
        var callback = new JSFunction((thisVal, args) =>
        {
            callCount++;
            return JSValue.Undefined;
        });

        context.EventLoop.SetTimeout(callback, 0);
        context.EventLoop.SetTimeout(callback, 0);

        var result = context.EventLoop.Run(1000);

        Assert.Equal(2, callCount);
        Assert.Equal(2, result);
    }

    [Fact]
    public void Microtask_ExecutesImmediately_BeforeNextTimer()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        var order = new List<string>();

        // Schedule a timeout
        context.EventLoop.SetTimeout(new JSFunction((t, a) =>
        {
            order.Add("timeout");
            // Inside timeout, queue a microtask
            context.EventLoop.EnqueueMicrotask(() => order.Add("microtask-in-timeout"));
            return JSValue.Undefined;
        }), 0);

        // Queue a microtask before running
        context.EventLoop.EnqueueMicrotask(() => order.Add("microtask-before"));

        context.EventLoop.Run(100);

        // Microtask-before should run first, then timeout, then microtask-in-timeout
        Assert.Equal(new[] { "microtask-before", "timeout", "microtask-in-timeout" }, order);
    }

    #endregion

    #region IsRunning Tests

    [Fact]
    public void EventLoop_IsRunning_FalseInitially()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        Assert.False(context.EventLoop.IsRunning);
    }

    [Fact]
    public void EventLoop_IsRunning_TrueDuringRun()
    {
        using var runtime = new JSRuntime();
        using var context = runtime.CreateContext();

        bool wasRunning = false;
        var callback = new JSFunction((thisVal, args) =>
        {
            wasRunning = context.EventLoop.IsRunning;
            return JSValue.Undefined;
        });

        context.EventLoop.SetTimeout(callback, 0);
        context.EventLoop.Run(100);

        Assert.True(wasRunning);
        Assert.False(context.EventLoop.IsRunning);
    }

    #endregion
}
