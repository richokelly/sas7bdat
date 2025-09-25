using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace Sas7Bdat.Core.Serializers;

internal static class FieldSerializers
{
    private static readonly DateTime Epoch1960 = new(1960, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly double DaysMin1960;
    private static readonly double DaysMax1960;
    private static readonly double SecsMin1960;
    private static readonly double SecsMax1960;

    static FieldSerializers()
    {
        var min = new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var max = new DateTime(9999, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc).AddTicks(9999); // absolute max

        DaysMin1960 = (min - Epoch1960).TotalDays;
        DaysMax1960 = (max - Epoch1960).TotalDays;

        SecsMin1960 = (min - Epoch1960).TotalSeconds;
        SecsMax1960 = (max - Epoch1960).TotalSeconds;
    }

    public static ColumnType InferKind(ColumnType storage, string? format, int length)
    {
        if (storage == ColumnType.String) return ColumnType.String;
        if (storage != ColumnType.Number) return ColumnType.Unknown;

        var normalisedFormat = NormaliseFormat(format);
        if (normalisedFormat.Length == 0) return ColumnType.Integer;

        if (length == 0) return ColumnType.Integer;
        if (length == 1) return ColumnType.Integer;
        if (length == 2) return ColumnType.Integer;

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


    internal static object? ConvertSasDateTimeSeconds(double? d)
    {
        if (d == null) return null;
        var r = RoundToInt(d.GetValueOrDefault());
        if (r < SecsMin1960 || r > SecsMax1960) return null;
        return Epoch1960.AddSeconds(r);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double RoundToInt(double x) => Math.Round(x, MidpointRounding.AwayFromZero);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsSasDateFromDatetime(string f)
        => StartsWithAny(f, ["B8601DN", "E8601DN", "IS8601DN"]);

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

    private abstract class NumericSerializer(Endian endian) : IDataSerializer
    {
        protected double? ExtractDouble(ReadOnlySpan<byte> data)
        {
            var length = data.Length;
            switch (length)
            {
                case 1:
                    return data[0];
                case 2:
                    return ReadInt16(data);
                case < 8:
                    {
                        var result = ReadIncompleteDouble(data, length);
                        return double.IsNaN(result) ? null : result;
                    }
                case 8:
                    {
                        var value = ReadInt64(data);
                        var result = BitConverter.Int64BitsToDouble(value);
                        return double.IsNaN(result) ? null : result;
                    }
                default:
                    return null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private short ReadInt16(ReadOnlySpan<byte> data)
        {
            return endian == Endian.Big
                ? BinaryPrimitives.ReadInt16BigEndian(data)
                : BinaryPrimitives.ReadInt16LittleEndian(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long ReadInt64(ReadOnlySpan<byte> data)
        {
            var result = endian == Endian.Big
                ? BinaryPrimitives.ReadInt64BigEndian(data)
                : BinaryPrimitives.ReadInt64LittleEndian(data);

            return result;
        }

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

        public abstract object? Deserialize(ReadOnlySpan<byte> bytes);
    }

    private class SasStringSerializer(Encoding encoding) : IDataSerializer
    {
        public object? Deserialize(ReadOnlySpan<byte> data)
        {
            var endIndex = data.Length;
            for (var i = data.Length - 1; i >= 0; i--)
            {
                if (data[i] == 0 || data[i] == 32) continue;
                endIndex = i + 1;
                break;
            }

            if (endIndex == 0)
                return string.Empty;

            var startIndex = 0;
            for (var i = 0; i < endIndex; i++)
            {
                if (data[i] == 32) continue;
                startIndex = i;
                break;
            }

            return startIndex >= endIndex
                ? string.Empty
                : encoding.GetString(data.Slice(startIndex, endIndex - startIndex)).Trim();
        }
    }

    private class SasDoubleSerializer(Endian endian) : NumericSerializer(endian)
    {
        public override object? Deserialize(ReadOnlySpan<byte> data) => ExtractDouble(data);
    }

    private class SasTimeSerializer(Endian endian) : NumericSerializer(endian)
    {
        public override object? Deserialize(ReadOnlySpan<byte> bytes)
        {
            var d = ExtractDouble(bytes);
            return d == null ? null : TimeSpan.FromSeconds(RoundToInt(d.GetValueOrDefault()));
        }
    }

    private class SasDateTimeSerializer(Endian endian) : NumericSerializer(endian)
    {
        public override object? Deserialize(ReadOnlySpan<byte> bytes)
        {
            var d = ExtractDouble(bytes);
            if (d == null) return null;
            var r = RoundToInt(d.GetValueOrDefault());
            if (r < SecsMin1960 || r > SecsMax1960) return null;
            return Epoch1960.AddSeconds(r);
        }
    }

    private class SasDateFromSecondsSerializer(Endian endian) : NumericSerializer(endian)
    {
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

    private class SasDateFromDaysSerializer(Endian endian) : NumericSerializer(endian)
    {
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