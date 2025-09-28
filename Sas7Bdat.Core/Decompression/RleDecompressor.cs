namespace Sas7Bdat.Core.Decompression;

/// <summary>
/// Implements the RLE (Run-Length Encoding) decompression algorithm used in SAS7BDAT files.
/// </summary>
/// <remarks>
/// RLE is a compression algorithm that efficiently encodes sequences of repeated data.
/// The SAS implementation uses a command-based approach where each operation is encoded
/// as a 4-bit command followed by parameters that specify the operation details.
/// 
/// The RLE format supports several operation types:
/// <list type="bullet">
/// <item><description>Direct copying of literal data with various length encodings</description></item>
/// <item><description>Run-length encoding for repeated bytes, spaces, nulls, and '@' characters</description></item>
/// <item><description>Variable-length encoding to handle different data pattern sizes efficiently</description></item>
/// </list>
/// 
/// This implementation handles all 16 RLE command types (0x0 through 0xF) and includes
/// robust error handling for malformed compressed data. The decompressor is optimized
/// for performance while maintaining memory safety through comprehensive boundary checking.
/// 
/// The class is stateless and thread-safe, making it suitable for concurrent decompression
/// operations across multiple threads without requiring synchronization.
/// </remarks>
/// <example>
/// <code>
/// var decompressor = new RleDecompressor();
/// var compressedPage = ReadRleCompressedData();
/// var outputBuffer = new byte[pageSize];
/// 
/// decompressor.Decompress(compressedPage, outputBuffer);
/// // outputBuffer contains the decompressed page data
/// </code>
/// </example>
public class RleDecompressor : IDecompressor
{
    /// <summary>
    /// The null character (0x00) used for zero-fill operations and buffer padding.
    /// </summary>
    private const byte CharNull = 0x00;

    /// <summary>
    /// The space character (0x20) used for blank-fill operations.
    /// </summary>
    private const byte CharSpace = 0x20;

    /// <summary>
    /// The '@' character (0x40) used for at-sign fill operations.
    /// </summary>
    private const byte CharAt = 0x40;

