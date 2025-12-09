// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace QuickJS
{
    /// <summary>
    /// Represents a JavaScript Date object.
    /// JavaScript dates are stored as milliseconds since Unix epoch (January 1, 1970, 00:00:00 UTC).
    /// </summary>
    public class JSDate : JSObject
    {
        /// <summary>
        /// The internal time value in milliseconds since Unix epoch.
        /// NaN represents an invalid date.
        /// </summary>
        private double _timeValue;

        /// <summary>
        /// Creates a new Date with the current time.
        /// </summary>
        public JSDate()
            : base(null, JSClassId.Date)
        {
            _timeValue = GetCurrentTimeMillis();
        }

        /// <summary>
        /// Creates a new Date with the specified time value.
        /// </summary>
        /// <param name="timeValue">Milliseconds since Unix epoch</param>
        public JSDate(double timeValue)
            : base(null, JSClassId.Date)
        {
            _timeValue = timeValue;
        }

        /// <summary>
        /// Creates a new Date from a DateTime.
        /// </summary>
        public JSDate(DateTime dateTime)
            : base(null, JSClassId.Date)
        {
            _timeValue = DateTimeToMillis(dateTime);
        }

        /// <summary>
        /// Creates a new Date from date components (in local time).
        /// </summary>
        public JSDate(int year, int month, int date = 1, int hours = 0, int minutes = 0, int seconds = 0, int milliseconds = 0)
            : base(null, JSClassId.Date)
        {
            try
            {
                // JavaScript months are 0-based, .NET months are 1-based
                // JavaScript uses two-digit year handling: years 0-99 map to 1900-1999
                int adjustedYear = year;
                if (year >= 0 && year <= 99)
                {
                    adjustedYear = 1900 + year;
                }

                // Clamp month to valid range for initial construction
                int netMonth = month + 1;
                
                // Handle month overflow/underflow
                if (netMonth < 1 || netMonth > 12 || date < 1 || date > 31)
                {
                    // Use DateTime arithmetic to handle overflow
                    var baseDate = new DateTime(adjustedYear, 1, 1, hours, minutes, seconds, milliseconds, DateTimeKind.Local);
                    baseDate = baseDate.AddMonths(month);
                    baseDate = baseDate.AddDays(date - 1);
                    _timeValue = DateTimeToMillis(baseDate);
                }
                else
                {
                    var dt = new DateTime(adjustedYear, netMonth, date, hours, minutes, seconds, milliseconds, DateTimeKind.Local);
                    _timeValue = DateTimeToMillis(dt);
                }
            }
            catch
            {
                _timeValue = double.NaN;
            }
        }

        /// <summary>
        /// Gets the primitive time value in milliseconds since epoch.
        /// Returns NaN for invalid dates.
        /// </summary>
        public double TimeValue => _timeValue;

        /// <summary>
        /// Returns true if this date is invalid (NaN).
        /// </summary>
        public bool IsInvalid => double.IsNaN(_timeValue);

        /// <summary>
        /// Converts to a .NET DateTime. Returns null for invalid dates.
        /// </summary>
        public DateTime? ToDateTime()
        {
            if (IsInvalid) return null;
            return MillisToDateTime(_timeValue);
        }

        // Static methods

        /// <summary>
        /// Returns the current time in milliseconds since epoch.
        /// </summary>
        public static double Now()
        {
            return GetCurrentTimeMillis();
        }

        /// <summary>
        /// Parses a date string and returns milliseconds since epoch.
        /// Returns NaN if the string cannot be parsed.
        /// </summary>
        public static double Parse(string dateString)
        {
            if (string.IsNullOrEmpty(dateString))
                return double.NaN;

            // Try parsing ISO 8601 format first
            if (DateTime.TryParse(dateString, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime result))
            {
                return DateTimeToMillis(result);
            }

            return double.NaN;
        }

        /// <summary>
        /// Returns milliseconds since epoch for the given UTC date components.
        /// </summary>
        public static double UTC(int year, int month, int date = 1, int hours = 0, int minutes = 0, int seconds = 0, int milliseconds = 0)
        {
            try
            {
                // JavaScript uses two-digit year handling
                int adjustedYear = year;
                if (year >= 0 && year <= 99)
                {
                    adjustedYear = 1900 + year;
                }

                // Handle month overflow/underflow
                int netMonth = month + 1;
                if (netMonth < 1 || netMonth > 12 || date < 1 || date > 31)
                {
                    var baseDate = new DateTime(adjustedYear, 1, 1, hours, minutes, seconds, milliseconds, DateTimeKind.Utc);
                    baseDate = baseDate.AddMonths(month);
                    baseDate = baseDate.AddDays(date - 1);
                    return DateTimeToMillis(baseDate);
                }

                var dt = new DateTime(adjustedYear, netMonth, date, hours, minutes, seconds, milliseconds, DateTimeKind.Utc);
                return DateTimeToMillis(dt);
            }
            catch
            {
                return double.NaN;
            }
        }

        // Instance methods - Getters (Local time)

        /// <summary>Gets the year (4 digits for dates between 1000-9999)</summary>
        public int GetFullYear() => IsInvalid ? 0 : ToLocalDateTime().Year;

        /// <summary>Gets the month (0-11)</summary>
        public int GetMonth() => IsInvalid ? 0 : ToLocalDateTime().Month - 1;

        /// <summary>Gets the day of the month (1-31)</summary>
        public int GetDate() => IsInvalid ? 0 : ToLocalDateTime().Day;

        /// <summary>Gets the day of the week (0=Sunday, 6=Saturday)</summary>
        public int GetDay() => IsInvalid ? 0 : (int)ToLocalDateTime().DayOfWeek;

        /// <summary>Gets the hours (0-23)</summary>
        public int GetHours() => IsInvalid ? 0 : ToLocalDateTime().Hour;

        /// <summary>Gets the minutes (0-59)</summary>
        public int GetMinutes() => IsInvalid ? 0 : ToLocalDateTime().Minute;

        /// <summary>Gets the seconds (0-59)</summary>
        public int GetSeconds() => IsInvalid ? 0 : ToLocalDateTime().Second;

        /// <summary>Gets the milliseconds (0-999)</summary>
        public int GetMilliseconds() => IsInvalid ? 0 : ToLocalDateTime().Millisecond;

        /// <summary>Gets the milliseconds since epoch</summary>
        public double GetTime() => _timeValue;

        /// <summary>Gets the timezone offset in minutes</summary>
        public int GetTimezoneOffset()
        {
            if (IsInvalid) return 0;
            var local = ToLocalDateTime();
            var utc = MillisToDateTime(_timeValue);
            return (int)(utc - local).TotalMinutes;
        }

        // Instance methods - Getters (UTC time)

        /// <summary>Gets the UTC year</summary>
        public int GetUTCFullYear() => IsInvalid ? 0 : MillisToDateTime(_timeValue).Year;

        /// <summary>Gets the UTC month (0-11)</summary>
        public int GetUTCMonth() => IsInvalid ? 0 : MillisToDateTime(_timeValue).Month - 1;

        /// <summary>Gets the UTC day of the month (1-31)</summary>
        public int GetUTCDate() => IsInvalid ? 0 : MillisToDateTime(_timeValue).Day;

        /// <summary>Gets the UTC day of the week (0=Sunday)</summary>
        public int GetUTCDay() => IsInvalid ? 0 : (int)MillisToDateTime(_timeValue).DayOfWeek;

        /// <summary>Gets the UTC hours (0-23)</summary>
        public int GetUTCHours() => IsInvalid ? 0 : MillisToDateTime(_timeValue).Hour;

        /// <summary>Gets the UTC minutes (0-59)</summary>
        public int GetUTCMinutes() => IsInvalid ? 0 : MillisToDateTime(_timeValue).Minute;

        /// <summary>Gets the UTC seconds (0-59)</summary>
        public int GetUTCSeconds() => IsInvalid ? 0 : MillisToDateTime(_timeValue).Second;

        /// <summary>Gets the UTC milliseconds (0-999)</summary>
        public int GetUTCMilliseconds() => IsInvalid ? 0 : MillisToDateTime(_timeValue).Millisecond;

        // Instance methods - Setters (Local time)

        /// <summary>Sets the year</summary>
        public double SetFullYear(int year, int? month = null, int? date = null)
        {
            if (IsInvalid) _timeValue = 0;
            var dt = ToLocalDateTime();
            int newMonth = month.HasValue ? month.Value + 1 : dt.Month;
            int newDate = date ?? dt.Day;
            
            // Handle two-digit years
            int adjustedYear = year;
            if (year >= 0 && year <= 99)
            {
                adjustedYear = 1900 + year;
            }

            try
            {
                dt = new DateTime(adjustedYear, newMonth, newDate, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, DateTimeKind.Local);
                _timeValue = DateTimeToMillis(dt);
            }
            catch
            {
                _timeValue = double.NaN;
            }
            return _timeValue;
        }

        /// <summary>Sets the month (0-11)</summary>
        public double SetMonth(int month, int? date = null)
        {
            if (IsInvalid) _timeValue = 0;
            var dt = ToLocalDateTime();
            int newDate = date ?? dt.Day;
            try
            {
                dt = new DateTime(dt.Year, 1, 1, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, DateTimeKind.Local);
                dt = dt.AddMonths(month).AddDays(newDate - 1);
                _timeValue = DateTimeToMillis(dt);
            }
            catch
            {
                _timeValue = double.NaN;
            }
            return _timeValue;
        }

        /// <summary>Sets the day of the month</summary>
        public double SetDate(int date)
        {
            if (IsInvalid) _timeValue = 0;
            var dt = ToLocalDateTime();
            try
            {
                dt = new DateTime(dt.Year, dt.Month, 1, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, DateTimeKind.Local);
                dt = dt.AddDays(date - 1);
                _timeValue = DateTimeToMillis(dt);
            }
            catch
            {
                _timeValue = double.NaN;
            }
            return _timeValue;
        }

        /// <summary>Sets the hours, and optionally minutes, seconds, milliseconds</summary>
        public double SetHours(int hours, int? minutes = null, int? seconds = null, int? milliseconds = null)
        {
            if (IsInvalid) _timeValue = 0;
            var dt = ToLocalDateTime();
            try
            {
                dt = new DateTime(dt.Year, dt.Month, dt.Day,
                    hours,
                    minutes ?? dt.Minute,
                    seconds ?? dt.Second,
                    milliseconds ?? dt.Millisecond,
                    DateTimeKind.Local);
                _timeValue = DateTimeToMillis(dt);
            }
            catch
            {
                _timeValue = double.NaN;
            }
            return _timeValue;
        }

        /// <summary>Sets the minutes</summary>
        public double SetMinutes(int minutes, int? seconds = null, int? milliseconds = null)
        {
            if (IsInvalid) _timeValue = 0;
            var dt = ToLocalDateTime();
            try
            {
                dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour,
                    minutes,
                    seconds ?? dt.Second,
                    milliseconds ?? dt.Millisecond,
                    DateTimeKind.Local);
                _timeValue = DateTimeToMillis(dt);
            }
            catch
            {
                _timeValue = double.NaN;
            }
            return _timeValue;
        }

        /// <summary>Sets the seconds</summary>
        public double SetSeconds(int seconds, int? milliseconds = null)
        {
            if (IsInvalid) _timeValue = 0;
            var dt = ToLocalDateTime();
            try
            {
                dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute,
                    seconds,
                    milliseconds ?? dt.Millisecond,
                    DateTimeKind.Local);
                _timeValue = DateTimeToMillis(dt);
            }
            catch
            {
                _timeValue = double.NaN;
            }
            return _timeValue;
        }

        /// <summary>Sets the milliseconds</summary>
        public double SetMilliseconds(int milliseconds)
        {
            if (IsInvalid) _timeValue = 0;
            var dt = ToLocalDateTime();
            try
            {
                dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second,
                    milliseconds,
                    DateTimeKind.Local);
                _timeValue = DateTimeToMillis(dt);
            }
            catch
            {
                _timeValue = double.NaN;
            }
            return _timeValue;
        }

        /// <summary>Sets the time value directly</summary>
        public double SetTime(double time)
        {
            _timeValue = time;
            return _timeValue;
        }

        // Instance methods - Setters (UTC time)

        /// <summary>Sets the UTC year</summary>
        public double SetUTCFullYear(int year, int? month = null, int? date = null)
        {
            if (IsInvalid) _timeValue = 0;
            var dt = MillisToDateTime(_timeValue);
            int newMonth = month.HasValue ? month.Value + 1 : dt.Month;
            int newDate = date ?? dt.Day;
            
            int adjustedYear = year;
            if (year >= 0 && year <= 99)
            {
                adjustedYear = 1900 + year;
            }

            try
            {
                dt = new DateTime(adjustedYear, newMonth, newDate, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, DateTimeKind.Utc);
                _timeValue = DateTimeToMillis(dt);
            }
            catch
            {
                _timeValue = double.NaN;
            }
            return _timeValue;
        }

        /// <summary>Sets the UTC month</summary>
        public double SetUTCMonth(int month, int? date = null)
        {
            if (IsInvalid) _timeValue = 0;
            var dt = MillisToDateTime(_timeValue);
            int newDate = date ?? dt.Day;
            try
            {
                dt = new DateTime(dt.Year, 1, 1, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, DateTimeKind.Utc);
                dt = dt.AddMonths(month).AddDays(newDate - 1);
                _timeValue = DateTimeToMillis(dt);
            }
            catch
            {
                _timeValue = double.NaN;
            }
            return _timeValue;
        }

        /// <summary>Sets the UTC day of month</summary>
        public double SetUTCDate(int date)
        {
            if (IsInvalid) _timeValue = 0;
            var dt = MillisToDateTime(_timeValue);
            try
            {
                dt = new DateTime(dt.Year, dt.Month, 1, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, DateTimeKind.Utc);
                dt = dt.AddDays(date - 1);
                _timeValue = DateTimeToMillis(dt);
            }
            catch
            {
                _timeValue = double.NaN;
            }
            return _timeValue;
        }

        /// <summary>Sets the UTC hours</summary>
        public double SetUTCHours(int hours, int? minutes = null, int? seconds = null, int? milliseconds = null)
        {
            if (IsInvalid) _timeValue = 0;
            var dt = MillisToDateTime(_timeValue);
            try
            {
                dt = new DateTime(dt.Year, dt.Month, dt.Day,
                    hours,
                    minutes ?? dt.Minute,
                    seconds ?? dt.Second,
                    milliseconds ?? dt.Millisecond,
                    DateTimeKind.Utc);
                _timeValue = DateTimeToMillis(dt);
            }
            catch
            {
                _timeValue = double.NaN;
            }
            return _timeValue;
        }

        /// <summary>Sets the UTC minutes</summary>
        public double SetUTCMinutes(int minutes, int? seconds = null, int? milliseconds = null)
        {
            if (IsInvalid) _timeValue = 0;
            var dt = MillisToDateTime(_timeValue);
            try
            {
                dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour,
                    minutes,
                    seconds ?? dt.Second,
                    milliseconds ?? dt.Millisecond,
                    DateTimeKind.Utc);
                _timeValue = DateTimeToMillis(dt);
            }
            catch
            {
                _timeValue = double.NaN;
            }
            return _timeValue;
        }

        /// <summary>Sets the UTC seconds</summary>
        public double SetUTCSeconds(int seconds, int? milliseconds = null)
        {
            if (IsInvalid) _timeValue = 0;
            var dt = MillisToDateTime(_timeValue);
            try
            {
                dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute,
                    seconds,
                    milliseconds ?? dt.Millisecond,
                    DateTimeKind.Utc);
                _timeValue = DateTimeToMillis(dt);
            }
            catch
            {
                _timeValue = double.NaN;
            }
            return _timeValue;
        }

        /// <summary>Sets the UTC milliseconds</summary>
        public double SetUTCMilliseconds(int milliseconds)
        {
            if (IsInvalid) _timeValue = 0;
            var dt = MillisToDateTime(_timeValue);
            try
            {
                dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second,
                    milliseconds,
                    DateTimeKind.Utc);
                _timeValue = DateTimeToMillis(dt);
            }
            catch
            {
                _timeValue = double.NaN;
            }
            return _timeValue;
        }

        // Conversion methods

        /// <summary>Returns the date portion as a string</summary>
        public string ToDateString()
        {
            if (IsInvalid) return "Invalid Date";
            return ToLocalDateTime().ToString("ddd MMM dd yyyy", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Returns the time portion as a string</summary>
        public string ToTimeString()
        {
            if (IsInvalid) return "Invalid Date";
            var dt = ToLocalDateTime();
            var offset = TimeZoneInfo.Local.GetUtcOffset(dt);
            var sign = offset < TimeSpan.Zero ? "-" : "+";
            var absOffset = offset < TimeSpan.Zero ? -offset : offset;
            return dt.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) +
                   $" GMT{sign}{absOffset.Hours:D2}{absOffset.Minutes:D2}";
        }

        /// <summary>Returns the date in ISO 8601 format</summary>
        public string ToISOString()
        {
            if (IsInvalid) throw new JSRangeError("Invalid time value");
            return MillisToDateTime(_timeValue).ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Returns the date in UTC string format</summary>
        public string ToUTCString()
        {
            if (IsInvalid) return "Invalid Date";
            return MillisToDateTime(_timeValue).ToString("ddd, dd MMM yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " GMT";
        }

        /// <summary>Returns a JSON representation (ISO string)</summary>
        public string ToJSON()
        {
            if (IsInvalid) return "null";
            return ToISOString();
        }

        /// <summary>Returns the primitive value (time in milliseconds)</summary>
        public double ValueOf() => _timeValue;

        /// <summary>Returns a string representation</summary>
        public override string ToString()
        {
            if (IsInvalid) return "Invalid Date";
            return ToDateString() + " " + ToTimeString();
        }

        // Helper methods

        private DateTime ToLocalDateTime()
        {
            return MillisToDateTime(_timeValue).ToLocalTime();
        }

        private static double GetCurrentTimeMillis()
        {
            return DateTimeToMillis(DateTime.UtcNow);
        }

        private static double DateTimeToMillis(DateTime dt)
        {
            // Convert to UTC if not already
            if (dt.Kind == DateTimeKind.Local)
            {
                dt = dt.ToUniversalTime();
            }
            else if (dt.Kind == DateTimeKind.Unspecified)
            {
                dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }

            // Unix epoch
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (dt - epoch).TotalMilliseconds;
        }

        private static DateTime MillisToDateTime(double millis)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddMilliseconds(millis);
        }
    }
}
