# Event Loop and Timers

This document describes the implementation of the JavaScript event loop and timer functions in the QuickJS C# port.

## Overview

The event loop provides asynchronous execution support for JavaScript code through:
- **setTimeout/clearTimeout** - One-shot delayed execution
- **setInterval/clearInterval** - Repeating execution at fixed intervals
- **queueMicrotask** - Microtask queue for high-priority async tasks
- **JSEventLoop** - The core event loop that manages all pending jobs

## Architecture

### JSEventLoop Class

The `JSEventLoop` class manages timers and microtasks:

```
JSContext
└── JSEventLoop
    ├── Dictionary<int, JSTimer> _timers
    ├── Queue<Action> _microtasks
    └── Methods: SetTimeout, ClearTimeout, SetInterval, ClearInterval, etc.
```

### JSTimer Class

Internal class representing a scheduled timer:

```csharp
internal class JSTimer
{
    public int Id { get; }                    // Unique identifier
    public JSFunction Callback { get; }        // Function to invoke
    public JSValue[] Arguments { get; }        // Arguments for callback
    public int Delay { get; }                  // Delay in milliseconds
    public bool IsInterval { get; }            // Repeating or one-shot
    public long NextExecutionTicks { get; set; } // Next scheduled time
    public bool IsCancelled { get; set; }      // Cancellation flag
}
```

## Global Timer Functions

### setTimeout(callback, delay, ...args)

Schedules a function to be called after the specified delay.

```javascript
// Call after 100ms
const id = setTimeout(() => console.log("Hello"), 100);

// With arguments
setTimeout((a, b) => console.log(a + b), 100, 1, 2);
```

**Parameters:**
- `callback` - Function to execute
- `delay` - Delay in milliseconds (default: 0, negative treated as 0)
- `...args` - Optional arguments to pass to callback

**Returns:** Timer ID (positive integer) or 0 if callback is invalid

### clearTimeout(timerId)

Cancels a timeout previously scheduled with `setTimeout`.

```javascript
const id = setTimeout(() => console.log("Never runs"), 1000);
clearTimeout(id);
```

### setInterval(callback, interval, ...args)

Schedules a function to be called repeatedly at fixed intervals.

```javascript
// Call every 100ms
const id = setInterval(() => console.log("Tick"), 100);

// Stop after 5 ticks
setTimeout(() => clearInterval(id), 500);
```

**Parameters:**
- `callback` - Function to execute repeatedly
- `interval` - Interval in milliseconds
- `...args` - Optional arguments to pass to callback

**Returns:** Timer ID (positive integer) or 0 if callback is invalid

### clearInterval(timerId)

Cancels an interval previously scheduled with `setInterval`.

```javascript
const id = setInterval(() => console.log("Tick"), 100);
clearInterval(id); // Stops immediately
```

### queueMicrotask(callback)

Queues a microtask to run before the next timer callback.

```javascript
queueMicrotask(() => console.log("Microtask"));
console.log("Sync");
// Output:
// Sync
// Microtask
```

**Execution Order:**
1. All synchronous code
2. All microtasks (in FIFO order)
3. Next timer callback
4. Any microtasks queued by that callback
5. Repeat

## JSEventLoop API

### Creating and Accessing

```csharp
using var runtime = new JSRuntime();
using var context = runtime.CreateContext();

// Access the event loop
var eventLoop = context.EventLoop;
```

### Direct Timer Management

```csharp
// Create a callback
var callback = new JSFunction((thisVal, args) =>
{
    Console.WriteLine("Timer fired!");
    return JSValue.Undefined;
});

// Schedule timeout (returns ID)
int id = eventLoop.SetTimeout(callback, 1000);

// Cancel
eventLoop.ClearTimeout(id);

// Schedule interval
int intervalId = eventLoop.SetInterval(callback, 500);

// Cancel interval
eventLoop.ClearInterval(intervalId);
```

### Running the Event Loop

```csharp
// Run until no more work (with safety limit)
int callbacksExecuted = eventLoop.Run(maxIterations: 1000);

// Run a single iteration
bool didWork = eventLoop.RunOnce();

// Process only microtasks
eventLoop.ProcessMicrotasks();
```

### Querying State

