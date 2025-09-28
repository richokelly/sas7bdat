using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace Sas7Bdat.Core.Serializers;

/// <summary>
/// Provides utilities for type inference and serializer creation for SAS column data types.
/// </summary>
/// <remarks>
/// FieldSerializers is a comprehensive utility class that handles the complex task of mapping
/// SAS format specifications to appropriate .NET data types and creating the corresponding
/// serialization infrastructure. The class includes both public utilities for type inference
/// and internal serializer implementations for different SAS data formats.
/// 
/// **Key Responsibilities:**
/// <list type="bullet">
/// <item><description>Analyze SAS format strings to infer logical column types</description></item>
/// <item><description>Create appropriate IDataSerializer instances for different data types</description></item>
/// <item><description>Handle SAS date/time conversions with proper epoch and range validation</description></item>
/// <item><description>Provide robust binary data parsing for various numeric formats</description></item>
/// </list>
/// 
/// **SAS Date/Time System:**
/// SAS uses January 1, 1960 as its epoch for date and time calculations, which differs
/// from .NET's DateTime system. The class handles conversions between these systems
/// while maintaining precision and handling edge cases.
/// 
/// **Format String Analysis:**
/// The class performs sophisticated analysis of SAS format strings to determine
/// the appropriate .NET data type, considering:
/// <list type="bullet">
/// <item><description>Format name patterns (DATE, TIME, DATETIME, etc.)</description></item>
/// <item><description>ISO 8601 format families (B8601, E8601, IS8601)</description></item>
/// <item><description>Storage type constraints (String vs Number)</description></item>
/// <item><description>Column length considerations for type disambiguation</description></item>
/// </list>
/// </remarks>
internal static class FieldSerializers
{
    /// <summary>
    /// The SAS epoch date (January 1, 1960 00:00:00 UTC) used as the base for SAS date/time calculations.
    /// </summary>
    /// <remarks>
    /// SAS represents dates and times as numeric values relative to this epoch:
    /// <list type="bullet">
    /// <item><description>Dates: Number of days since/before the epoch</description></item>
    /// <item><description>DateTime: Number of seconds since/before the epoch</description></item>
    /// <item><description>Time: Number of seconds from midnight (independent of epoch)</description></item>
    /// </list>
    /// 
    /// This differs from .NET's DateTime epoch (January 1, 0001) and Unix epoch (January 1, 1970),
    /// requiring careful conversion to maintain accuracy across the supported date range.
    /// </remarks>
    private static readonly DateTime Epoch1960 = new(1960, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// The minimum number of days from the SAS epoch to the .NET DateTime minimum value.
    /// </summary>
    /// <remarks>
    /// This value represents the range boundary for date calculations and is used to
    /// validate that converted dates fall within the valid .NET DateTime range.
    /// Values outside this range are treated as missing data to prevent overflow exceptions.
    /// </remarks>
    private static readonly double DaysMin1960;

    /// <summary>
    /// The maximum number of days from the SAS epoch to the .NET DateTime maximum value.
    /// </summary>
    /// <remarks>
    /// This value represents the upper range boundary for date calculations and ensures
    /// that converted dates don't exceed .NET DateTime capabilities. The calculation
    /// includes fractional components to handle the maximum precision of DateTime.
    /// </remarks>
    private static readonly double DaysMax1960;

    /// <summary>
    /// The minimum number of seconds from the SAS epoch to the .NET DateTime minimum value.
    /// </summary>
    /// <remarks>
    /// Used for validating datetime and time conversions to ensure they fall within
    /// the valid .NET DateTime range when converted from SAS numeric representations.
    /// </remarks>
    private static readonly double SecsMin1960;

    /// <summary>
    /// The maximum number of seconds from the SAS epoch to the .NET DateTime maximum value.
    /// </summary>
    /// <remarks>
    /// Provides the upper bound for datetime and time validation, ensuring conversions
    /// don't exceed .NET DateTime capabilities. The value accounts for the maximum
    /// precision available in DateTime including ticks.
    /// </remarks>
    private static readonly double SecsMax1960;

    /// <summary>
    /// Initializes the static range validation constants for SAS date/time conversions.
    /// </summary>
    /// <remarks>
    /// This static constructor calculates the valid range boundaries for SAS date/time
    /// conversions by determining the minimum and maximum DateTime values in terms of
    /// days and seconds from the SAS epoch.
    /// 
    /// **Range Calculations:**
    /// <list type="bullet">
    /// <item><description>Minimum: DateTime(1, 1, 1) to handle the earliest possible date</description></item>
    /// <item><description>Maximum: DateTime(9999, 12, 31, 23, 59, 59, 999) + 9999 ticks for absolute precision</description></item>
    /// </list>
    /// 
    /// These values are used throughout the serializers to validate that converted
    /// dates and times fall within valid .NET DateTime ranges, preventing overflow
    /// exceptions and ensuring data integrity.
    /// </remarks>
    static FieldSerializers()
    {
        var min = new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var max = new DateTime(9999, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc).AddTicks(9999); // absolute max

        DaysMin1960 = (min - Epoch1960).TotalDays;
        DaysMax1960 = (max - Epoch1960).TotalDays;

        SecsMin1960 = (min - Epoch1960).TotalSeconds;
        SecsMax1960 = (max - Epoch1960).TotalSeconds;
    }

    /// <summary>
    /// Infers the logical column type from storage type, format specification, and column length.
    /// </summary>
    /// <param name="storage">The physical storage type of the column (String or Number).</param>
    /// <param name="format">The SAS format specification string, or null if not specified.</param>
    /// <param name="length">The length of the column in bytes.</param>
    /// <returns>
    /// The inferred ColumnType based on the analysis of the input parameters.
    /// </returns>
    /// <remarks>
    /// This method performs sophisticated type inference by analyzing multiple factors:
    /// 
    /// **Storage Type Constraints:**
    /// <list type="bullet">
    /// <item><description>String storage → ColumnType.String (definitive)</description></item>
    /// <item><description>Non-Number storage → ColumnType.Unknown</description></item>
    /// <item><description>Number storage → Further analysis based on format and length</description></item>
    /// </list>
    /// 
    /// **Format String Analysis:**
    /// The method analyzes normalized format strings for specific patterns:
    /// 
    /// **ISO 8601 Format Families:**
    /// <list type="bullet">
    /// <item><description>B8601DT/DZ, E8601DT/DZ, IS8601DT/DZ → DateTime</description></item>
    /// <item><description>B8601TM/TN, E8601TM/TN, IS8601TM/TN → Time</description></item>
    /// <item><description>B8601DA/DN, E8601DA/DN, IS8601DA/DN → Date</description></item>
    /// </list>
    /// 
    /// **Classic SAS Format Families:**
    /// <list type="bullet">
    /// <item><description>DATETIME* → DateTime</description></item>
    /// <item><description>TIME*, HHMM*, HMS*, etc. → Time</description></item>
    /// <item><description>DATE*, DAY*, YYMMDD*, etc. → Date</description></item>
    /// </list>
    /// 
    /// **Length-Based Validation:**
    /// <list type="bullet">
    /// <item><description>Lengths 0-2: Always treated as Number regardless of format</description></item>
    /// <item><description>Length 3+: Format analysis applies</description></item>
    /// </list>
    /// 
    /// **Fallback Patterns:**
    /// If specific patterns aren't found, the method checks for suffix patterns
    /// (DT, DZ, TM, TN, DA, DN) as a secondary identification method.
    /// 
    /// **Default Behavior:**
    /// When no date/time patterns are detected, numeric storage defaults to ColumnType.Number.
    /// </remarks>
    /// <example>
    /// <code>
    /// // String storage is always string type
    /// var stringType = FieldSerializers.InferKind(StorageType.String, "$CHAR20.", 20);
    /// // Returns: ColumnType.String
    /// 
    /// // Numeric storage with date format
    /// var dateType = FieldSerializers.InferKind(StorageType.Number, "DATE9.", 8);
    /// // Returns: ColumnType.Date
    /// 
    /// // Numeric storage with datetime format
    /// var datetimeType = FieldSerializers.InferKind(StorageType.Number, "DATETIME20.", 8);
    /// // Returns: ColumnType.DateTime
    /// 
    /// // Numeric storage without specific format
    /// var numberType = FieldSerializers.InferKind(StorageType.Number, null, 8);
    /// // Returns: ColumnType.Number
    /// </code>
    /// </example>
    public static ColumnType InferKind(StorageType storage, string? format, int length)
    {
        if (storage == StorageType.String) return ColumnType.String;
        if (storage != StorageType.Number) return ColumnType.Unknown;

        var normalisedFormat = NormaliseFormat(format);
        if (normalisedFormat.Length == 0) return ColumnType.Number;

        if (length == 0) return ColumnType.Number;
        if (length == 1) return ColumnType.Number;
        if (length == 2) return ColumnType.Number;

        // ISO 8601 families (B8601 / E8601 / IS8601)
        // *DT, *DZ → datetime (seconds)
        if (StartsWithAny(normalisedFormat, ["B8601DT", "E8601DT", "IS8601DT", "B8601DZ", "E8601DZ", "IS8601DZ"]))
            return ColumnType.DateTime;

        // *TM (time) and *TN (time-from-datetime) → time (seconds either way)
        if (StartsWithAny(normalisedFormat, ["B8601TM", "E8601TM", "IS8601TM", "B8601TN", "E8601TN", "IS8601TN", "E8601LZ"]))
            return ColumnType.Time;

        // *DA (date-from-date), *DN (date-from-datetime)
        if (StartsWithAny(normalisedFormat, ["B8601DA", "E8601DA", "IS8601DA", "B8601DN", "E8601DN", "IS8601DN"]))
            return ColumnType.Date;

        // Classic SAS families
        if (ContainsAny(normalisedFormat, ["DATETIME"])) return ColumnType.DateTime;
        if (StartsWithAny(normalisedFormat, ["TIME", "HHMM", "MMSS", "HMS", "TIMEAMPM", "HOUR", "MINUTE", "SECOND"]))
            return ColumnType.Time;

        if (StartsWithAny(normalisedFormat, ["DATE",
            "DAY",
            "YYMMDD",
            "MMDDYY",
            "DDMMYY",
            "JULIAN",
            "JULDAY",
            "MONYY",
            "MMYY",
            "YYMM",
            "MONNAME",
            "MONTH",
            "WEEKDAT",
            "WORDDAT",
            "EURDF",
            "NLDAT",
            "YYQ",
            "YYMON",
            "YEAR",
            "WEEK",
            "QTR",
            "QUARTER",
            "DOWNAME"]))
            return ColumnType.Date;

        // Suffix fallbacks (guarded)
        if (StartsWithAny(normalisedFormat, ["DT"]) || EndsWithAny(normalisedFormat, ["DT", "DZ"])) return ColumnType.DateTime;
        if (EndsWithAny(normalisedFormat, ["TM", "TN"])) return ColumnType.Time;
        if (EndsWithAny(normalisedFormat, ["DA", "DN"])) return ColumnType.Date;

        return ColumnType.Number;
    }

    /// <summary>
    /// Creates an appropriate IDataSerializer instance for the specified column characteristics.
    /// </summary>
    /// <param name="kind">The logical column type determined by type inference.</param>
    /// <param name="format">The SAS format specification string.</param>
    /// <param name="endian">The endianness for reading binary numeric data.</param>
    /// <param name="encoding">The text encoding for string data conversion.</param>
    /// <returns>
    /// An IDataSerializer instance configured for the specified column type and characteristics.
    /// </returns>
    /// <remarks>
    /// This factory method creates specialized serializer instances based on the inferred
    /// column type and format specifications. Each serializer is optimized for its specific
    /// data conversion requirements.
    /// 
    /// **Serializer Selection:**
    /// <list type="bullet">
    /// <item><description>Date: SasDateFromSecondsSerializer or SasDateFromDaysSerializer based on format</description></item>
    /// <item><description>DateTime: SasDateTimeSerializer for epoch-based datetime conversion</description></item>
    /// <item><description>Time: SasTimeSerializer for duration/time-of-day conversion</description></item>
    /// <item><description>String: SasStringSerializer with appropriate encoding</description></item>
    /// <item><description>Default: SasDoubleSerializer for numeric data</description></item>
    /// </list>
    /// 
    /// **Date Format Specialization:**
    /// Date columns use different serializers based on their storage format:
    /// <list type="bullet">
    /// <item><description>Date-from-datetime formats (DN variants): Store dates as seconds, extract date component</description></item>
    /// <item><description>Date-from-date formats (standard): Store dates as day counts from epoch</description></item>
    /// </list>
    /// 
    /// **Configuration Parameters:**
    /// <list type="bullet">
    /// <item><description>Endianness: Required for all numeric serializers to correctly interpret binary data</description></item>
    /// <item><description>Encoding: Required for string serializers to handle character conversion</description></item>
    /// <item><description>Format: Used to determine specialized date handling requirements</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create serializer for a date column
    /// var dateSerializer = FieldSerializers.GetSerializer(
    ///     ColumnType.Date, "DATE9.", Endian.Little, Encoding.UTF8);
    /// 
    /// // Create serializer for a string column
    /// var stringSerializer = FieldSerializers.GetSerializer(
    ///     ColumnType.String, "$CHAR20.", Endian.Little, Encoding.GetEncoding("windows-1252"));
    /// 
    /// // Create serializer for a numeric column
    /// var numericSerializer = FieldSerializers.GetSerializer(
    ///     ColumnType.Number, "F8.2", Endian.Big, Encoding.UTF8);
    /// </code>
    /// </example>
    public static IDataSerializer GetSerializer(ColumnType kind, string? format, Endian endian, Encoding encoding)
    {
        return kind switch
        {
            ColumnType.Date => IsSasDateFromDatetime(NormaliseFormat(format)) ? new SasDateFromSecondsSerializer(endian) : new SasDateFromDaysSerializer(endian),
            ColumnType.DateTime => new SasDateTimeSerializer(endian),
            ColumnType.Time => new SasTimeSerializer(endian),
            ColumnType.String => new SasStringSerializer(encoding),
            _ => new SasDoubleSerializer(endian),
        };
    }

    /// <summary>
    /// Converts a SAS datetime value (seconds since 1960 epoch) to a .NET DateTime.
    /// </summary>
    /// <param name="d">The SAS datetime value as seconds since January 1, 1960, or null for missing values.</param>
    /// <returns>
    /// A DateTime representing the converted value, or null if the input is null or outside valid range.
    /// </returns>
    /// <remarks>
    /// This internal utility method handles the conversion from SAS datetime representation
    /// to .NET DateTime objects. The method includes robust validation and error handling:
    /// 
    /// **Conversion Process:**
    /// <list type="number">
    /// <item><description>Check for null input (missing data)</description></item>
    /// <item><description>Round the seconds value to nearest integer</description></item>
    /// <item><description>Validate against minimum and maximum range boundaries</description></item>
    /// <item><description>Add seconds to the SAS epoch to get the final DateTime</description></item>
    /// </list>
    /// 
    /// **Range Validation:**
    /// Values outside the valid .NET DateTime range are treated as missing data
    /// to prevent overflow exceptions and maintain data integrity.
    /// 
    /// **Rounding Behavior:**
    /// Fractional seconds are rounded to the nearest integer using MidpointRounding.AwayFromZero
    /// for consistent and predictable behavior across different platforms.
    /// </remarks>
    internal static object? ConvertSasDateTimeSeconds(double? d)
    {
        if (d == null) return null;
        var r = RoundToInt(d.GetValueOrDefault());
        if (r < SecsMin1960 || r > SecsMax1960) return null;
        return Epoch1960.AddSeconds(r);
    }

    /// <summary>
    /// Rounds a double value to the nearest integer using away-from-zero midpoint rounding.
    /// </summary>
    /// <param name="x">The double value to round.</param>
    /// <returns>The rounded value as a double.</returns>
    /// <remarks>
    /// This method provides consistent rounding behavior for date/time conversions.
    /// The AwayFromZero rounding mode ensures predictable results when dealing with
    /// midpoint values (e.g., 2.5 rounds to 3, -2.5 rounds to -3).
    /// 
    /// Aggressive inlining ensures this frequently-called utility has minimal overhead.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double RoundToInt(double x) => Math.Round(x, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Normalizes a SAS format string by removing width/precision specifications and converting to uppercase.
    /// </summary>
    /// <param name="format">The raw SAS format string to normalize.</param>
    /// <returns>The normalized format string with width/precision removed and converted to uppercase.</returns>
    /// <remarks>
    /// This method standardizes format strings for pattern matching by:
    /// <list type="number">
    /// <item><description>Trimming whitespace and converting to uppercase</description></item>
    /// <item><description>Removing trailing digits, dots, and commas (width/precision specifications)</description></item>
    /// </list>
    /// 
    /// **Examples:**
    /// <list type="bullet">
    /// <item><description>"DATETIME19." → "DATETIME"</description></item>
    /// <item><description>"YYMMDD10." → "YYMMDD"</description></item>
    /// <item><description>"E8601DT25.3" → "E8601DT"</description></item>
    /// </list>
    /// 
    /// This normalization enables consistent pattern matching regardless of the specific
    /// width or precision specified in the original format.
    /// </remarks>
    private static string NormaliseFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format)) return string.Empty;
        var f = format.Trim().ToUpperInvariant().AsSpan();

        // Strip width/precision suffix (e.g., DATETIME19., YYMMDD10., E8601DT25.3)
        // Remove trailing digits, dots, or commas.
        var cut = f.Length;
        while (cut > 0)
        {
            var c = f[cut - 1];
            if (c is >= '0' and <= '9' or '.' or ',') cut--;
            else break;
        }

        if (cut < f.Length) f = f[..cut];
        return new string(f);
    }

    /// <summary>
    /// Determines if a date format represents date-from-datetime (seconds-based) storage.
    /// </summary>
    /// <param name="f">The normalized format string to check.</param>
    /// <returns>true if the format uses seconds-based date storage; otherwise, false.</returns>
    /// <remarks>
    /// Date-from-datetime formats (DN variants) store date values as datetime seconds
    /// and extract only the date component during conversion. This differs from standard
    /// date formats that store dates as day counts from the epoch.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsSasDateFromDatetime(string f)
        => StartsWithAny(f, ["B8601DN", "E8601DN", "IS8601DN"]);

    /// <summary>
    /// Checks if a string starts with any of the specified prefixes.
    /// </summary>
    /// <param name="s">The string to check.</param>
    /// <param name="prefixes">The prefixes to test against.</param>
    /// <returns>true if the string starts with any of the prefixes; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool StartsWithAny(string s, Span<string> prefixes)
    {
        var span = s.AsSpan();
        foreach (var p in prefixes)
        {
            if (span.StartsWith(p, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a string ends with any of the specified suffixes.
    /// </summary>
    /// <param name="s">The string to check.</param>
    /// <param name="suffixes">The suffixes to test against.</param>
    /// <returns>true if the string ends with any of the suffixes; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool EndsWithAny(string s, Span<string> suffixes)
    {
        var span = s.AsSpan();
        foreach (var p in suffixes)
        {
            if (span.EndsWith(p, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a string contains any of the specified substrings.
    /// </summary>
    /// <param name="s">The string to check.</param>
    /// <param name="needles">The substrings to search for.</param>
    /// <returns>true if the string contains any of the substrings; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ContainsAny(string s, Span<string> needles)
    {
        var span = s.AsSpan();
        foreach (var n in needles)
        {
            if (span.Contains(n, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Provides base functionality for all numeric data serializers.
    /// </summary>
    /// <param name="endian">The endianness to use for reading binary numeric data.</param>
    /// <remarks>
    /// This abstract base class handles the complex task of extracting numeric values
    /// from SAS binary data, which can have various lengths and formats. The class
    /// provides robust parsing for different numeric storage formats while handling
    /// missing data represented as NaN values.
    /// 
    /// **Supported Formats:**
    /// <list type="bullet">
    /// <item><description>8-byte IEEE 754 double precision values</description></item>
    /// <item><description>1-byte unsigned integer values</description></item>
    /// <item><description>2-byte signed integer values</description></item>
    /// <item><description>3-7 byte incomplete double values with zero-padding</description></item>
    /// </list>
    /// 
    /// **Missing Data Handling:**
    /// NaN values in the binary data are converted to null in .NET, providing
    /// consistent missing data representation across all numeric types.
    /// </remarks>
    private abstract class NumericSerializer(Endian endian) : IDataSerializer
    {
        /// <summary>
        /// Extracts a double value from binary data, handling various storage formats.
        /// </summary>
        /// <param name="data">The binary data containing the numeric value.</param>
        /// <returns>The extracted double value, or null if the data represents missing/invalid data.</returns>
        /// <remarks>
        /// This method handles multiple binary formats for numeric data:
        /// 
        /// **8-byte format:** Standard IEEE 754 double precision
        /// **1-byte format:** Unsigned byte value (0-255)
        /// **2-byte format:** Signed 16-bit integer
        /// **3-7 bytes:** Incomplete double with appropriate zero-padding
        /// **Other lengths:** Treated as invalid data
        /// 
        /// NaN values are converted to null to represent missing data consistently.
        /// </remarks>
        protected double? ExtractDouble(ReadOnlySpan<byte> data)
        {
            var length = data.Length;
            if (length == 8)
            {
                var value = ReadInt64(data);
                var result = BitConverter.Int64BitsToDouble(value);
                return double.IsNaN(result) ? null : result;
            }

            if (length == 1) return data[0];

            if (length == 2) return ReadInt16(data);

            if (length < 8)
            {
                var result = ReadIncompleteDouble(data, length);
                return double.IsNaN(result) ? null : result;
            }

            return null;
        }

        /// <summary>
        /// Reads a 16-bit signed integer using the appropriate endianness.
        /// </summary>
        /// <param name="data">The 2-byte data to read.</param>
        /// <returns>The extracted 16-bit signed integer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private short ReadInt16(ReadOnlySpan<byte> data)
        {
            return endian == Endian.Big
                ? BinaryPrimitives.ReadInt16BigEndian(data)
                : BinaryPrimitives.ReadInt16LittleEndian(data);
        }

        /// <summary>
        /// Reads a 64-bit signed integer using the appropriate endianness.
        /// </summary>
        /// <param name="data">The 8-byte data to read.</param>
        /// <returns>The extracted 64-bit signed integer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long ReadInt64(ReadOnlySpan<byte> data)
        {
            var result = endian == Endian.Big
                ? BinaryPrimitives.ReadInt64BigEndian(data)
                : BinaryPrimitives.ReadInt64LittleEndian(data);

            return result;
        }

        /// <summary>
        /// Reads an incomplete double value by zero-padding to 8 bytes.
        /// </summary>
        /// <param name="data">The partial binary data (3-7 bytes).</param>
        /// <param name="bytes">The actual length of the data.</param>
        /// <returns>The reconstructed double value.</returns>
        /// <remarks>
        /// This method handles SAS numeric data that is stored in less than 8 bytes
        /// by appropriately zero-padding based on endianness:
        /// <list type="bullet">
        /// <item><description>Big endian: Pad at the end (least significant bytes)</description></item>
        /// <item><description>Little endian: Natural positioning with zero extension</description></item>
        /// </list>
        /// </remarks>
        private double ReadIncompleteDouble(ReadOnlySpan<byte> data, int bytes)
        {
            Span<byte> buffer = stackalloc byte[8];

            if (endian == Endian.Big)
            {
                // For big endian, copy to beginning and zero-fill the rest
                data.CopyTo(buffer);
                for (var i = bytes; i < 8; i++)
                    buffer[i] = 0;
            }
            else
            {
                // For little endian, zero-fill beginning, then copy data
                data.CopyTo(buffer);
            }

            var value = endian == Endian.Big
                ? BinaryPrimitives.ReadInt64BigEndian(buffer)
                : BinaryPrimitives.ReadInt64LittleEndian(buffer);

            return BitConverter.Int64BitsToDouble(value);
        }

        /// <summary>
        /// When implemented by derived classes, converts binary data to the appropriate .NET type.
        /// </summary>
        /// <param name="bytes">The binary data to deserialize.</param>
        /// <returns>The converted value or null for missing data.</returns>
        public abstract object? Deserialize(ReadOnlySpan<byte> bytes);
    }

    /// <summary>
    /// Serializer for SAS string data with encoding conversion and whitespace handling.
    /// </summary>
    /// <param name="encoding">The text encoding to use for string conversion.</param>
    /// <remarks>
    /// This serializer handles the conversion of SAS character data to .NET strings,
    /// including proper handling of null termination, space padding, and encoding conversion.
    /// </remarks>
    private class SasStringSerializer(Encoding encoding) : IDataSerializer
    {
        /// <summary>
        /// Converts binary character data to a .NET string with proper trimming.
        /// </summary>
        /// <param name="data">The binary character data.</param>
        /// <returns>The converted and trimmed string, or empty string for whitespace-only data.</returns>
        /// <remarks>
        /// The conversion process includes:
        /// <list type="number">
        /// <item><description>Trim trailing null bytes and spaces</description></item>
        /// <item><description>Trim leading spaces</description></item>
        /// <item><description>Convert remaining bytes using the specified encoding</description></item>
        /// <item><description>Return empty string if no meaningful content remains</description></item>
        /// </list>
        /// </remarks>
        public object? Deserialize(ReadOnlySpan<byte> data)
        {
            var start = 0;
            var end = data.Length;

            while (end > 0 && (data[end - 1] == 0 || data[end - 1] == 32))
                end--;

            if (end == 0)
                return string.Empty;

            while (start < end && data[start] == 32)
                start++;

            return start >= end ? string.Empty : encoding.GetString(data[start..(end - start)]);
        }
    }

    /// <summary>
    /// Serializer for standard SAS numeric data.
    /// </summary>
    /// <param name="endian">The endianness for reading binary data.</param>
    private class SasDoubleSerializer(Endian endian) : NumericSerializer(endian)
    {
        /// <summary>
        /// Deserializes binary data to a double value.
        /// </summary>
        /// <param name="data">The binary numeric data.</param>
        /// <returns>The extracted double value or null for missing data.</returns>
        public override object? Deserialize(ReadOnlySpan<byte> data) => ExtractDouble(data);
    }

    /// <summary>
    /// Serializer for SAS time data (duration values).
    /// </summary>
    /// <param name="endian">The endianness for reading binary data.</param>
    private class SasTimeSerializer(Endian endian) : NumericSerializer(endian)
    {
        /// <summary>
        /// Deserializes binary data to a TimeSpan representing duration.
        /// </summary>
        /// <param name="bytes">The binary time data as seconds.</param>
        /// <returns>A TimeSpan representing the duration, or null for missing data.</returns>
        public override object? Deserialize(ReadOnlySpan<byte> bytes)
        {
            var d = ExtractDouble(bytes);
            return d == null ? null : TimeSpan.FromSeconds(RoundToInt(d.GetValueOrDefault()));
        }
    }

    /// <summary>
    /// Serializer for SAS datetime data (date and time combined).
    /// </summary>
    /// <param name="endian">The endianness for reading binary data.</param>
    private class SasDateTimeSerializer(Endian endian) : NumericSerializer(endian)
    {
        /// <summary>
        /// Deserializes binary data to a DateTime with full date and time information.
        /// </summary>
        /// <param name="bytes">The binary datetime data as seconds since SAS epoch.</param>
        /// <returns>A DateTime representing the date and time, or null for missing/invalid data.</returns>
        public override object? Deserialize(ReadOnlySpan<byte> bytes)
        {
            var d = ExtractDouble(bytes);
            if (d == null) return null;
            var r = RoundToInt(d.GetValueOrDefault());
            if (r < SecsMin1960 || r > SecsMax1960) return null;
            return Epoch1960.AddSeconds(r);
        }
    }

    /// <summary>
    /// Serializer for SAS date data stored as datetime seconds with date-only extraction.
    /// </summary>
    /// <param name="endian">The endianness for reading binary data.</param>
    private class SasDateFromSecondsSerializer(Endian endian) : NumericSerializer(endian)
    {
        /// <summary>
        /// Deserializes binary datetime data to a date-only DateTime.
        /// </summary>
        /// <param name="bytes">The binary datetime data as seconds since SAS epoch.</param>
        /// <returns>A DateTime with only date information (time set to midnight), or null for missing/invalid data.</returns>
        public override object? Deserialize(ReadOnlySpan<byte> bytes)
        {
            var d = ExtractDouble(bytes);
            if (d == null) return null;
            var r = RoundToInt(d.GetValueOrDefault());
            if (r < SecsMin1960 || r > SecsMax1960) return null;
            var dt = Epoch1960.AddSeconds(r);
            return new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Utc);
        }
    }

    /// <summary>
    /// Serializer for SAS date data stored as day counts from epoch.
    /// </summary>
    /// <param name="endian">The endianness for reading binary data.</param>
    private class SasDateFromDaysSerializer(Endian endian) : NumericSerializer(endian)
    {
        /// <summary>
        /// Deserializes binary date data to a DateTime.
        /// </summary>
        /// <param name="bytes">The binary date data as days since SAS epoch.</param>
        /// <returns>A DateTime representing the date, or null for missing/invalid data.</returns>
        public override object? Deserialize(ReadOnlySpan<byte> bytes)
        {
            var d = ExtractDouble(bytes);
            if (d == null) return null;
            var r = RoundToInt(d.GetValueOrDefault());
            if (r < DaysMin1960 || r > DaysMax1960) return null;
            return Epoch1960.AddDays(r);
        }
    }
}