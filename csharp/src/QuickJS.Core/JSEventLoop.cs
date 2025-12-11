// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;

namespace QuickJS;

/// <summary>
/// Represents a scheduled timer (setTimeout or setInterval).
/// </summary>
internal class JSTimer
{
    private static int _nextId = 1;

    /// <summary>
    /// Gets the unique identifier for this timer.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets the callback function to invoke.
    /// </summary>
    public JSFunction Callback { get; }

    /// <summary>
    /// Gets the arguments to pass to the callback.
    /// </summary>
    public JSValue[] Arguments { get; }

    /// <summary>
    /// Gets the delay in milliseconds.
    /// </summary>
    public int Delay { get; }

    /// <summary>
    /// Gets whether this is an interval timer (repeating).
    /// </summary>
    public bool IsInterval { get; }

    /// <summary>
    /// Gets or sets the next scheduled execution time.
    /// </summary>
    public long NextExecutionTicks { get; set; }

    /// <summary>
    /// Gets or sets whether the timer has been cancelled.
    /// </summary>
    public bool IsCancelled { get; set; }

    public JSTimer(JSFunction callback, JSValue[] arguments, int delay, bool isInterval)
    {
        Id = Interlocked.Increment(ref _nextId);
        Callback = callback;
        Arguments = arguments;
        Delay = Math.Max(0, delay);
        IsInterval = isInterval;
        NextExecutionTicks = DateTime.UtcNow.Ticks + (long)Delay * TimeSpan.TicksPerMillisecond;
    }

    /// <summary>
    /// Resets the ID counter (for testing).
    /// </summary>
    internal static void ResetIdCounter()
    {
        _nextId = 0;
    }
}

/// <summary>
/// Simple event loop implementation for managing timers and pending jobs.
/// </summary>
/// <remarks>
/// This class is not thread-safe. It is owned by a <see cref="JSContext"/> which
/// follows the single-threaded execution model of JavaScript runtimes.
/// </remarks>
public class JSEventLoop
{
    private readonly JSContext _context;
    private readonly Dictionary<int, JSTimer> _timers = new Dictionary<int, JSTimer>();
    private readonly Queue<Action> _microtasks = new Queue<Action>();
    private bool _running;

    /// <summary>
    /// Gets whether the event loop is currently running.
    /// </summary>
    public bool IsRunning => _running;

    /// <summary>
    /// Gets the number of active timers.
    /// </summary>
    public int ActiveTimerCount => _timers.Count;

    /// <summary>
    /// Gets the number of pending microtasks.
    /// </summary>
    public int PendingMicrotaskCount => _microtasks.Count;