    /// <summary>
    /// Decompresses RLE-compressed data into the specified destination buffer.
    /// </summary>
    /// <param name="compressed">The RLE-compressed data to decompress.</param>
    /// <param name="destination">The buffer where decompressed data will be written.</param>
    /// <remarks>
    /// This method implements the complete SAS RLE decompression algorithm by processing
    /// a sequence of commands, each encoded in a single byte where the upper 4 bits
    /// specify the command type and the lower 4 bits provide command-specific parameters.
    /// 
    /// Command Types and Operations:
    /// <list type="table">
    /// <listheader>
    /// <term>Command</term>
    /// <description>Operation</description>
    /// </listheader>
    /// <item>
    /// <term>0x0-0x3</term>
    /// <description>Copy 64+ bytes directly from input (COPY64)</description>
    /// </item>
    /// <item>
    /// <term>0x4</term>
    /// <description>Insert repeated byte 18+ times (INSERT_BYTE18)</description>
    /// </item>
    /// <item>
    /// <term>0x5</term>
    /// <description>Insert '@' character 17+ times (INSERT_AT17)</description>
    /// </item>
    /// <item>
    /// <term>0x6</term>
    /// <description>Insert space character 17+ times (INSERT_BLANK17)</description>
    /// </item>
    /// <item>
    /// <term>0x7</term>
    /// <description>Insert null bytes 17+ times (INSERT_ZERO17)</description>
    /// </item>
    /// <item>
    /// <term>0x8</term>
    /// <description>Copy 1-16 bytes directly (COPY1)</description>
    /// </item>
    /// <item>
    /// <term>0x9</term>
    /// <description>Copy 17-32 bytes directly (COPY17)</description>
    /// </item>
    /// <item>
    /// <term>0xA</term>
    /// <description>Copy 33-48 bytes directly (COPY33)</description>
    /// </item>
    /// <item>
    /// <term>0xB</term>
    /// <description>Copy 49-64 bytes directly (COPY49)</description>
    /// </item>
    /// <item>
    /// <term>0xC</term>
    /// <description>Insert repeated byte 3-18 times (INSERT_BYTE3)</description>
    /// </item>
    /// <item>
    /// <term>0xD</term>
    /// <description>Insert '@' character 2-17 times (INSERT_AT2)</description>
    /// </item>
    /// <item>
    /// <term>0xE</term>
    /// <description>Insert space character 2-17 times (INSERT_BLANK2)</description>
    /// </item>
    /// <item>
    /// <term>0xF</term>
    /// <description>Insert null bytes 2-17 times (INSERT_ZERO2)</description>
    /// </item>
    /// </list>
    /// 
    /// The algorithm includes comprehensive boundary checking to handle:
    /// <list type="bullet">
    /// <item><description>Insufficient input data for command parameters</description></item>
    /// <item><description>Output buffer overflow prevention</description></item>
    /// <item><description>Partial data scenarios at the end of input</description></item>
    /// </list>
    /// 
    /// Any remaining space in the destination buffer after decompression is filled with null bytes.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an unrecognized RLE command is encountered, indicating corrupted or
    /// invalid compressed data.
    /// </exception>
    /// <example>
    /// <code>
    /// var rleDecompressor = new RleDecompressor();
    /// var compressedData = new byte[] { 0x81, 0x41, 0x42, 0xF2 }; // Example RLE data
    /// var output = new byte[10];
    /// 
    /// try
    /// {
    ///     rleDecompressor.Decompress(compressedData, output);
    ///     // output now contains decompressed data
    /// }
    /// catch (InvalidOperationException ex)
    /// {
    ///     Console.WriteLine($"RLE decompression failed: {ex.Message}");
    /// }
    /// </code>
    /// </example>
    public void Decompress(ReadOnlySpan<byte> compressed, Span<byte> destination)
    {
        var output = destination;
        var outputPos = 0;
        var inputPos = 0;
        var span = compressed;
        var expectedLength = destination.Length;

        while (inputPos < compressed.Length - 1 && outputPos < destination.Length)
        {
            var val = span[inputPos++];
            var command = (byte)(val >> 4);
            var endOfFirstByte = (byte)(val & 0x0F);

            switch (command)
            {
                case 0x0: // SAS_RLE_COMMAND_COPY64
                case 0x1:
                case 0x2:
                case 0x3:
                    if (inputPos < compressed.Length)
                    {
                        var n = (endOfFirstByte << 8) + span[inputPos++] + 64;
                        var length = n;
                        var availableInput = compressed.Length - inputPos;
                        length = Math.Min(length, availableInput);

                        var availableOutput = destination.Length - outputPos;
                        length = Math.Min(length, availableOutput);

                        if (length > 0)
                        {
                            compressed.Slice(inputPos, length).CopyTo(output.Slice(outputPos, length));
                            inputPos += length;
                            outputPos += length;
                        }
                    }
                    break;

                case 0x4: // SAS_RLE_COMMAND_INSERT_BYTE18
                    if (inputPos + 1 < compressed.Length)
                    {
                        var n = (endOfFirstByte << 4) + span[inputPos++] + 18;
                        var fillByte = span[inputPos++];
                        var length = n;
                        var availableOutput = expectedLength - outputPos;
                        length = Math.Min(length, availableOutput);

                        if (length > 0)
                        {
                            output.Slice(outputPos, length).Fill(fillByte);
                            outputPos += length;
                        }
                    }
                    break;

                case 0x5: // SAS_RLE_COMMAND_INSERT_AT17
                    if (inputPos < compressed.Length)
                    {
                        var n = (endOfFirstByte << 8) + span[inputPos++] + 17;
                        var length = n;
                        var availableOutput = expectedLength - outputPos;
                        length = Math.Min(length, availableOutput);

                        if (length > 0)
                        {
                            output.Slice(outputPos, length).Fill(CharAt);
                            outputPos += length;
                        }
                    }
                    break;

                case 0x6: // SAS_RLE_COMMAND_INSERT_BLANK17
                    if (inputPos < compressed.Length)
                    {
                        var n = (endOfFirstByte << 8) + span[inputPos++] + 17;
                        var length = n;
                        var availableOutput = expectedLength - outputPos;
                        length = Math.Min(length, availableOutput);

                        if (length > 0)
                        {
                            output.Slice(outputPos, length).Fill(CharSpace);
                            outputPos += length;
                        }
                    }
                    break;

                case 0x7: // SAS_RLE_COMMAND_INSERT_ZERO17
                    if (inputPos < compressed.Length)
                    {
                        var n = (endOfFirstByte << 8) + span[inputPos++] + 17;
                        var length = n;
                        var availableOutput = expectedLength - outputPos;
                        length = Math.Min(length, availableOutput);

                        if (length > 0)
                        {
                            output.Slice(outputPos, length).Clear();
                            outputPos += length;
                        }
                    }
                    break;

                case 0x8: // SAS_RLE_COMMAND_COPY1
                    {
                        var n = endOfFirstByte + 1;
                        var length = n;
                        var availableInput = compressed.Length - inputPos;
                        length = Math.Min(length, availableInput);

                        var availableOutput = expectedLength - outputPos;
                        length = Math.Min(length, availableOutput);

                        if (length > 0)
                        {
                            compressed.Slice(inputPos, length).CopyTo(output.Slice(outputPos, length));
                            inputPos += length;
                            outputPos += length;
                        }
                    }
                    break;

                case 0x9: // SAS_RLE_COMMAND_COPY17
                    {
                        var n = endOfFirstByte + 17;
                        var length = n;
                        var availableInput = compressed.Length - inputPos;
                        length = Math.Min(length, availableInput);

                        var availableOutput = expectedLength - outputPos;
                        length = Math.Min(length, availableOutput);

                        if (length > 0)
                        {
                            compressed.Slice(inputPos, length).CopyTo(output.Slice(outputPos, length));
                            inputPos += length;
                            outputPos += length;
                        }
                    }
                    break;

                case 0xA: // SAS_RLE_COMMAND_COPY33
                    {
                        var n = endOfFirstByte + 33;
                        var length = n;
                        var availableInput = compressed.Length - inputPos;
                        length = Math.Min(length, availableInput);

                        var availableOutput = expectedLength - outputPos;
                        length = Math.Min(length, availableOutput);

                        if (length > 0)
                        {
                            compressed.Slice(inputPos, length).CopyTo(output.Slice(outputPos, length));
                            inputPos += length;
                            outputPos += length;
                        }
                    }
                    break;

                case 0xB: // SAS_RLE_COMMAND_COPY49
                    {
                        var n = endOfFirstByte + 49;
                        var length = n;
                        var availableInput = compressed.Length - inputPos;
                        length = Math.Min(length, availableInput);

                        var availableOutput = expectedLength - outputPos;
                        length = Math.Min(length, availableOutput);

                        if (length > 0)
                        {
                            compressed.Slice(inputPos, length).CopyTo(output.Slice(outputPos, length));
                            inputPos += length;
                            outputPos += length;
                        }
                    }
                    break;

                case 0xC: // SAS_RLE_COMMAND_INSERT_BYTE3
                    if (inputPos < compressed.Length)
                    {
                        var fillByte = span[inputPos++];
                        var n = endOfFirstByte + 3;
                        var length = n;
                        var availableOutput = expectedLength - outputPos;
                        length = Math.Min(length, availableOutput);

                        if (length > 0)
                        {
                            output.Slice(outputPos, length).Fill(fillByte);
                            outputPos += length;
                        }
                    }
                    break;

                case 0xD: // SAS_RLE_COMMAND_INSERT_AT2
                    {
                        var n = endOfFirstByte + 2;
                        var length = n;
                        var availableOutput = expectedLength - outputPos;
                        length = Math.Min(length, availableOutput);

                        if (length > 0)
                        {
                            output.Slice(outputPos, length).Fill(CharAt);
                            outputPos += length;
                        }
                    }
                    break;

                case 0xE: // SAS_RLE_COMMAND_INSERT_BLANK2
                    {
                        var n = endOfFirstByte + 2;
                        var length = n;
                        var availableOutput = expectedLength - outputPos;
                        length = Math.Min(length, availableOutput);

                        if (length > 0)
                        {
                            output.Slice(outputPos, length).Fill(CharSpace);
                            outputPos += length;
                        }
                    }
                    break;

                case 0xF: // SAS_RLE_COMMAND_INSERT_ZERO2
                    {
                        var n = endOfFirstByte + 2;
                        var length = n;
                        var availableOutput = expectedLength - outputPos;
                        length = Math.Min(length, availableOutput);

                        if (length > 0)
                        {
                            output.Slice(outputPos, length).Clear();
                            outputPos += length;
                        }
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Invalid RLE command: {command:X} at offset {inputPos - 1}");
            }
        }

        while (outputPos < expectedLength)
        {
            output[outputPos++] = CharNull;
        }
    }
}