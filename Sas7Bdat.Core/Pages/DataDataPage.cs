using Sas7Bdat.Core.Decompression;

namespace Sas7Bdat.Core.Pages;

/// <summary>
/// Represents a data page containing only observation rows without metadata.
/// </summary>
/// <remarks>
/// DataDataPage handles pages that contain pure data content - rows of observations
/// organized according to the column layout defined in the file metadata. These pages
/// contain no subheaders or metadata information, just sequential data rows.
/// 
/// Key characteristics:
/// <list type="bullet">
/// <item><description>Contains only observation data, no metadata subheaders</description></item>
/// <item><description>Rows are stored sequentially starting immediately after the page header</description></item>
/// <item><description>Each row has the fixed length specified in the file metadata</description></item>
/// <item><description>The number of rows per page is indicated by the page's BlockCount</description></item>
/// </list>
/// 
/// The page layout is straightforward:
/// <list type="number">
/// <item><description>Page header (format-dependent size)</description></item>
/// <item><description>Sequential data rows, each of length Metadata.RowLength</description></item>
/// </list>
/// 
/// This is the most common page type in SAS files after the initial metadata pages,
/// and represents the bulk of the file content for large datasets.
/// </remarks>
/// <example>
/// <code>
/// var dataPage = new DataDataPage(pageBuffer, metadata, decompressor);
/// 
/// foreach (var row in dataPage.EnumerateRows())
/// {
///     // Process each row - row.Span contains raw bytes for all columns
///     var firstColumnData = row.Span.Slice(columnOffset, columnLength);
/// }
/// </code>
/// </example>
internal sealed class DataDataPage : SasDataPage
{
    /// <summary>
    /// The byte offset within the page where data rows begin.
    /// </summary>
    /// <remarks>
    /// This offset accounts for the page header structure and represents the position
    /// immediately following the header where the first data row is stored.
    /// The value is calculated as PageBitOffset + 8 to skip past the page type,
    /// block count, and subheader count fields.
    /// </remarks>
    private readonly int _dataStartOffset;

    /// <summary>
    /// Initializes a new instance of the DataDataPage class.
    /// </summary>
    /// <param name="pageBuffer">The memory buffer containing the complete page data.</param>
    /// <param name="metadata">The file metadata containing format and structure information.</param>
    /// <param name="decompressor">The decompressor for handling compressed data (typically not used for data pages).</param>
    /// <remarks>
    /// The constructor calculates the data start offset by adding 8 bytes to the page bit offset
    /// to account for the page header fields (type, block count, subheader count). Since data
    /// pages don't contain subheaders, the data rows begin immediately after these header fields.
    /// 
    /// The page buffer should contain the complete page data as read from the file, and its
    /// length should match the PageLength specified in the metadata.
    /// </remarks>
    public DataDataPage(Memory<byte> pageBuffer, SasFileMetadata metadata, IDecompressor decompressor)
        : base(pageBuffer, metadata, decompressor)
    {
        _dataStartOffset = PageBitOffset + 8;
    }

    /// <summary>
    /// Enumerates all data rows contained within this page.
    /// </summary>
    /// <returns>
    /// An enumerable sequence of ReadOnlyMemory&lt;byte&gt; instances, where each instance
    /// represents one complete data row containing all column values in binary format.
    /// </returns>
    /// <remarks>
    /// This method provides sequential access to all data rows on the page. Each returned
    /// memory segment contains exactly one row of data with the length specified by
    /// Metadata.RowLength. The data is returned in its raw binary format and must be
    /// interpreted according to the column specifications defined in the file metadata.
    /// 
    /// **Row Structure:**
    /// Each row contains data for all columns in the dataset, with each column occupying
    /// a fixed position and length within the row:
    /// <list type="bullet">
    /// <item><description>Columns are positioned at their defined offsets</description></item>
    /// <item><description>String columns contain character data in the file's encoding</description></item>
    /// <item><description>Numeric columns contain binary IEEE 754 double values</description></item>
    /// <item><description>Date/time columns contain SAS date/time values as doubles</description></item>
    /// </list>
    /// 
    /// **Performance Characteristics:**
    /// <list type="bullet">
    /// <item><description>Lazy enumeration - rows are yielded on demand</description></item>
    /// <item><description>No memory copying - returns slices of the original page buffer</description></item>
    /// <item><description>O(1) access time per row with O(n) total enumeration</description></item>
    /// </list>
    /// 
    /// **Usage Pattern:**
    /// The returned memory segments are valid only as long as the underlying page buffer
    /// remains valid. If you need to store row data beyond the current enumeration,
    /// copy the data to a separate buffer.
    /// </remarks>
    /// <example>
    /// <code>
    /// var dataPage = new DataDataPage(pageBuffer, metadata, decompressor);
    /// 
    /// foreach (var rowData in dataPage.EnumerateRows())
    /// {
    ///     // Extract first column (assuming it starts at offset 0 with length 8)
    ///     var firstColumn = rowData.Span.Slice(0, 8);
    ///     
    ///     // Extract a string column (assuming offset 8, length 20)
    ///     var stringColumn = rowData.Span.Slice(8, 20);
    ///     
    ///     // Process the raw binary data according to column types
    /// }
    /// </code>
    /// </example>
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