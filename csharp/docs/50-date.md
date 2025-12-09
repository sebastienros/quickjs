# Step 7.11: Date

This step implements the JavaScript Date object for working with dates and times.

## Overview

The Date object in JavaScript represents a single moment in time, stored internally as the number of milliseconds since the Unix epoch (January 1, 1970, 00:00:00 UTC). This implementation provides both a `JSDate` class and runtime integration with the global `Date` constructor.

## Time Representation

JavaScript dates use milliseconds since epoch as the internal representation:

```javascript
// January 1, 2000 00:00:00 UTC = 946684800000 milliseconds
new Date(946684800000)
```

### Key Constants

| Value | Description |
|-------|-------------|
| 0 | Unix epoch (Jan 1, 1970 UTC) |
| 946684800000 | Jan 1, 2000 00:00:00 UTC |
| -62135596800000 | Minimum .NET DateTime equivalent |
| 253402300799999 | Maximum .NET DateTime equivalent |

## JSDate Class

### Implementation

```csharp
public class JSDate : JSObject
{
    private double _timeValue;

    public JSDate()  // Current time
    public JSDate(double timeValue)  // Milliseconds since epoch
    public JSDate(DateTime dateTime)  // From .NET DateTime
    public JSDate(int year, int month, int date = 1, 
                  int hours = 0, int minutes = 0, 
                  int seconds = 0, int milliseconds = 0)  // Components

    public double TimeValue { get; }
    public bool IsInvalid { get; }  // true if NaN
    public DateTime? ToDateTime();
}
```

### Invalid Dates

A Date can be "invalid" when its internal time value is NaN:

```javascript
new Date("not a date").getTime()  // NaN
new Date(NaN).toString()          // "Invalid Date"
```

## Constructor Forms

### No Arguments - Current Time
```javascript
new Date()  // Current date and time
```

### Single Argument - Time Value or String
```javascript
new Date(946684800000)           // From milliseconds
new Date("2000-01-01T00:00:00Z") // Parse ISO string
```

### Multiple Arguments - Date Components
```javascript
// Year, month (0-based), day, hours, minutes, seconds, milliseconds
new Date(2000, 0, 1)              // January 1, 2000 local time
new Date(2000, 5, 15, 10, 30, 0)  // June 15, 2000 10:30 local time
```

### Two-Digit Year Handling

Years 0-99 are mapped to 1900-1999:

```javascript
new Date(99, 0, 1).getFullYear()  // 1999 (not 99)
new Date(0, 0, 1).getFullYear()   // 1900
```

## Static Methods

### Date.now()

Returns milliseconds since epoch for the current time:

```javascript
let start = Date.now();
// ... do work ...
let elapsed = Date.now() - start;
```

### Date.parse(dateString)

Parses a date string and returns milliseconds:

```javascript
Date.parse("2000-01-01T00:00:00Z")  // 946684800000
Date.parse("invalid")               // NaN
```

### Date.UTC(year, month, ...)

Returns milliseconds for UTC date components:

```javascript
Date.UTC(2000, 0, 1)  // 946684800000
```

## Instance Methods - Getters (Local Time)

| Method | Returns |
|--------|---------|
| `getFullYear()` | 4-digit year |
| `getMonth()` | Month (0-11) |
| `getDate()` | Day of month (1-31) |
| `getDay()` | Day of week (0=Sunday) |
| `getHours()` | Hours (0-23) |
| `getMinutes()` | Minutes (0-59) |
| `getSeconds()` | Seconds (0-59) |
| `getMilliseconds()` | Milliseconds (0-999) |
| `getTime()` | Milliseconds since epoch |
| `getTimezoneOffset()` | UTC offset in minutes |

## Instance Methods - Getters (UTC)

| Method | Returns |
|--------|---------|
| `getUTCFullYear()` | UTC year |
| `getUTCMonth()` | UTC month (0-11) |
| `getUTCDate()` | UTC day of month |
| `getUTCDay()` | UTC day of week |
| `getUTCHours()` | UTC hours |
| `getUTCMinutes()` | UTC minutes |
| `getUTCSeconds()` | UTC seconds |
| `getUTCMilliseconds()` | UTC milliseconds |

## Instance Methods - Setters (Local Time)

All setters return the new time value:

| Method | Parameters |
|--------|------------|
| `setFullYear(year [, month [, date]])` | Year (optionally month, date) |
| `setMonth(month [, date])` | Month (optionally date) |
| `setDate(date)` | Day of month |
| `setHours(hour [, min [, sec [, ms]]])` | Hours (optionally minutes, seconds, ms) |
| `setMinutes(min [, sec [, ms]])` | Minutes |
| `setSeconds(sec [, ms])` | Seconds |
| `setMilliseconds(ms)` | Milliseconds |
| `setTime(time)` | Direct time value |

