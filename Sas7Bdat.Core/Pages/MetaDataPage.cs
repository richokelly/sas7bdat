using Sas7Bdat.Core.Decompression;
using Sas7Bdat.Core.Headers;

namespace Sas7Bdat.Core.Pages;

/// <summary>
/// Represents a subheader within a page, containing metadata or data information.
/// </summary>
/// <param name="Offset">The byte offset within the page where the subheader data begins.</param>
/// <param name="Length">The length in bytes of the subheader data.</param>
/// <param name="Compression">The compression flag indicating the subheader's compression status.</param>
/// <param name="Type">The type identifier for the subheader content.</param>
/// <remarks>
/// PageSubheader provides the structural information needed to locate and interpret
/// subheader content within a page. The offset and length define the data boundaries,
/// while compression and type flags indicate how the data should be processed.
/// 
/// Compression values have special meanings:
/// <list type="bullet">
/// <item><description>0: Uncompressed data</description></item>
/// <item><description>1: Truncated subheader (should be skipped)</description></item>
/// <item><description>4: Compressed data requiring decompression</description></item>
/// </list>
/// </remarks>
internal record struct PageSubheader(int Offset, int Length, byte Compression, byte Type);

/// <summary>
/// Represents the header information for a page in a SAS7BDAT file.
/// </summary>
/// <param name="Type">The page type indicating the kind of content (data, metadata, mixed, etc.).</param>
/// <param name="BlockCount">The number of data blocks or rows contained in the page.</param>
/// <param name="SubheaderCount">The number of subheaders present in the page.</param>
/// <remarks>
/// PageHeader provides essential information for interpreting page content. The Type
/// determines the overall page structure and processing approach, while BlockCount
/// and SubheaderCount specify the quantities of different content types within the page.
/// 
/// Different page types use BlockCount differently:
/// <list type="bullet">
/// <item><description>Data pages: BlockCount indicates the number of data rows</description></item>
/// <item><description>Meta pages: BlockCount may indicate data blocks within subheaders</description></item>
/// <item><description>Mix pages: BlockCount relates to data rows in the data section</description></item>
/// </list>
/// </remarks>
internal record struct PageHeader(SasPageType Type, ushort BlockCount, ushort SubheaderCount);

