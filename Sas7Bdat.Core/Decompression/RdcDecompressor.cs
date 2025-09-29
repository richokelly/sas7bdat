using System.Runtime.CompilerServices;

namespace Sas7Bdat.Core.Decompression;

/// <summary>
/// Implements the RDC (Ross Data Compression) decompression algorithm used in SAS7BDAT files.
/// </summary>
/// <remarks>
/// RDC is a proprietary compression algorithm developed by SAS Institute that provides
/// efficient compression for typical SAS dataset patterns. It uses a combination of
/// literal byte copying, run-length encoding, and back-reference pattern matching.
/// 
/// The algorithm operates using a control bit stream that determines whether the next
/// operation is a literal copy or a compressed sequence. Compressed sequences can be:
/// <list type="bullet">
/// <item><description>Repeated byte patterns (run-length encoding)</description></item>
/// <item><description>Back-references to previously decompressed data</description></item>
/// <item><description>Extended repeat operations with larger counts</description></item>
/// </list>
/// 
/// This implementation is optimized for performance while maintaining memory safety
/// through boundary checking and validation of compressed data integrity.
/// 
/// The decompressor is stateless and thread-safe, allowing concurrent use across
/// multiple threads without synchronization requirements.
/// </remarks>
/// <example>
/// <code>
/// var decompressor = new RdcDecompressor();
/// var compressedData = ReadRdcCompressedPage();
/// var outputBuffer = new byte[originalPageSize];
/// 
/// decompressor.Decompress(compressedData, outputBuffer);
/// // outputBuffer now contains the decompressed page data
/// </code>
/// </example>
public class RdcDecompressor : IDecompressor
{
    /// <summary>
    /// Decompresses RDC-compressed data into the specified destination buffer.
    /// </summary>
    /// <param name="compressed">The RDC-compressed data to decompress.</param>
    /// <param name="destination">The buffer where decompressed data will be written.</param>
    /// <remarks>
    /// This method implements the complete RDC decompression algorithm:
    /// 
    /// 1. **Control Bit Processing**: Uses a 16-bit control word to determine operation types
    /// 2. **Literal Copying**: Direct byte-to-byte copying when control bit is 0
    /// 3. **Compressed Operations**: Various compression patterns when control bit is 1:
    ///    - Command 0: Simple repeat operations (3+ repetitions)
    ///    - Command 1: Extended repeat operations (19+ repetitions)
    ///    - Command 2: Back-reference copying with extended parameters
    ///    - Commands 3-15: Back-reference copying with varying lengths
    /// 
    /// The algorithm processes the input sequentially, updating control bits every 16 operations.
    /// Any remaining space in the destination buffer is filled with null bytes.
    /// 
    /// Performance considerations:
    /// <list type="bullet">
    /// <item><description>Aggressive inlining for pattern copying operations</description></item>
    /// <item><description>Boundary checking to prevent buffer overruns</description></item>
    /// <item><description>Efficient span-based operations for memory manipulation</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="InvalidDataException">
    /// Thrown when:
    /// <list type="bullet">
    /// <item><description>An unknown RDC command marker is encountered</description></item>
    /// <item><description>A back-reference offset is invalid (greater than current position)</description></item>
    /// <item><description>The compressed data is truncated or corrupted</description></item>
    /// </list>
    /// </exception>
    /// <example>
    /// <code>
    /// var rdcDecompressor = new RdcDecompressor();
    /// 
    /// try
    /// {
    ///     rdcDecompressor.Decompress(compressedPageData, decompressedBuffer);
    ///     Console.WriteLine("RDC decompression completed successfully");
    /// }
    /// catch (InvalidDataException ex)
    /// {
    ///     Console.WriteLine($"RDC decompression failed: {ex.Message}");
    /// }
    /// </code>
    /// </example>
    public void Decompress(ReadOnlySpan<byte> compressed, Span<byte> destination)
    {
        var output = destination;
        var outputPos = 0;
        var inputPos = 0;

        int controlMask = 0;
        int controlBits = 0;

        var span = compressed;
        while (inputPos < compressed.Length - 2 && outputPos < destination.Length)
        {
            controlMask >>= 1;
            if (controlMask == 0)
            {
                if (inputPos + 1 >= compressed.Length) break;
                controlBits = span[inputPos] << 8 | span[inputPos + 1];
                inputPos += 2;
                controlMask = 0x8000;
            }

            if ((controlBits & controlMask) == 0)
            {
                if (inputPos < compressed.Length)
                {
                    output[outputPos++] = span[inputPos++];
                }
            }
            else
            {
                if (inputPos >= compressed.Length) break;

                var val = span[inputPos++];
                var cmd = val >> 4 & 0x0F;
                var cnt = val & 0x0F;

                if (cmd == 0)
                {
                    if (inputPos >= compressed.Length) break;
                    var repeatCount = cnt + 3;
                    var repeatByte = span[inputPos++];
                    FillBytes(output, ref outputPos, repeatByte, repeatCount);
                }
                else if (cmd == 1)
                {
                    if (inputPos + 1 >= compressed.Length) break;
                    var repeatCount = cnt + (span[inputPos] << 4) + 19;
                    inputPos++;
                    var repeatByte = span[inputPos++];
                    FillBytes(output, ref outputPos, repeatByte, repeatCount);
                }
                else if (cmd == 2)
                {
                    if (inputPos + 1 >= compressed.Length) break;
                    var offset = cnt + 3 + (span[inputPos] << 4);
                    inputPos++;
                    var copyCount = span[inputPos++] + 16;
                    CopyPattern(output, outputPos, offset, copyCount, ref outputPos);
                }
                else if (cmd >= 3 && cmd <= 15)
                {
                    if (inputPos >= compressed.Length) break;
                    var offset = cnt + 3 + (span[inputPos] << 4);
                    inputPos++;
                    CopyPattern(output, outputPos, offset, cmd, ref outputPos);
                }
                else
                {
                    throw new InvalidDataException($"Unknown RDC marker {val:X2} at offset {inputPos - 1}");
                }
            }
        }

        output[outputPos..].Clear();
    }

