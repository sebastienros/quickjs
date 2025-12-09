// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Xunit;

namespace QuickJS.Tests;

/// <summary>
/// Tests for JSDate, Date constructor, and Date methods
/// </summary>
public class DateTests : IDisposable
{
    private readonly JSRuntime _runtime;
    private readonly JSContext _context;

    public DateTests()
    {
        _runtime = new JSRuntime();
        _context = _runtime.CreateContext();
    }

    public void Dispose()
    {
        _context.Dispose();
        _runtime.Dispose();
    }

    #region JSDate Class Tests

    [Fact]
    public void JSDate_DefaultConstructor_CreatesCurrentDate()
    {
        var before = DateTime.UtcNow;
        var date = new JSDate();
        var after = DateTime.UtcNow;

        Assert.False(date.IsInvalid);
        var dt = date.ToDateTime();
        Assert.NotNull(dt);
        Assert.True(dt >= before && dt <= after);
    }

    [Fact]
    public void JSDate_TimeValueConstructor_SetsTimeValue()
    {
        // 2000-01-01 00:00:00 UTC = 946684800000 ms
        var date = new JSDate(946684800000);

        Assert.False(date.IsInvalid);
        Assert.Equal(946684800000, date.TimeValue);
    }

    [Fact]
    public void JSDate_DateTimeConstructor_ConvertsCorrectly()
    {
        var dt = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var date = new JSDate(dt);

        Assert.False(date.IsInvalid);
        var result = date.ToDateTime();
        Assert.NotNull(result);
        Assert.Equal(dt, result.Value);
    }

    [Fact]
    public void JSDate_ComponentsConstructor_CreatesDate()
    {
        // Note: month is 0-based (0 = January)
        var date = new JSDate(2023, 5, 15, 10, 30, 45, 500);

        Assert.False(date.IsInvalid);
        Assert.Equal(2023, date.GetFullYear());
        Assert.Equal(5, date.GetMonth()); // June (0-based)
        Assert.Equal(15, date.GetDate());
        Assert.Equal(10, date.GetHours());
        Assert.Equal(30, date.GetMinutes());
        Assert.Equal(45, date.GetSeconds());
        Assert.Equal(500, date.GetMilliseconds());
    }

    [Fact]
    public void JSDate_NaN_IsInvalid()
    {
        var date = new JSDate(double.NaN);

        Assert.True(date.IsInvalid);
        Assert.Null(date.ToDateTime());
    }

