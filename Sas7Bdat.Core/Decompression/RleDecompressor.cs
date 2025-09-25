namespace Sas7Bdat.Core.Decompression;

internal sealed class RleDecompressor : IDecompressor
{
    private const byte CharNull = 0x00;
    private const byte CharSpace = 0x20;
    private const byte CharAt = 0x40;

    public ReadOnlyMemory<byte> Decompress(ReadOnlyMemory<byte> compressed, int expectedLength)
    {
        var output = new byte[expectedLength];
        var outputPos = 0;
        var inputPos = 0;

        while (inputPos < compressed.Length - 1 && outputPos < expectedLength)
        {
            var val = compressed.Span[inputPos++];
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
                        var n = (endOfFirstByte << 8) + compressed.Span[inputPos++] + 64;
                        var length = n;
                        var availableInput = compressed.Length - inputPos;
                        length = Math.Min(length, availableInput); 

                        var availableOutput = expectedLength - outputPos;
                        length = Math.Min(length, availableOutput);

                        if (length > 0)
                        {
                            compressed.Slice(inputPos, length).CopyTo(output.AsMemory(outputPos, length));
                            inputPos += length;
                            outputPos += length;
                        }
                    }
                    break;

                case 0x4: // SAS_RLE_COMMAND_INSERT_BYTE18
                    if (inputPos + 1 < compressed.Length)
                    {
                        var n = (endOfFirstByte << 4) + compressed.Span[inputPos++] + 18;
                        var fillByte = compressed.Span[inputPos++];
                        var length = n;
                        var availableOutput = expectedLength - outputPos;
                        length = Math.Min(length, availableOutput);

                        if (length > 0)
                        {
                            output.AsSpan(outputPos, length).Fill(fillByte);
                            outputPos += length;
                        }
                    }
                    break;

                case 0x5: // SAS_RLE_COMMAND_INSERT_AT17
                    if (inputPos < compressed.Length)
                    {
                        var n = (endOfFirstByte << 8) + compressed.Span[inputPos++] + 17;
                        var length = n;
                        var availableOutput = expectedLength - outputPos;
                        length = Math.Min(length, availableOutput);

                        if (length > 0)
                        {
                            output.AsSpan(outputPos, length).Fill(CharAt);
                            outputPos += length;
                        }
                    }
                    break;

                case 0x6: // SAS_RLE_COMMAND_INSERT_BLANK17
                    if (inputPos < compressed.Length)
                    {
                        var n = (endOfFirstByte << 8) + compressed.Span[inputPos++] + 17;
                        var length = n;
                        var availableOutput = expectedLength - outputPos;
                        length = Math.Min(length, availableOutput);

                        if (length > 0)
                        {
                            output.AsSpan(outputPos, length).Fill(CharSpace);
                            outputPos += length;
                        }
                    }
                    break;

                case 0x7: // SAS_RLE_COMMAND_INSERT_ZERO17
                    if (inputPos < compressed.Length)
                    {
                        var n = (endOfFirstByte << 8) + compressed.Span[inputPos++] + 17;
                        var length = n;
                        var availableOutput = expectedLength - outputPos;
                        length = Math.Min(length, availableOutput);

                        if (length > 0)
                        {
                            output.AsSpan(outputPos, length).Fill(CharNull);
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
                            compressed.Slice(inputPos, length).CopyTo(output.AsMemory(outputPos, length));
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
                            compressed.Slice(inputPos, length).CopyTo(output.AsMemory(outputPos, length));
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
                            compressed.Slice(inputPos, length).CopyTo(output.AsMemory(outputPos, length));
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
                            compressed.Slice(inputPos, length).CopyTo(output.AsMemory(outputPos, length));
                            inputPos += length;
                            outputPos += length;
                        }
                    }
                    break;

                case 0xC: // SAS_RLE_COMMAND_INSERT_BYTE3
                    if (inputPos < compressed.Length)
                    {
                        var fillByte = compressed.Span[inputPos++];
                        var n = endOfFirstByte + 3;
                        var length = n;
                        var availableOutput = expectedLength - outputPos;
                        length = Math.Min(length, availableOutput);

                        if (length > 0)
                        {
                            output.AsSpan(outputPos, length).Fill(fillByte);
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
                            output.AsSpan(outputPos, length).Fill(CharAt);
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
                            output.AsSpan(outputPos, length).Fill(CharSpace);
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
                            output.AsSpan(outputPos, length).Fill(CharNull);
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

        return output;
    }
}