namespace Sas7Bdat.Core.Decompression;

public sealed class NoDecompressor : IDecompressor
{
    public ReadOnlyMemory<byte> Decompress(ReadOnlyMemory<byte> compressed, int expectedLength)
    {
        return compressed;
    }
}