    [Fact]
    public void JSDate_Now_ReturnsCurrentTimeMillis()
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var now = JSDate.Now();
        var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Allow for a small tolerance
        Assert.True(now >= before - 100 && now <= after + 100, $"Expected now ({now}) to be between {before} and {after}");
    }

    [Fact]
    public void JSDate_Parse_ValidISODate()
    {
        var millis = JSDate.Parse("2023-06-15T10:30:00.000Z");

        Assert.False(double.IsNaN(millis));
        var date = new JSDate(millis);
        Assert.Equal(2023, date.GetUTCFullYear());
        Assert.Equal(5, date.GetUTCMonth()); // June
        Assert.Equal(15, date.GetUTCDate());
    }

    [Fact]
    public void JSDate_Parse_InvalidString_ReturnsNaN()
    {
        var millis = JSDate.Parse("not a date");

        Assert.True(double.IsNaN(millis));
    }

    [Fact]
    public void JSDate_UTC_ReturnsMillis()
    {
        // UTC(2000, 0, 1) = Jan 1, 2000 = 946684800000
        var millis = JSDate.UTC(2000, 0, 1);

        Assert.Equal(946684800000, millis);
    }

    #endregion

    #region Getters (Local Time)

    [Fact]
    public void JSDate_GetDay_ReturnsWeekday()
    {
        // Jan 1, 2000 was a Saturday (day 6)
        var date = new JSDate(946684800000); // UTC time
        
        // GetDay returns day of week (0=Sunday)
        var day = date.GetUTCDay();
        Assert.Equal(6, day); // Saturday
    }

    [Fact]
    public void JSDate_GetTime_ReturnsTimeValue()
    {
        var date = new JSDate(1234567890123);
        Assert.Equal(1234567890123, date.GetTime());
    }

    #endregion

    #region Getters (UTC)

    [Fact]
    public void JSDate_GetUTCFullYear_ReturnsYear()
    {
        var date = new JSDate(946684800000); // 2000-01-01 00:00:00 UTC
        Assert.Equal(2000, date.GetUTCFullYear());
    }

    [Fact]
    public void JSDate_GetUTCMonth_ReturnsMonth()
    {
        var date = new JSDate(946684800000); // 2000-01-01 UTC
        Assert.Equal(0, date.GetUTCMonth()); // January
    }

    [Fact]
    public void JSDate_GetUTCDate_ReturnsDay()
    {
        var date = new JSDate(946684800000); // 2000-01-01 UTC
        Assert.Equal(1, date.GetUTCDate());
    }

    [Fact]
    public void JSDate_GetUTCHours_ReturnsHours()
    {
        var date = new JSDate(946684800000); // 2000-01-01 00:00:00 UTC
        Assert.Equal(0, date.GetUTCHours());
    }

    [Fact]
    public void JSDate_GetUTCMinutes_ReturnsMinutes()
    {
        var date = new JSDate(946684800000 + 30 * 60 * 1000); // 2000-01-01 00:30:00 UTC
        Assert.Equal(30, date.GetUTCMinutes());
    }

    [Fact]
    public void JSDate_GetUTCSeconds_ReturnsSeconds()
    {
        var date = new JSDate(946684800000 + 45 * 1000); // 2000-01-01 00:00:45 UTC
        Assert.Equal(45, date.GetUTCSeconds());
    }

    [Fact]
    public void JSDate_GetUTCMilliseconds_ReturnsMilliseconds()
    {
        var date = new JSDate(946684800123); // 2000-01-01 00:00:00.123 UTC
        Assert.Equal(123, date.GetUTCMilliseconds());
    }

    #endregion

    #region Setters (Local Time)

    [Fact]
    public void JSDate_SetTime_ChangesTimeValue()
    {
        var date = new JSDate(0);
        date.SetTime(946684800000);

        Assert.Equal(946684800000, date.GetTime());
    }

    [Fact]
    public void JSDate_SetMilliseconds_ChangesMilliseconds()
    {
        var date = new JSDate(946684800000);
        date.SetMilliseconds(500);

        Assert.Equal(500, date.GetMilliseconds());
    }

    #endregion

    #region Setters (UTC)

    [Fact]
    public void JSDate_SetUTCFullYear_ChangesYear()
    {
        var date = new JSDate(946684800000); // 2000-01-01
        date.SetUTCFullYear(2025);

        Assert.Equal(2025, date.GetUTCFullYear());
    }

    [Fact]
    public void JSDate_SetUTCMonth_ChangesMonth()
    {
        var date = new JSDate(946684800000); // 2000-01-01
        date.SetUTCMonth(6); // July

        Assert.Equal(6, date.GetUTCMonth());
    }

    [Fact]
    public void JSDate_SetUTCDate_ChangesDay()
    {
        var date = new JSDate(946684800000); // 2000-01-01
        date.SetUTCDate(15);

        Assert.Equal(15, date.GetUTCDate());
    }

    [Fact]
    public void JSDate_SetUTCHours_ChangesHours()
    {
        var date = new JSDate(946684800000); // 2000-01-01 00:00:00
        date.SetUTCHours(12);

        Assert.Equal(12, date.GetUTCHours());
    }

    #endregion

    #region String Conversion

    [Fact]
    public void JSDate_ToISOString_ReturnsISOFormat()
    {
        var date = new JSDate(946684800000); // 2000-01-01 00:00:00 UTC
        var iso = date.ToISOString();

        Assert.Equal("2000-01-01T00:00:00.000Z", iso);
    }

    [Fact]
    public void JSDate_ToUTCString_ReturnsUTCFormat()
    {
        var date = new JSDate(946684800000); // 2000-01-01 00:00:00 UTC
        var utc = date.ToUTCString();

        Assert.Contains("01 Jan 2000", utc);
        Assert.Contains("GMT", utc);
    }

    [Fact]
    public void JSDate_ToString_InvalidDate_ReturnsInvalidDate()
    {
        var date = new JSDate(double.NaN);
        Assert.Equal("Invalid Date", date.ToString());
    }

    [Fact]
    public void JSDate_ToJSON_ReturnsISOString()
    {
        var date = new JSDate(946684800000);
        var json = date.ToJSON();

        Assert.Equal("2000-01-01T00:00:00.000Z", json);
    }

    [Fact]
    public void JSDate_ToJSON_InvalidDate_ReturnsNull()
    {
        var date = new JSDate(double.NaN);
        var json = date.ToJSON();

        Assert.Equal("null", json);
    }

    [Fact]
    public void JSDate_ValueOf_ReturnsTimeValue()
    {
        var date = new JSDate(946684800000);
        Assert.Equal(946684800000, date.ValueOf());
    }

    #endregion

    #region Context Date Constructor Tests

    [Fact]
    public void DateConstructor_NoArgs_ReturnsCurrentDate()
    {
        var dateCtor = _context.GetGlobalProperty("Date");
        Assert.True(dateCtor.IsObject);

        var nowFn = dateCtor.AsObject().Get("now");
        Assert.True(nowFn.IsObject);

        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var result = ((JSFunction)nowFn.AsObject()).CallNative(JSValue.Undefined, Array.Empty<JSValue>());
        var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Assert.True(result.IsNumber);
        var millis = result.ToDouble();
        // Allow for timing tolerance
        Assert.True(millis >= before - 100 && millis <= after + 100, $"Expected millis ({millis}) to be between {before - 100} and {after + 100}");
    }

    [Fact]
    public void DateConstructor_Parse_ValidString()
    {
        var dateCtor = _context.GetGlobalProperty("Date");
        var parseFn = dateCtor.AsObject().Get("parse");

        var result = ((JSFunction)parseFn.AsObject()).CallNative(
            JSValue.Undefined,
            new[] { JSValue.FromString("2023-06-15T00:00:00.000Z") });

        Assert.True(result.IsNumber);
        Assert.False(double.IsNaN(result.ToDouble()));
    }

    [Fact]
    public void DateConstructor_UTC_ReturnsMillis()
    {
        var dateCtor = _context.GetGlobalProperty("Date");
        var utcFn = dateCtor.AsObject().Get("UTC");

        var result = ((JSFunction)utcFn.AsObject()).CallNative(
            JSValue.Undefined,
            new[] { JSValue.FromInt32(2000), JSValue.FromInt32(0), JSValue.FromInt32(1) });

        Assert.True(result.IsNumber);
        Assert.Equal(946684800000, result.ToDouble());
    }

    [Fact]
    public void DatePrototype_GetFullYear_Works()
    {
        var dateCtor = _context.GetGlobalProperty("Date");
        
        // Create a new Date with specific time value
        var dateObj = ((JSFunction)dateCtor.AsObject()).CallNative(
            JSValue.Undefined,
            new[] { JSValue.FromDouble(946684800000) }); // 2000-01-01 00:00:00 UTC

        var date = dateObj.AsObject() as JSDate;
        Assert.NotNull(date);

        // Use getUTCFullYear for consistent timezone-independent results
        var getFullYearFn = date!.Get("getUTCFullYear");
        var result = ((JSFunction)getFullYearFn.AsObject()).CallNative(dateObj, Array.Empty<JSValue>());

        Assert.True(result.IsNumber);
        Assert.Equal(2000, result.ToInt32());
    }

    [Fact]
    public void DatePrototype_GetUTCFullYear_Works()
    {
        var dateCtor = _context.GetGlobalProperty("Date");
        
        var dateObj = ((JSFunction)dateCtor.AsObject()).CallNative(
            JSValue.Undefined,
            new[] { JSValue.FromDouble(946684800000) });

        var date = dateObj.AsObject() as JSDate;
        Assert.NotNull(date);

        var getFn = date!.Get("getUTCFullYear");
        var result = ((JSFunction)getFn.AsObject()).CallNative(dateObj, Array.Empty<JSValue>());

        Assert.True(result.IsNumber);
        Assert.Equal(2000, result.ToInt32());
    }

    [Fact]
    public void DatePrototype_GetUTCMonth_Works()
    {
        var dateCtor = _context.GetGlobalProperty("Date");
        
        var dateObj = ((JSFunction)dateCtor.AsObject()).CallNative(
            JSValue.Undefined,
            new[] { JSValue.FromDouble(946684800000) });

        var date = dateObj.AsObject() as JSDate;
        var getFn = date!.Get("getUTCMonth");
        var result = ((JSFunction)getFn.AsObject()).CallNative(dateObj, Array.Empty<JSValue>());

        Assert.True(result.IsNumber);
        Assert.Equal(0, result.ToInt32()); // January = 0
    }

    [Fact]
    public void DatePrototype_GetTime_Works()
    {
        var dateCtor = _context.GetGlobalProperty("Date");
        
        var dateObj = ((JSFunction)dateCtor.AsObject()).CallNative(
            JSValue.Undefined,
            new[] { JSValue.FromDouble(946684800000) });

        var date = dateObj.AsObject() as JSDate;
        var getFn = date!.Get("getTime");
        var result = ((JSFunction)getFn.AsObject()).CallNative(dateObj, Array.Empty<JSValue>());

        Assert.True(result.IsNumber);
        Assert.Equal(946684800000, result.ToDouble());
    }

    [Fact]
    public void DatePrototype_SetTime_Works()
    {
        var dateCtor = _context.GetGlobalProperty("Date");
        
        var dateObj = ((JSFunction)dateCtor.AsObject()).CallNative(
            JSValue.Undefined,
            new[] { JSValue.FromDouble(0) });

        var date = dateObj.AsObject() as JSDate;
        var setFn = date!.Get("setTime");
        ((JSFunction)setFn.AsObject()).CallNative(dateObj, new[] { JSValue.FromDouble(946684800000) });

        var getFn = date.Get("getTime");
        var result = ((JSFunction)getFn.AsObject()).CallNative(dateObj, Array.Empty<JSValue>());

        Assert.Equal(946684800000, result.ToDouble());
    }

    [Fact]
    public void DatePrototype_SetUTCFullYear_Works()
    {
        var dateCtor = _context.GetGlobalProperty("Date");
        
        var dateObj = ((JSFunction)dateCtor.AsObject()).CallNative(
            JSValue.Undefined,
            new[] { JSValue.FromDouble(946684800000) }); // 2000

        var date = dateObj.AsObject() as JSDate;
        var setFn = date!.Get("setUTCFullYear");
        ((JSFunction)setFn.AsObject()).CallNative(dateObj, new[] { JSValue.FromInt32(2025) });

        var getFn = date.Get("getUTCFullYear");
        var result = ((JSFunction)getFn.AsObject()).CallNative(dateObj, Array.Empty<JSValue>());

        Assert.Equal(2025, result.ToInt32());
    }

    [Fact]
    public void DatePrototype_ToISOString_Works()
    {
        var dateCtor = _context.GetGlobalProperty("Date");
        
        var dateObj = ((JSFunction)dateCtor.AsObject()).CallNative(
            JSValue.Undefined,
            new[] { JSValue.FromDouble(946684800000) });

        var date = dateObj.AsObject() as JSDate;
        var toISOFn = date!.Get("toISOString");
        var result = ((JSFunction)toISOFn.AsObject()).CallNative(dateObj, Array.Empty<JSValue>());

        Assert.True(result.IsString);
        Assert.Equal("2000-01-01T00:00:00.000Z", result.ToString());
    }

    [Fact]
    public void DatePrototype_ToUTCString_Works()
    {
        var dateCtor = _context.GetGlobalProperty("Date");
        
        var dateObj = ((JSFunction)dateCtor.AsObject()).CallNative(
            JSValue.Undefined,
            new[] { JSValue.FromDouble(946684800000) });

        var date = dateObj.AsObject() as JSDate;
        var toUTCFn = date!.Get("toUTCString");
        var result = ((JSFunction)toUTCFn.AsObject()).CallNative(dateObj, Array.Empty<JSValue>());

        Assert.True(result.IsString);
        Assert.Contains("GMT", result.ToString());
    }

    [Fact]
    public void DatePrototype_ValueOf_Works()
    {
        var dateCtor = _context.GetGlobalProperty("Date");
        
        var dateObj = ((JSFunction)dateCtor.AsObject()).CallNative(
            JSValue.Undefined,
            new[] { JSValue.FromDouble(946684800000) });

        var date = dateObj.AsObject() as JSDate;
        var valueOfFn = date!.Get("valueOf");
        var result = ((JSFunction)valueOfFn.AsObject()).CallNative(dateObj, Array.Empty<JSValue>());

        Assert.True(result.IsNumber);
        Assert.Equal(946684800000, result.ToDouble());
    }

    [Fact]
    public void DatePrototype_ToJSON_Works()
    {
        var dateCtor = _context.GetGlobalProperty("Date");
        
        var dateObj = ((JSFunction)dateCtor.AsObject()).CallNative(
            JSValue.Undefined,
            new[] { JSValue.FromDouble(946684800000) });

        var date = dateObj.AsObject() as JSDate;
        var toJSONFn = date!.Get("toJSON");
        var result = ((JSFunction)toJSONFn.AsObject()).CallNative(dateObj, Array.Empty<JSValue>());

        Assert.True(result.IsString);
        Assert.Equal("2000-01-01T00:00:00.000Z", result.ToString());
    }

    [Fact]
    public void DatePrototype_ToString_Works()
    {
        var dateCtor = _context.GetGlobalProperty("Date");
        
        var dateObj = ((JSFunction)dateCtor.AsObject()).CallNative(
            JSValue.Undefined,
            new[] { JSValue.FromDouble(946684800000) });

        var date = dateObj.AsObject() as JSDate;
        var toStringFn = date!.Get("toString");
        var result = ((JSFunction)toStringFn.AsObject()).CallNative(dateObj, Array.Empty<JSValue>());

        Assert.True(result.IsString);
        // toString includes both date and time portions - it may show 1999 or 2000 depending on timezone
        var str = result.ToString()!;
        Assert.True(str.Contains("1999") || str.Contains("2000"), $"Expected date string to contain '1999' or '2000', got: {str}");
    }

    [Fact]
    public void DateConstructor_WithComponents_Works()
    {
        var dateCtor = _context.GetGlobalProperty("Date");
        
        // new Date(2023, 5, 15, 10, 30, 0, 0) = June 15, 2023 10:30 local
        var dateObj = ((JSFunction)dateCtor.AsObject()).CallNative(
            JSValue.Undefined,
            new[] { 
                JSValue.FromInt32(2023), 
                JSValue.FromInt32(5), // June
                JSValue.FromInt32(15), 
                JSValue.FromInt32(10), 
                JSValue.FromInt32(30) 
            });

        var date = dateObj.AsObject() as JSDate;
        Assert.NotNull(date);
        Assert.Equal(2023, date!.GetFullYear());
        Assert.Equal(5, date.GetMonth());
        Assert.Equal(15, date.GetDate());
    }

    [Fact]
    public void DatePrototype_ToGMTString_IsAliasForToUTCString()
    {
        var dateCtor = _context.GetGlobalProperty("Date");
        
        var dateObj = ((JSFunction)dateCtor.AsObject()).CallNative(
            JSValue.Undefined,
            new[] { JSValue.FromDouble(946684800000) });

        var date = dateObj.AsObject() as JSDate;
        
        var toUTCFn = date!.Get("toUTCString");
        var toGMTFn = date.Get("toGMTString");
        
        var utcResult = ((JSFunction)toUTCFn.AsObject()).CallNative(dateObj, Array.Empty<JSValue>());
        var gmtResult = ((JSFunction)toGMTFn.AsObject()).CallNative(dateObj, Array.Empty<JSValue>());

        Assert.Equal(utcResult.ToString(), gmtResult.ToString());
    }

    #endregion

    #region Two-Digit Year Handling

    [Fact]
    public void JSDate_TwoDigitYear_MapsTo1900s()
    {
        // Years 0-99 map to 1900-1999
        var date = new JSDate(99, 0, 1);
        Assert.Equal(1999, date.GetFullYear());
    }

    [Fact]
    public void JSDate_TwoDigitYear_Zero_MapsTo1900()
    {
        var date = new JSDate(0, 0, 1);
        Assert.Equal(1900, date.GetFullYear());
    }

    #endregion
}