    internal JSEventLoop(JSContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Schedules a function to be called after the specified delay.
    /// </summary>
    /// <param name="callback">The function to call.</param>
    /// <param name="delay">The delay in milliseconds.</param>
    /// <param name="arguments">Arguments to pass to the callback.</param>
    /// <returns>A timer identifier that can be used to cancel the timer.</returns>
    public int SetTimeout(JSFunction callback, int delay, params JSValue[] arguments)
    {
        var timer = new JSTimer(callback, arguments, delay, isInterval: false);
        _timers[timer.Id] = timer;
        return timer.Id;
    }

    /// <summary>
    /// Cancels a timeout previously scheduled with SetTimeout.
    /// </summary>
    /// <param name="timerId">The timer identifier returned by SetTimeout.</param>
    public void ClearTimeout(int timerId)
    {
        if (_timers.TryGetValue(timerId, out var timer))
        {
            timer.IsCancelled = true;
            _timers.Remove(timerId);
        }
    }

    /// <summary>
    /// Schedules a function to be called repeatedly at the specified interval.
    /// </summary>
    /// <param name="callback">The function to call.</param>
    /// <param name="interval">The interval in milliseconds.</param>
    /// <param name="arguments">Arguments to pass to the callback.</param>
    /// <returns>A timer identifier that can be used to cancel the interval.</returns>
    public int SetInterval(JSFunction callback, int interval, params JSValue[] arguments)
    {
        var timer = new JSTimer(callback, arguments, interval, isInterval: true);
        _timers[timer.Id] = timer;
        return timer.Id;
    }

    /// <summary>
    /// Cancels an interval previously scheduled with SetInterval.
    /// </summary>
    /// <param name="timerId">The timer identifier returned by SetInterval.</param>
    public void ClearInterval(int timerId)
    {
        // Same as ClearTimeout - they use the same internal mechanism
        ClearTimeout(timerId);
    }

    /// <summary>
    /// Enqueues a microtask to be executed at the next opportunity.
    /// </summary>
    /// <param name="task">The task to enqueue.</param>
    public void EnqueueMicrotask(Action task)
    {
        _microtasks.Enqueue(task);
    }

    /// <summary>
    /// Processes all pending microtasks.
    /// </summary>
    public void ProcessMicrotasks()
    {
        while (_microtasks.Count > 0)
        {
            var task = _microtasks.Dequeue();
            task?.Invoke();
        }
    }

    /// <summary>
    /// Runs the event loop until there are no more pending timers or microtasks.
    /// </summary>
    /// <param name="maxIterations">Maximum number of iterations to prevent infinite loops. Default is 1000.</param>
    /// <returns>The number of callbacks executed.</returns>
    public int Run(int maxIterations = 1000)
    {
        _running = true;
        int callbacksExecuted = 0;
        int iterations = 0;

        try
        {
            while (iterations < maxIterations)
            {
                iterations++;

                // Process all pending microtasks first
                ProcessMicrotasks();

                // Check if we have any timers to process
                JSTimer? timerToExecute = null;
                long currentTicks = DateTime.UtcNow.Ticks;

                if (_timers.Count == 0 && _microtasks.Count == 0)
                    break;

                // Find the next timer that's ready to execute
                foreach (var timer in _timers.Values)
                {
                    if (!timer.IsCancelled && timer.NextExecutionTicks <= currentTicks)
                    {
                        if (timerToExecute == null || timer.NextExecutionTicks < timerToExecute.NextExecutionTicks)
                        {
                            timerToExecute = timer;
                        }
                    }
                }

                if (timerToExecute != null)
                {
                    // Execute the timer callback
                    try
                    {
                        timerToExecute.Callback.CallNative(JSValue.Undefined, timerToExecute.Arguments);
                        callbacksExecuted++;
                    }
                    catch
                    {
                        // Timer callbacks should not throw - swallow the exception
                    }

                    if (timerToExecute.IsInterval && !timerToExecute.IsCancelled)
                    {
                        // Reschedule the interval
                        timerToExecute.NextExecutionTicks = DateTime.UtcNow.Ticks + 
                            (long)timerToExecute.Delay * TimeSpan.TicksPerMillisecond;
                    }
                    else
                    {
                        // Remove one-shot timers
                        _timers.Remove(timerToExecute.Id);
                    }

                    // Process any microtasks that may have been enqueued by the callback
                    ProcessMicrotasks();
                }
                else
                {
                    // No timer ready yet - check if we should wait or exit
                    if (_timers.Count == 0)
                        break;

                    // Find the next timer
                    long nextTicks = long.MaxValue;
                    foreach (var timer in _timers.Values)
                    {
                        if (!timer.IsCancelled && timer.NextExecutionTicks < nextTicks)
                        {
                            nextTicks = timer.NextExecutionTicks;
                        }
                    }

                    if (nextTicks == long.MaxValue)
                        break;

                    // Wait for the timer (with a small sleep to avoid busy-waiting)
                    long waitMs = (nextTicks - currentTicks) / TimeSpan.TicksPerMillisecond;
                    if (waitMs > 0)
                    {
                        // Sleep for at most 1ms to allow responsive cancellation
                        Thread.Sleep(Math.Min((int)waitMs, 1));
                    }
                }
            }

            return callbacksExecuted;
        }
        finally
        {
            _running = false;
        }
    }

    /// <summary>
    /// Processes a single iteration of the event loop (one timer or microtask batch).
    /// </summary>
    /// <returns>True if work was performed, false if there's nothing to do.</returns>
    public bool RunOnce()
    {
        // Process microtasks
        bool didWork = false;
        while (_microtasks.Count > 0)
        {
            var task = _microtasks.Dequeue();
            task?.Invoke();
            didWork = true;
        }

        // Check for a ready timer
        JSTimer? timerToExecute = null;
        long currentTicks = DateTime.UtcNow.Ticks;

        foreach (var timer in _timers.Values)
        {
            if (!timer.IsCancelled && timer.NextExecutionTicks <= currentTicks)
            {
                if (timerToExecute == null || timer.NextExecutionTicks < timerToExecute.NextExecutionTicks)
                {
                    timerToExecute = timer;
                }
            }
        }

        if (timerToExecute != null)
        {
            try
            {
                timerToExecute.Callback.CallNative(JSValue.Undefined, timerToExecute.Arguments);
                didWork = true;
            }
            catch
            {
                // Swallow
            }

            if (timerToExecute.IsInterval && !timerToExecute.IsCancelled)
            {
                timerToExecute.NextExecutionTicks = DateTime.UtcNow.Ticks + 
                    (long)timerToExecute.Delay * TimeSpan.TicksPerMillisecond;
            }
            else
            {
                _timers.Remove(timerToExecute.Id);
            }
        }

        return didWork;
    }

    /// <summary>
    /// Clears all pending timers.
    /// </summary>
    public void ClearAllTimers()
    {
        _timers.Clear();
    }

    /// <summary>
    /// Clears all pending microtasks.
    /// </summary>
    public void ClearAllMicrotasks()
    {
        _microtasks.Clear();
    }

    /// <summary>
    /// Returns whether there are pending jobs (timers or microtasks).
    /// </summary>
    public bool HasPendingJobs => _timers.Count > 0 || _microtasks.Count > 0;
}