```csharp
// Check if loop is currently running
bool isRunning = eventLoop.IsRunning;

// Check for pending work
bool hasPending = eventLoop.HasPendingJobs;

// Get counts
int timerCount = eventLoop.ActiveTimerCount;
int microtaskCount = eventLoop.PendingMicrotaskCount;
```

### Cleanup

```csharp
// Clear all timers
eventLoop.ClearAllTimers();

// Clear all microtasks
eventLoop.ClearAllMicrotasks();
```

## Execution Model

### Microtask vs Timer Priority

Microtasks always execute before the next timer callback:

```csharp
eventLoop.SetTimeout(callback1, 0);  // Runs second
eventLoop.EnqueueMicrotask(action);   // Runs first
eventLoop.Run();
```

### Timer Callback Order

Timers execute in order of their scheduled time:

```csharp
eventLoop.SetTimeout(cb1, 100);  // Runs second
eventLoop.SetTimeout(cb2, 50);   // Runs first
eventLoop.Run();
```

### Callbacks Can Schedule More Work

```csharp
var callback1 = new JSFunction((t, a) =>
{
    // Schedule another timeout from within a callback
    eventLoop.SetTimeout(callback2, 0);
    return JSValue.Undefined;
});
```

### Self-Clearing Intervals

```csharp
int id = 0;
int count = 0;
var callback = new JSFunction((t, a) =>
{
    count++;
    if (count >= 5)
    {
        eventLoop.ClearInterval(id);
    }
    return JSValue.Undefined;
});
id = eventLoop.SetInterval(callback, 100);
```

## Integration with Promises

The microtask queue integrates with Promise resolution:

```javascript
Promise.resolve().then(() => console.log("Promise"));
queueMicrotask(() => console.log("Microtask"));
console.log("Sync");

// Output:
// Sync
// Promise (or Microtask - order depends on implementation)
// Microtask (or Promise)
```

## Thread Safety

The `JSEventLoop` uses locks to protect internal state, allowing timer scheduling from multiple threads. However, timer callbacks execute on the thread that calls `Run()`.

**Best Practices:**
- Call `Run()` from a single dedicated thread
- Timer scheduling is thread-safe
- Callback execution is single-threaded

## Performance Considerations

### Timer Resolution

The minimum timer resolution depends on system clock precision. Very short delays (< 1ms) may not be accurately represented.

### Maximum Iterations

The `Run()` method accepts a `maxIterations` parameter (default: 1000) to prevent infinite loops from runaway intervals:

```csharp
// Runs at most 100 iterations
eventLoop.Run(maxIterations: 100);
```

### Busy Waiting

When waiting for the next timer, the event loop uses a short sleep to avoid busy-waiting while remaining responsive.

## Error Handling

### Callback Errors

Timer and microtask callbacks that throw exceptions are silently caught - the event loop continues processing remaining work.

### Invalid Arguments

- Non-function callbacks: `setTimeout` returns 0
- Invalid timer IDs: `clearTimeout` silently ignores
- Missing arguments: Handled gracefully with defaults

## Test Coverage

The timer implementation includes comprehensive tests covering:

- Basic setTimeout/clearTimeout functionality
- Basic setInterval/clearInterval functionality
- Argument passing to callbacks
- Microtask scheduling and execution order
- Self-clearing timers
- Nested timer scheduling
- Edge cases (negative delays, invalid arguments)
- Global function availability
- Event loop state tracking

## Example: Polling with Timeout

```csharp
using var runtime = new JSRuntime();
using var context = runtime.CreateContext();

var checkCount = 0;
var maxChecks = 10;
int pollId = 0;

var pollCallback = new JSFunction((t, a) =>
{
    checkCount++;
    Console.WriteLine($"Check {checkCount}");
    
    if (checkCount >= maxChecks)
    {
        context.EventLoop.ClearInterval(pollId);
        Console.WriteLine("Polling complete");
    }
    return JSValue.Undefined;
});

pollId = context.EventLoop.SetInterval(pollCallback, 100);
context.EventLoop.Run();
```

## Related Files

- `JSEventLoop.cs` - Event loop and timer implementation
- `JSContext.cs` - Global timer function initialization
- `EventLoopTests.cs` - Unit tests
