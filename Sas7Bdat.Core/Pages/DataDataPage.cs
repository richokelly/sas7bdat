using Sas7Bdat.Core.Decompression;

namespace Sas7Bdat.Core.Pages;

internal sealed class DataDataPage : SasDataPage
{
    private readonly int _dataStartOffset;

    public DataDataPage(Memory<byte> pageBuffer, SasFileMetadata metadata, IDecompressor decompressor)
        : base(pageBuffer, metadata, decompressor)
    {
        _dataStartOffset = PageBitOffset + 8;
    }

    public override IEnumerable<ReadOnlyMemory<byte>> EnumerateRows()
    {
        var length = Metadata.RowLength;
        var rows = Header.BlockCount;
        for (var i = 0; i < rows; i++)
        {
            var offset = _dataStartOffset + i * length;
            yield return PageBuffer.Slice(offset, length);
        }
    }
}