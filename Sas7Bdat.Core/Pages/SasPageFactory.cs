using Sas7Bdat.Core.Decompression;

namespace Sas7Bdat.Core.Pages;

internal static class SasPageFactory
{
    internal static SasDataPage CreatePage(Memory<byte> pageBuffer, SasFileMetadata metadata, IDecompressor decompressor, long currentRow = 0)
    {
        if (pageBuffer.Length < metadata.PageLength)
            throw new ArgumentException($"Buffer size {pageBuffer.Length} is less than page length {metadata.PageLength}");

        var pageBitOffset = metadata.Format == Format.Bit64 ? 32 : 16;
        var pageType = (SasPageType)metadata.Endianness.ReadUInt16At(pageBuffer.Span, pageBitOffset);

        if (pageType.IsDataPage()) return new DataDataPage(pageBuffer, metadata, decompressor);
        if (pageType.IsMetaPage()) return new MetaDataPage(pageBuffer, metadata, decompressor);
        if (pageType.IsMixPage()) return new MixDataPage(pageBuffer, metadata, decompressor, currentRow);

        return new UnknownDataPage(pageBuffer, metadata, decompressor);
    }
}