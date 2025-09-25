using Sas7Bdat.Core.Decompression;

namespace Sas7Bdat.Core.Pages;

internal sealed class MixDataPage : SasDataPage
{
    private readonly long _rowCount;
    private readonly int _dataStartOffset;

    public MixDataPage(Memory<byte> pageBuffer, SasFileMetadata metadata, IDecompressor decompressor, long currentRow)
        : base(pageBuffer, metadata, decompressor)
    {
        var subheaderSize = 3 * IntegerSize;
        var baseOffset = PageBitOffset + 8 + Header.SubheaderCount * subheaderSize;

        var alignCorrection = baseOffset % 8;

        _dataStartOffset = baseOffset + alignCorrection;

        var remainingRows = metadata.RowCount - currentRow;
        _rowCount = Math.Min(metadata.MixPageRowCount, remainingRows);
    }

    public override IEnumerable<ReadOnlyMemory<byte>> EnumerateRows()
    {
        var length = Metadata.RowLength;
        var bufferLength = PageBuffer.Length;

        for (var i = 0; i < _rowCount; i++)
        {
            var offset = _dataStartOffset + i * length;
            if (offset + length > bufferLength)
                yield break;

            yield return PageBuffer.Slice(offset, length);
        }
    }
}