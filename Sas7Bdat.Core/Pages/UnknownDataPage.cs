using Sas7Bdat.Core.Decompression;

namespace Sas7Bdat.Core.Pages;

internal sealed class UnknownDataPage(Memory<byte> pageBuffer, SasFileMetadata metadata, IDecompressor decompressor)
    : SasDataPage(pageBuffer, metadata, decompressor)
{
    public override IEnumerable<ReadOnlyMemory<byte>> EnumerateRows() => [];
}