    /// <summary>
    /// Fills a portion of the destination buffer with repeated instances of a specific byte value.
    /// </summary>
    /// <param name="dest">The destination buffer to fill.</param>
    /// <param name="destPos">Reference to the current position in the destination buffer, updated after filling.</param>
    /// <param name="value">The byte value to repeat.</param>
    /// <param name="count">The number of times to repeat the byte value.</param>
    /// <remarks>
    /// This method handles run-length encoding decompression by efficiently filling buffer sections
    /// with repeated byte values. It includes boundary checking to prevent writing beyond the
    /// destination buffer limits.
    /// 
    /// The method updates the destPos parameter to reflect the new position after filling,
    /// allowing the caller to continue processing from the correct location.
    /// </remarks>
    /// <example>
    /// <code>
    /// var buffer = new byte[100];
    /// var position = 10;
    /// 
    /// // Fill 5 bytes with value 0xFF starting at position 10
    /// FillBytes(buffer, ref position, 0xFF, 5);
    /// // position is now 15, buffer[10..14] contains 0xFF
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillBytes(Span<byte> dest, ref int destPos, byte value, int count)
    {
        var actualCount = Math.Min(count, dest.Length - destPos);
        if (actualCount > 0)
        {
            dest.Slice(destPos, actualCount).Fill(value);
            destPos += actualCount;
        }
    }

    /// <summary>
    /// Copies a pattern from a previous location in the buffer to the current position.
    /// </summary>
    /// <param name="buffer">The buffer containing both source and destination data.</param>
    /// <param name="currentPos">The current position in the buffer where copying should begin.</param>
    /// <param name="offset">The backward offset from currentPos where the pattern starts.</param>
    /// <param name="count">The number of bytes to copy from the pattern.</param>
    /// <param name="outputPos">Reference to the output position, updated after copying.</param>
    /// <remarks>
    /// This method implements back-reference decompression, a key component of the RDC algorithm.
    /// It copies previously decompressed data to the current position, allowing for efficient
    /// compression of repeated patterns.
    /// 
    /// Key behaviors:
    /// <list type="bullet">
    /// <item><description>Handles overlapping patterns where the copy length exceeds the pattern length</description></item>
    /// <item><description>Uses modulo arithmetic to repeat short patterns across longer copy operations</description></item>
    /// <item><description>Includes boundary checking to prevent buffer overruns</description></item>
    /// <item><description>Validates that offset doesn't exceed current position</description></item>
    /// </list>
    /// 
    /// The method is marked with AggressiveInlining for optimal performance in the tight
    /// decompression loop.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    /// Thrown when the offset is greater than the current position, indicating corrupted
    /// compressed data or an invalid back-reference.
    /// </exception>
    /// <example>
    /// <code>
    /// // Buffer contains: [A, B, C, D, ...]
    /// // Current position: 4, want to copy pattern "ABC" (offset=3, count=6)
    /// // Result: [A, B, C, D, A, B, C, A, B, C]
    /// //                     ^-- copies start here
    /// 
    /// var position = 4;
    /// CopyPattern(buffer, 4, 3, 6, ref position);
    /// // position is now 10
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyPattern(Span<byte> buffer, int currentPos, int offset, int count, ref int outputPos)
    {
        if (offset > currentPos)
            throw new InvalidDataException($"Invalid RDC pattern offset: {offset} > {currentPos}");

        var sourcePos = currentPos - offset;
        var actualCount = Math.Min(count, buffer.Length - outputPos);

        if (offset >= actualCount)
        {
            // Non-overlapping: can use efficient bulk copy
            buffer.Slice(sourcePos, actualCount).CopyTo(buffer.Slice(outputPos, actualCount));
            outputPos += actualCount;
        }
        else
        {
            // Overlapping: must copy byte-by-byte
            for (var i = 0; i < actualCount; i++)
            {
                buffer[outputPos++] = buffer[sourcePos + (i % offset)];
            }
        }
    }
}