namespace Sas7Bdat.Core.Decompression;

public interface IDecompressor
{
    ReadOnlyMemory<byte> Decompress(ReadOnlyMemory<byte> compressed, int expectedLength);
}