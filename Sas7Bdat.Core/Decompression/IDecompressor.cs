namespace Sas7Bdat.Core.Decompression;

public interface IDecompressor
{
    void Decompress(ReadOnlySpan<byte> compressed, Span<byte> destination);
}