## Instance Methods - Setters (UTC)

Same as local setters but for UTC time:
- `setUTCFullYear()`, `setUTCMonth()`, `setUTCDate()`
- `setUTCHours()`, `setUTCMinutes()`, `setUTCSeconds()`, `setUTCMilliseconds()`

## String Conversion Methods

| Method | Format | Example |
|--------|--------|---------|
| `toString()` | Full local string | "Sat Jan 01 2000 00:00:00 GMT-0800" |
| `toDateString()` | Date portion only | "Sat Jan 01 2000" |
| `toTimeString()` | Time portion only | "00:00:00 GMT-0800" |
| `toISOString()` | ISO 8601 format | "2000-01-01T00:00:00.000Z" |
| `toUTCString()` | UTC string | "Sat, 01 Jan 2000 00:00:00 GMT" |
| `toJSON()` | JSON (ISO format) | "2000-01-01T00:00:00.000Z" |
| `valueOf()` | Time value | 946684800000 |

## Design Decisions

### 1. Internal Storage

We store time as `double` milliseconds since epoch:
- Matches JavaScript semantics exactly
- Can represent NaN for invalid dates
- Range: ±8,640,000,000,000,000 ms (about ±273,000 years)

### 2. .NET DateTime Integration

```csharp
private static double DateTimeToMillis(DateTime dt)
{
    if (dt.Kind == DateTimeKind.Local)
        dt = dt.ToUniversalTime();
    var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    return (dt - epoch).TotalMilliseconds;
}
```

### 3. Local vs UTC

JavaScript Date methods come in pairs:
- `getHours()` / `getUTCHours()`
- `setMonth()` / `setUTCMonth()`

Local methods use the system's timezone, UTC methods work in UTC.

### 4. Invalid Date Handling

Methods return NaN or "Invalid Date" for invalid dates:

```csharp
public int GetFullYear() => IsInvalid ? 0 : ToLocalDateTime().Year;

public string ToString()
{
    if (IsInvalid) return "Invalid Date";
    return ToDateString() + " " + ToTimeString();
}
```

## C# Implementation Notes

### Timezone Offset

```csharp
public int GetTimezoneOffset()
{
    if (IsInvalid) return 0;
    var local = ToLocalDateTime();
    var utc = MillisToDateTime(_timeValue);
    return (int)(utc - local).TotalMinutes;
}
```

### Month Handling

JavaScript months are 0-based, .NET months are 1-based:

```csharp
// JavaScript: month 5 = June
// .NET: month 6 = June
int netMonth = jsMonth + 1;
```

## Tests

The implementation includes 48 tests covering:

1. **Constructor Tests**
   - Default constructor (current time)
   - Time value constructor
   - DateTime constructor
   - Components constructor
   - Invalid date (NaN)

2. **Static Method Tests**
   - `Date.now()`
   - `Date.parse()`
   - `Date.UTC()`

3. **Getter Tests**
   - All local time getters
   - All UTC getters
   - Timezone offset

4. **Setter Tests**
   - All local time setters
   - All UTC setters
   - `setTime()`

5. **String Conversion Tests**
   - `toString()`, `toDateString()`, `toTimeString()`
   - `toISOString()`, `toUTCString()`
   - `toJSON()`, `valueOf()`

6. **Edge Cases**
   - Invalid dates
   - Two-digit year handling
   - Timezone variations

## Files Created/Modified

### New Files
- `src/QuickJS.Core/JSDate.cs` - Date object implementation
- `tests/QuickJS.Tests/DateTests.cs` - 48 comprehensive tests
- `docs/50-date.md` - This documentation

### Modified Files
- `src/QuickJS.Core/JSContext.cs` - Added `InitializeDate()` method

## Usage Examples

### Date Formatting

```javascript
let d = new Date(2023, 5, 15, 14, 30, 0);
console.log(d.toISOString());      // "2023-06-15T21:30:00.000Z" (UTC)
console.log(d.toDateString());     // "Thu Jun 15 2023"
console.log(d.toTimeString());     // "14:30:00 GMT-0700"
```

### Date Arithmetic

```javascript
let now = new Date();
let tomorrow = new Date(now.getTime() + 24 * 60 * 60 * 1000);
let nextMonth = new Date(now);
nextMonth.setMonth(now.getMonth() + 1);
```

### Comparing Dates

```javascript
let d1 = new Date(2023, 0, 1);
let d2 = new Date(2023, 5, 1);
if (d1.getTime() < d2.getTime()) {
    console.log("d1 is earlier");
}
```

## Next Steps

- Step 7.12: Symbol and Reflect
- Step 7.13: Proxy

## References

- [ECMAScript Date Objects](https://tc39.es/ecma262/#sec-date-objects)
- [MDN Date](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Date)
- [ISO 8601](https://en.wikipedia.org/wiki/ISO_8601)
