using Sas7Bdat.Core.Decompression;

namespace Sas7Bdat.Core.Pages;

internal abstract class SasDataPage
{
    protected readonly ReadOnlyMemory<byte> PageBuffer;
    protected readonly SasFileMetadata Metadata;
    protected readonly PageHeader Header;
    protected readonly IDecompressor Decompressor;

    protected int PageBitOffset { get; }
    protected int IntegerSize { get; }

    protected SasDataPage(ReadOnlyMemory<byte> pageBuffer, SasFileMetadata metadata, IDecompressor decompressor)
    {
        PageBuffer = pageBuffer;
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        Decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));

        IntegerSize = metadata.Format == Format.Bit64 ? 8 : 4;
        PageBitOffset = metadata.Format == Format.Bit64 ? 32 : 16;

        var type = (SasPageType)Metadata.Endianness.ReadUInt16At(PageBuffer.Span, PageBitOffset);
        var blockCount = Metadata.Endianness.ReadUInt16At(PageBuffer.Span, PageBitOffset + 2);
        var subheaderCount = Metadata.Endianness.ReadUInt16At(PageBuffer.Span, PageBitOffset + 4);

        Header = new PageHeader(type, blockCount, subheaderCount);
    }

    public abstract IEnumerable<ReadOnlyMemory<byte>> EnumerateRows();
}