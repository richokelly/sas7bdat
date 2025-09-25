namespace Sas7Bdat.Core.Decompression;

internal sealed class RdcDecompressor : IDecompressor
{
    private const byte CharNull = 0x00;

    public ReadOnlyMemory<byte> Decompress(ReadOnlyMemory<byte> compressed, int expectedLength)
    {
        var output = new byte[expectedLength];
        var outputPos = 0;
        var inputPos = 0;

        int controlMask = 0;
        int controlBits = 0;

        while (inputPos < compressed.Length - 2 && outputPos < expectedLength)
        {
            controlMask >>= 1;
            if (controlMask == 0)
            {
                if (inputPos + 1 >= compressed.Length) break;
                controlBits = compressed.Span[inputPos] << 8 | compressed.Span[inputPos + 1];
                inputPos += 2;
                controlMask = 0x8000;
            }

            if ((controlBits & controlMask) == 0)
            {
                if (inputPos < compressed.Length)
                {
                    output[outputPos++] = compressed.Span[inputPos++];
                }
            }
            else
            {
                if (inputPos >= compressed.Length) break;

                var val = compressed.Span[inputPos++];
                var cmd = val >> 4 & 0x0F;
                var cnt = val & 0x0F;

                if (cmd == 0)
                {
                    if (inputPos >= compressed.Length) break;
                    var repeatCount = cnt + 3;
                    var repeatByte = compressed.Span[inputPos++];
                    FillBytes(output, ref outputPos, repeatByte, repeatCount);
                }
                else if (cmd == 1)
                {
                    if (inputPos + 1 >= compressed.Length) break;
                    var repeatCount = cnt + (compressed.Span[inputPos] << 4) + 19;
                    inputPos++;
                    var repeatByte = compressed.Span[inputPos++];
                    FillBytes(output, ref outputPos, repeatByte, repeatCount);
                }
                else if (cmd == 2)
                {
                    if (inputPos + 1 >= compressed.Length) break;
                    var offset = cnt + 3 + (compressed.Span[inputPos] << 4);
                    inputPos++;
                    var copyCount = compressed.Span[inputPos++] + 16;
                    CopyPattern(output, outputPos, offset, copyCount, ref outputPos);
                }
                else if (cmd >= 3 && cmd <= 15)
                {
                    if (inputPos >= compressed.Length) break;
                    var offset = cnt + 3 + (compressed.Span[inputPos] << 4);
                    inputPos++;
                    CopyPattern(output, outputPos, offset, cmd, ref outputPos);
                }
                else
                {
                    throw new InvalidOperationException($"Unknown RDC marker {val:X2} at offset {inputPos - 1}");
                }
            }
        }

        while (outputPos < expectedLength)
        {
            output[outputPos++] = CharNull;
        }

        return output;
    }

    private static void FillBytes(byte[] dest, ref int destPos, byte value, int count)
    {
        var actualCount = Math.Min(count, dest.Length - destPos);
        if (actualCount > 0)
        {
            dest.AsSpan(destPos, actualCount).Fill(value);
            destPos += actualCount;
        }
    }

    private static void CopyPattern(byte[] buffer, int currentPos, int offset, int count, ref int outputPos)
    {
        if (offset > currentPos)
        {
            throw new InvalidOperationException($"Invalid RDC pattern offset: {offset} > {currentPos}");
        }

        var sourcePos = currentPos - offset;
        var actualCount = Math.Min(count, buffer.Length - outputPos);

        for (int i = 0; i < actualCount; i++)
        {
            buffer[outputPos++] = buffer[sourcePos + i % offset];
        }
    }
}