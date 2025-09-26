namespace Sas7Bdat.Core.Decompression;

public sealed class NoDecompressor : IDecompressor
{
    public void Decompress(ReadOnlySpan<byte> compressed, Span<byte> destination)
    {
        compressed.CopyTo(destination);
    }
}