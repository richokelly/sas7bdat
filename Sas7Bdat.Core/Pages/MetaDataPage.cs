using Sas7Bdat.Core.Decompression;
using Sas7Bdat.Core.Headers;

namespace Sas7Bdat.Core.Pages;

internal record struct PageSubheader(int Offset, int Length, byte Compression, byte Type);
internal record struct PageHeader(SasPageType Type, ushort BlockCount, ushort SubheaderCount);

internal sealed class MetaDataPage(ReadOnlyMemory<byte> pageBuffer, SasFileMetadata metadata, IDecompressor decompressor)
    : SasDataPage(pageBuffer, metadata, decompressor)
{
    public override IEnumerable<ReadOnlyMemory<byte>> EnumerateRows()
    {
        var subheaderSize = 3 * IntegerSize;

        for (var i = 0; i < Header.SubheaderCount; i++)
        {
            var offset = PageBitOffset + 8 + i * subheaderSize;
            var subheader = ReadSubheaderAt(offset);

            if (subheader.Length == 0 || subheader.Compression == HeaderConstants.TruncatedSubheaderId)
                continue;

            var signature = PageBuffer.Span.ReadBytesAt(subheader.Offset, IntegerSize);
            var subheaderType = SubheaderSignatures.IdentifySubheader(signature, Metadata.Format);
            if (subheaderType != SubheaderType.Unknown && subheaderType != SubheaderType.Data) continue;

            if (!(Metadata.Compression != Compression.None &&
                  (subheader.Compression == HeaderConstants.CompressedSubheaderId || subheader.Compression == 0) &&
                  subheader.Type == HeaderConstants.CompressedSubheaderType)) continue;

            var rawData = PageBuffer.Slice(subheader.Offset, subheader.Length);
            var decompressed = Metadata.Compression == Compression.None || subheader.Length >= Metadata.RowLength
                ? rawData
                : Decompressor.Decompress(rawData, Metadata.RowLength);

            yield return decompressed;
        }
    }

    private PageSubheader ReadSubheaderAt(int location)
    {
        var offset = (int)Metadata.Endianness.ReadIntegerBySizeAt(PageBuffer.Span, location, IntegerSize);
        var length = (int)Metadata.Endianness.ReadIntegerBySizeAt(PageBuffer.Span, location + IntegerSize, IntegerSize);
        var compression = Metadata.Endianness.ReadByteAt(PageBuffer.Span, location + IntegerSize * 2);
        var type = Metadata.Endianness.ReadByteAt(PageBuffer.Span, location + IntegerSize * 2 + 1);

        return new PageSubheader(offset, length, compression, type);
    }
}