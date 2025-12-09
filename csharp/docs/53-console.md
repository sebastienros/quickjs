# Console Object

This document describes the implementation of the JavaScript `console` object in the QuickJS C# port.

## Overview

The `console` object provides a debugging console with methods for logging messages at various levels, measuring execution time, counting occurrences, and organizing output into groups.

## Architecture

### JSConsole Class

The `JSConsole` class extends `JSObject` and provides all standard console methods:

```
JSObject
└── JSConsole
    ├── ConsoleLogLevel enum
    ├── ConsoleOutputEventArgs class
    ├── Internal counters dictionary
    ├── Internal timers dictionary (Stopwatch)
    └── Group stack for nesting
```

## Console Log Levels

The `ConsoleLogLevel` enum defines the severity levels for console output:

```csharp
public enum ConsoleLogLevel
{
    Log,     // General logging
    Info,    // Informational messages
    Warn,    // Warning messages
    Error,   // Error messages
    Debug,   // Debug messages
    Trace,   // Trace with stack information
    Assert,  // Assertion failures
    Count,   // Counter output
    Time,    // Timer output
    Group,   // Group markers
    Dir,     // Object inspection
    Table    // Tabular data
}
```

## Output Event

The `Output` event allows C# code to capture console output:

```csharp
public event EventHandler<ConsoleOutputEventArgs>? Output;

public class ConsoleOutputEventArgs : EventArgs
{
    public ConsoleLogLevel Level { get; }
    public string Message { get; }
    public int IndentLevel { get; }
}
```

### Example: Capturing Console Output

```csharp
var context = new JSContext();
var console = context.Console;

console.Output += (sender, e) =>
{
    var prefix = new string(' ', e.IndentLevel * 2);
    Console.WriteLine($"[{e.Level}] {prefix}{e.Message}");
};
```

## Logging Methods

### log, info, warn, error, debug

These methods log messages at their respective levels:

```javascript
console.log("Hello");           // Level: Log
console.info("Information");    // Level: Info
console.warn("Warning");        // Level: Warn
console.error("Error");         // Level: Error
console.debug("Debug");         // Level: Debug
```

All logging methods support multiple arguments which are space-separated:

```javascript
console.log("Count:", 42, "items"); // "Count: 42 items"
```

### trace

Outputs a stack trace to the console:

```javascript
console.trace("Trace point");
// Output: Trace: Trace point
// (stack trace follows in real implementation)
```

## Assertion

### assert

Writes an error message if the assertion is false:

```javascript
console.assert(true, "This won't show");
console.assert(false, "Assertion failed!"); // Shows: Assertion failed: Assertion failed!
```

## Counting

### count, countReset

Count how many times a particular label is called:

```javascript
console.count("myLabel");       // "myLabel: 1"
console.count("myLabel");       // "myLabel: 2"
console.count();                // "default: 1" (uses 'default' label)
console.countReset("myLabel");  // Resets counter
console.count("myLabel");       // "myLabel: 1"
```

## Timing

### time, timeLog, timeEnd

Measure elapsed time:

```javascript
console.time("myTimer");
// ... perform operations ...
console.timeLog("myTimer");     // "myTimer: 123ms" (intermediate reading)
// ... more operations ...
console.timeEnd("myTimer");     // "myTimer: 456ms" (final reading, timer removed)
```

Attempting to use a timer that doesn't exist produces a warning:

```javascript
console.timeEnd("nonexistent"); // Warning: Timer 'nonexistent' does not exist
```

## Grouping

### group, groupCollapsed, groupEnd

Organize output into collapsible groups:

```javascript
console.group("Outer");
  console.log("Inside outer");
  console.group("Inner");
    console.log("Inside inner");
  console.groupEnd();
console.groupEnd();
```

The `IndentLevel` in `ConsoleOutputEventArgs` increases with each group level.

`groupCollapsed` works identically to `group` but hints that the output should start collapsed in visual implementations.

## Object Inspection

### dir, dirxml

Inspect objects:

```javascript
console.dir({ name: "test", value: 42 });
console.dirxml(document.body); // Same as dir in non-browser environments
```

## Clearing

### clear

Clears the console:

```javascript
console.clear(); // Emits output event with Level: Log and message: console.clear
```

## Tabular Display

### table

Display tabular data:

```javascript
console.table([1, 2, 3]);
console.table({ a: 1, b: 2 });
```

## Value Formatting

The console formats values according to their type:

| Type | Format |
|------|--------|
| Undefined | `undefined` |
| Null | `null` |
| Boolean | `true` or `false` |
| Number | Numeric string |
| String | The string value (no quotes) |
| Symbol | `Symbol(description)` |
| Function | `[Function: name]` or `[Function]` |
| Array | `[element1, element2, ...]` |
| RegExp | `/pattern/flags` |
| Date | `Date: ISO string` |
| Error | `ErrorName: message` |
| Object | `{ key: value, ... }` |

## Implementation Details

### Internal State

- **Counters**: A `Dictionary<string, int>` tracks count occurrences per label
- **Timers**: A `Dictionary<string, Stopwatch>` tracks timing measurements
- **Group Stack**: A `Stack<string>` tracks group nesting with labels

### Thread Safety

The current implementation is not thread-safe. Console operations should be performed from a single thread or protected with appropriate synchronization.

### Initialization

The console object is automatically initialized when creating a `JSContext`:

```csharp
var context = new JSContext();
// context.Console is available
// context.GlobalThis["console"] is the same JSConsole instance
```

## Test Coverage

The console implementation includes comprehensive tests covering:

- All logging methods (log, info, warn, error, debug, trace)
- Multiple argument formatting
- Assertion handling (true and false cases)
- Counter operations (count, countReset)
- Timer operations (time, timeLog, timeEnd)
- Group operations (group, groupCollapsed, groupEnd)
- Object inspection (dir, dirxml)
- Table output
- Clear operation
- Value formatting for all types

## Related Files

- `JSConsole.cs` - Console implementation
- `JSContext.cs` - Context initialization including console
- `ConsoleTests.cs` - Unit tests
