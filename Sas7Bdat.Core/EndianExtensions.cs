using System.Buffers.Binary;
using System.Text;

namespace Sas7Bdat.Core;

internal static class EndianExtensions
{
    public static ushort ReadUInt16At(this Endian endian, ReadOnlySpan<byte> bytes, int offset)
    {
        var span = bytes.Slice(offset, 2);

        return endian == Endian.Big
            ? BinaryPrimitives.ReadUInt16BigEndian(span)
            : BinaryPrimitives.ReadUInt16LittleEndian(span);
    }

    public static byte ReadByteAt(this Endian endian, ReadOnlySpan<byte> buffer, int offset)
    {
        return buffer[offset];
    }

    public static uint ReadUInt32At(this Endian endian, ReadOnlySpan<byte> buffer, int offset)
    {
        var span = buffer.Slice(offset, 4);

        return endian == Endian.Big
            ? BinaryPrimitives.ReadUInt32BigEndian(span)
            : BinaryPrimitives.ReadUInt32LittleEndian(span);
    }

    public static ulong ReadUInt64At(this Endian endian, ReadOnlySpan<byte> buffer, int offset)
    {
        var span = buffer.Slice(offset, 8);

        return endian == Endian.Big
            ? BinaryPrimitives.ReadUInt64BigEndian(span)
            : BinaryPrimitives.ReadUInt64LittleEndian(span);
    }

    public static double ReadDoubleAt(this Endian endian, ReadOnlySpan<byte> buffer, int offset)
    {
        var span = buffer.Slice(offset, 8);

        var longBits = endian == Endian.Big ? BinaryPrimitives.ReadInt64BigEndian(span) : BinaryPrimitives.ReadInt64LittleEndian(span);

        return BitConverter.Int64BitsToDouble(longBits);
    }

    public static ReadOnlySpan<byte> ReadBytesAt(this ReadOnlySpan<byte> buffer, int offset, int count)
    {
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative");
        if (count < 0)
            throw new ArgumentException("Count cannot be negative", nameof(count));
        if (offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count), $"Cannot read {count} bytes at offset {offset}");
        return buffer.Slice(offset, count);
    }

    public static string ReadStringAt(this ReadOnlySpan<byte> buffer, int offset, int length, Encoding encoding)
    {
        return buffer.Slice(offset, length).ReadStringFromSpan(encoding);
    }

    private static string ReadStringFromSpan(this ReadOnlySpan<byte> bytes, Encoding encoding)
    {
        int endIndex = bytes.Length;
        for (int i = bytes.Length - 1; i >= 0; i--)
        {
            if (bytes[i] != 0 && bytes[i] != 32)
            {
                endIndex = i + 1;
                break;
            }
        }

        if (endIndex == 0)
            return string.Empty;

        var startIndex = 0;
        for (var i = 0; i < endIndex; i++)
        {
            if (bytes[i] != 32)
            {
                startIndex = i;
                break;
            }
        }

        if (startIndex >= endIndex)
            return string.Empty;

        return encoding.GetString(bytes[startIndex..(endIndex - startIndex)]);
    }

    public static ulong ReadIntegerBySizeAt(this Endian endian, ReadOnlySpan<byte> buffer, int offset, int size)
    {
        return size switch
        {
            1 => endian.ReadByteAt(buffer, offset),
            2 => endian.ReadUInt16At(buffer, offset),
            4 => endian.ReadUInt32At(buffer, offset),
            8 => endian.ReadUInt64At(buffer, offset),
            _ => throw new ArgumentException($"Invalid integer size: {size}")
        };
    }
}