using System.Runtime.CompilerServices;

namespace Sas7Bdat.Core.Decompression;

internal sealed class RdcDecompressor : IDecompressor
{
    private const byte CharNull = 0x00;

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
                    throw new InvalidOperationException($"Unknown RDC marker {val:X2} at offset {inputPos - 1}");
                }
            }
        }

        while (outputPos < destination.Length)
        {
            output[outputPos++] = CharNull;
        }
    }

    private static void FillBytes(Span<byte> dest, ref int destPos, byte value, int count)
    {
        var actualCount = Math.Min(count, dest.Length - destPos);
        if (actualCount > 0)
        {
            dest.Slice(destPos, actualCount).Fill(value);
            destPos += actualCount;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyPattern(Span<byte> buffer, int currentPos, int offset, int count, ref int outputPos)
    {
        if (offset > currentPos)
        {
            throw new InvalidOperationException($"Invalid RDC pattern offset: {offset} > {currentPos}");
        }

        var sourcePos = currentPos - offset;
        var actualCount = Math.Min(count, buffer.Length - outputPos);

        for (var i = 0; i < actualCount; i++)
        {
            buffer[outputPos++] = buffer[sourcePos + i % offset];
        }
    }
}