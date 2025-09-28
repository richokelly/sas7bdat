using System.Buffers.Binary;
using System.Text;

namespace Sas7Bdat.Core;

/// <summary>
/// Provides extension methods for reading binary data with endianness support.
/// </summary>
/// <remarks>
/// This class contains utility methods for reading various data types from byte spans
/// while respecting the specified endianness (big-endian or little-endian).
/// </remarks>
internal static class EndianExtensions
{
    /// <summary>
    /// Reads a 16-bit unsigned integer from the specified offset in the byte span using the given endianness.
    /// </summary>
    /// <param name="endian">The endianness to use for reading the value.</param>
    /// <param name="bytes">The source byte span to read from.</param>
    /// <param name="offset">The zero-based offset in the byte span where reading begins.</param>
    /// <returns>A 16-bit unsigned integer read from the specified position.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the offset is invalid or there are insufficient bytes to read.</exception>
    public static ushort ReadUInt16At(this Endian endian, ReadOnlySpan<byte> bytes, int offset)
    {
        var span = bytes.Slice(offset, 2);

        return endian == Endian.Big
            ? BinaryPrimitives.ReadUInt16BigEndian(span)
            : BinaryPrimitives.ReadUInt16LittleEndian(span);
    }

    /// <summary>
    /// Reads a single byte from the specified offset in the buffer.
    /// </summary>
    /// <param name="endian">The endianness parameter (not used for single byte reads but maintained for consistency).</param>
    /// <param name="buffer">The source byte span to read from.</param>
    /// <param name="offset">The zero-based offset in the buffer where the byte is located.</param>
    /// <returns>The byte value at the specified offset.</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when the offset is outside the bounds of the buffer.</exception>
    public static byte ReadByteAt(this Endian endian, ReadOnlySpan<byte> buffer, int offset)
    {
        return buffer[offset];
    }

    /// <summary>
    /// Reads a 32-bit unsigned integer from the specified offset in the buffer using the given endianness.
    /// </summary>
    /// <param name="endian">The endianness to use for reading the value.</param>
    /// <param name="buffer">The source byte span to read from.</param>
    /// <param name="offset">The zero-based offset in the buffer where reading begins.</param>
    /// <returns>A 32-bit unsigned integer read from the specified position.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the offset is invalid or there are insufficient bytes to read.</exception>
    public static uint ReadUInt32At(this Endian endian, ReadOnlySpan<byte> buffer, int offset)
    {
        var span = buffer.Slice(offset, 4);

        return endian == Endian.Big
            ? BinaryPrimitives.ReadUInt32BigEndian(span)
            : BinaryPrimitives.ReadUInt32LittleEndian(span);
    }

    /// <summary>
    /// Reads a 64-bit unsigned integer from the specified offset in the buffer using the given endianness.
    /// </summary>
    /// <param name="endian">The endianness to use for reading the value.</param>
    /// <param name="buffer">The source byte span to read from.</param>
    /// <param name="offset">The zero-based offset in the buffer where reading begins.</param>
    /// <returns>A 64-bit unsigned integer read from the specified position.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the offset is invalid or there are insufficient bytes to read.</exception>
    public static ulong ReadUInt64At(this Endian endian, ReadOnlySpan<byte> buffer, int offset)
    {
        var span = buffer.Slice(offset, 8);

        return endian == Endian.Big
            ? BinaryPrimitives.ReadUInt64BigEndian(span)
            : BinaryPrimitives.ReadUInt64LittleEndian(span);
    }

    /// <summary>
    /// Reads a double-precision floating-point value from the specified offset in the buffer using the given endianness.
    /// </summary>
    /// <param name="endian">The endianness to use for reading the value.</param>
    /// <param name="buffer">The source byte span to read from.</param>
    /// <param name="offset">The zero-based offset in the buffer where reading begins.</param>
    /// <returns>A double-precision floating-point value read from the specified position.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the offset is invalid or there are insufficient bytes to read.</exception>
    public static double ReadDoubleAt(this Endian endian, ReadOnlySpan<byte> buffer, int offset)
    {
        var span = buffer.Slice(offset, 8);

        var longBits = endian == Endian.Big ? BinaryPrimitives.ReadInt64BigEndian(span) : BinaryPrimitives.ReadInt64LittleEndian(span);

        return BitConverter.Int64BitsToDouble(longBits);
    }

    /// <summary>
    /// Reads a specified number of bytes from the buffer starting at the given offset.
    /// </summary>
    /// <param name="buffer">The source byte span to read from.</param>
    /// <param name="offset">The zero-based offset in the buffer where reading begins.</param>
    /// <param name="count">The number of bytes to read.</param>
    /// <returns>A read-only span of bytes containing the requested data.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the offset is negative or when the offset plus count exceeds the buffer length.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when the count is negative.</exception>
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

    /// <summary>
    /// Reads a string from the buffer at the specified offset with the given length and encoding.
    /// </summary>
    /// <param name="buffer">The source byte span to read from.</param>
    /// <param name="offset">The zero-based offset in the buffer where the string begins.</param>
    /// <param name="length">The length in bytes of the string data to read.</param>
    /// <param name="encoding">The text encoding to use for converting bytes to string.</param>
    /// <returns>
    /// A string decoded from the specified bytes, with leading and trailing whitespace and null bytes removed.
    /// Returns an empty string if no valid characters are found.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the offset or length parameters are invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the encoding parameter is null.</exception>
    public static string ReadStringAt(this ReadOnlySpan<byte> buffer, int offset, int length, Encoding encoding)
    {
        return buffer.Slice(offset, length).ReadStringFromSpan(encoding);
    }

    /// <summary>
    /// Converts a byte span to a string using the specified encoding, trimming null bytes and spaces.
    /// </summary>
    /// <param name="bytes">The byte span containing the string data.</param>
    /// <param name="encoding">The text encoding to use for conversion.</param>
    /// <returns>
    /// A trimmed string representation of the byte data. Leading and trailing spaces and null bytes are removed.
    /// Returns an empty string if the span contains only whitespace or null bytes.
    /// </returns>
    /// <remarks>
    /// This method performs trimming by finding the actual start and end positions of meaningful data,
    /// excluding null bytes (0) and space characters (32) from both ends.
    /// </remarks>
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

    /// <summary>
    /// Reads an unsigned integer of the specified size from the buffer at the given offset using the specified endianness.
    /// </summary>
    /// <param name="endian">The endianness to use for reading the value.</param>
    /// <param name="buffer">The source byte span to read from.</param>
    /// <param name="offset">The zero-based offset in the buffer where reading begins.</param>
    /// <param name="size">The size in bytes of the integer to read. Must be 1, 2, 4, or 8.</param>
    /// <returns>An unsigned 64-bit integer containing the value read from the buffer.</returns>
    /// <exception cref="ArgumentException">Thrown when the size parameter is not 1, 2, 4, or 8.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the offset is invalid or there are insufficient bytes to read.</exception>
    /// <remarks>
    /// This method provides a unified interface for reading integers of different sizes.
    /// Smaller integer types are automatically promoted to 64-bit unsigned integers.
    /// </remarks>
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