/// <summary>
/// Represents a metadata page that contains subheaders with potential embedded data rows.
/// </summary>
/// <param name="pageBuffer">The memory buffer containing the complete page data.</param>
/// <param name="metadata">The file metadata containing format and structure information.</param>
/// <param name="decompressor">The decompressor for handling compressed subheader content.</param>
/// <remarks>
/// MetaDataPage handles pages that primarily contain metadata subheaders but may also
/// include embedded data rows within certain subheaders. This page type is used during
/// the metadata reading phase and can also yield data rows when subheaders contain
/// compressed or uncompressed observation data.
/// 
/// Key characteristics:
/// <list type="bullet">
/// <item><description>Contains subheaders with metadata about columns, formats, etc.</description></item>
/// <item><description>May contain embedded data rows within subheaders</description></item>
/// <item><description>Subheaders may be compressed and require decompression</description></item>
/// <item><description>Only yields rows from subheaders identified as containing data</description></item>
/// </list>
/// 
/// **Subheader Processing:**
/// The page examines each subheader to determine if it contains data rows:
/// <list type="number">
/// <item><description>Skip truncated subheaders (length = 0 or compression = 1)</description></item>
/// <item><description>Identify subheader type using signature matching</description></item>
/// <item><description>Process only Unknown or Data type subheaders for row extraction</description></item>
/// <item><description>Handle compression based on subheader compression flags</description></item>
/// </list>
/// 
/// **Compression Handling:**
/// Subheaders may be compressed when the file uses compression and specific conditions are met:
/// <list type="bullet">
/// <item><description>File has compression enabled (RLE or RDC)</description></item>
/// <item><description>Subheader compression flag is 4 or 0</description></item>
/// <item><description>Subheader type is 1 (compressed subheader type)</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var metaPage = new MetaDataPage(pageBuffer, metadata, decompressor);
/// 
/// foreach (var row in metaPage.EnumerateRows())
/// {
///     // Process data rows embedded within metadata subheaders
///     Console.WriteLine($"Found embedded row of {row.Length} bytes");
/// }
/// </code>
/// </example>
internal sealed class MetaDataPage(ReadOnlyMemory<byte> pageBuffer, SasFileMetadata metadata, IDecompressor decompressor)
    : SasDataPage(pageBuffer, metadata, decompressor)
{
    /// <summary>
    /// Enumerates data rows embedded within metadata subheaders.
    /// </summary>
    /// <returns>
    /// An enumerable sequence of ReadOnlyMemory&lt;byte&gt; instances representing data rows
    /// found within subheaders that contain observation data.
    /// </returns>
    /// <remarks>
    /// This method processes each subheader on the page to identify those containing
    /// data rows rather than pure metadata. The process involves:
    /// 
    /// **Subheader Identification:**
    /// <list type="number">
    /// <item><description>Read subheader descriptors from the page header area</description></item>
    /// <item><description>Skip truncated subheaders (length 0 or compression flag 1)</description></item>
    /// <item><description>Examine subheader signatures to determine type</description></item>
    /// <item><description>Process only Unknown or Data type subheaders</description></item>
    /// </list>
    /// 
    /// **Compression Detection:**
    /// Subheaders are considered compressed when all conditions are met:
    /// <list type="bullet">
    /// <item><description>File metadata indicates compression is enabled</description></item>
    /// <item><description>Subheader compression flag is 4 (compressed) or 0</description></item>
    /// <item><description>Subheader type flag is 1 (compressed subheader type)</description></item>
    /// </list>
    /// 
    /// **Data Extraction:**
    /// <list type="bullet">
    /// <item><description>Uncompressed: Return subheader data directly as a memory slice</description></item>
    /// <item><description>Compressed: Decompress into a temporary buffer and return buffer memory</description></item>
    /// <item><description>Size validation: Ensure subheader length is appropriate for row data</description></item>
    /// </list>
    /// 
    /// **Memory Management:**
    /// The method uses a pooled buffer for decompression to avoid memory allocation overhead.
    /// Compressed data is decompressed into this reusable buffer, while uncompressed data
    /// is returned as slices of the original page buffer.
    /// 
    /// **Performance Considerations:**
    /// <list type="bullet">
    /// <item><description>Lazy enumeration processes subheaders on demand</description></item>
    /// <item><description>Memory pooling reduces garbage collection pressure</description></item>
    /// <item><description>Direct memory slicing avoids unnecessary copying for uncompressed data</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var metaPage = new MetaDataPage(pageBuffer, metadata, decompressor);
    /// 
    /// foreach (var embeddedRow in metaPage.EnumerateRows())
    /// {
    ///     if (embeddedRow.Length >= metadata.RowLength)
    ///     {
    ///         // Process as a complete data row
    ///         ProcessDataRow(embeddedRow);
    ///     }
    ///     else
    ///     {
    ///         // Handle partial or special data
    ///         Console.WriteLine($"Partial data: {embeddedRow.Length} bytes");
    ///     }
    /// }
    /// </code>
    /// </example>
    public override IEnumerable<ReadOnlyMemory<byte>> EnumerateRows()
    {
        var subheaderSize = 3 * IntegerSize;

        using var buffer = new PooledMemory<byte>(Metadata.RowLength);
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

            if ((Metadata.Compression == Compression.None || subheader.Length >= Metadata.RowLength))
            {
                yield return PageBuffer.Slice(subheader.Offset, subheader.Length);
            }
            else
            {
                Decompressor.Decompress(PageBuffer.Span.Slice(subheader.Offset, subheader.Length), buffer.Span);
                yield return buffer.Memory;
            }
        }
    }

    /// <summary>
    /// Reads and parses a subheader descriptor at the specified location within the page.
    /// </summary>
    /// <param name="location">The byte offset within the page where the subheader descriptor is located.</param>
    /// <returns>A PageSubheader structure containing the parsed subheader information.</returns>
    /// <remarks>
    /// This method extracts subheader descriptor information from the page header area.
    /// Each descriptor contains four fields that define how to locate and interpret the
    /// associated subheader data:
    /// 
    /// **Field Layout (format-dependent offsets):**
    /// <list type="bullet">
    /// <item><description>Offset: IntegerSize bytes specifying subheader data location</description></item>
    /// <item><description>Length: IntegerSize bytes specifying subheader data size</description></item>
    /// <item><description>Compression: 1 byte indicating compression/truncation status</description></item>
    /// <item><description>Type: 1 byte indicating subheader content type</description></item>
    /// </list>
    /// 
    /// **Endianness Handling:**
    /// All multi-byte values are read using the file's endianness specification to ensure
    /// correct interpretation regardless of the platform where the file was created.
    /// 
    /// **Integer Size:**
    /// The size of offset and length fields depends on the file format:
    /// <list type="bullet">
    /// <item><description>32-bit format: 4-byte integers</description></item>
    /// <item><description>64-bit format: 8-byte integers</description></item>
    /// </list>
    /// </remarks>
    private PageSubheader ReadSubheaderAt(int location)
    {
        var endian = Metadata.Endianness;
        var offset = (int)endian.ReadIntegerBySizeAt(PageBuffer.Span, location, IntegerSize);
        var length = (int)endian.ReadIntegerBySizeAt(PageBuffer.Span, location + IntegerSize, IntegerSize);
        var compression = endian.ReadByteAt(PageBuffer.Span, location + IntegerSize * 2);
        var type = endian.ReadByteAt(PageBuffer.Span, location + IntegerSize * 2 + 1);

        return new PageSubheader(offset, length, compression, type);
    }
}