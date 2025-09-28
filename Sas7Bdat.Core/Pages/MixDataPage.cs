using Sas7Bdat.Core.Decompression;

namespace Sas7Bdat.Core.Pages;

/// <summary>
/// Represents a mixed page containing both metadata subheaders and data rows.
/// </summary>
/// <remarks>
/// MixDataPage handles pages that contain both subheaders with metadata and a data section
/// with observation rows. This page type combines the functionality of metadata and data
/// pages, typically appearing in the transition area between pure metadata pages and pure
/// data pages in the file structure.
/// 
/// Key characteristics:
/// <list type="bullet">
/// <item><description>Contains subheaders in the header area (like MetaDataPage)</description></item>
/// <item><description>Contains a data section with observation rows (like DataDataPage)</description></item>
/// <item><description>Data section starts after subheaders with 8-byte alignment</description></item>
/// <item><description>Row count may be limited by remaining space or file constraints</description></item>
/// </list>
/// 
/// **Page Layout:**
/// <list type="number">
/// <item><description>Page header (type, block count, subheader count)</description></item>
/// <item><description>Subheader descriptors (one per subheader)</description></item>
/// <item><description>Alignment padding to 8-byte boundary</description></item>
/// <item><description>Data section with sequential observation rows</description></item>
/// </list>
/// 
/// **Row Count Calculation:**
/// The number of rows is determined by the minimum of:
/// <list type="bullet">
/// <item><description>MixPageRowCount from file metadata (maximum rows per mixed page)</description></item>
/// <item><description>Remaining rows in the dataset (RowCount - currentRow)</description></item>
/// </list>
/// 
/// This ensures that the page doesn't exceed its capacity or read beyond the end of the dataset.
/// </remarks>
/// <example>
/// <code>
/// var mixPage = new MixDataPage(pageBuffer, metadata, decompressor, currentRowIndex);
/// 
/// foreach (var row in mixPage.EnumerateRows())
/// {
///     // Process data rows from the mixed page
///     Console.WriteLine($"Mixed page row: {row.Length} bytes");
/// }
/// </code>
/// </example>
internal sealed class MixDataPage : SasDataPage
{
    /// <summary>
    /// The number of data rows contained in this mixed page.
    /// </summary>
    /// <remarks>
    /// This value is calculated during construction and represents the actual number of
    /// rows that can be read from this page, considering both the page capacity and
    /// the remaining rows in the dataset.
    /// </remarks>
    private readonly long _rowCount;

    /// <summary>
    /// The byte offset within the page where the data section begins.
    /// </summary>
    /// <remarks>
    /// This offset is calculated to account for the page header, subheader descriptors,
    /// and required 8-byte alignment. The data section starts at this offset and contains
    /// sequential data rows.
    /// </remarks>
    private readonly int _dataStartOffset;

    /// <summary>
    /// Initializes a new instance of the MixDataPage class.
    /// </summary>
    /// <param name="pageBuffer">The memory buffer containing the complete page data.</param>
    /// <param name="metadata">The file metadata containing format and structure information.</param>
    /// <param name="decompressor">The decompressor for handling compressed data (if applicable).</param>
    /// <param name="currentRow">The zero-based index of the current row position in the dataset.</param>
    /// <remarks>
    /// The constructor performs several calculations to determine the data section layout:
    /// 
    /// **Data Offset Calculation:**
    /// <list type="number">
    /// <item><description>Calculate base offset: PageBitOffset + 8 + (SubheaderCount * SubheaderSize)</description></item>
    /// <item><description>Apply 8-byte alignment correction to ensure proper data alignment</description></item>
    /// <item><description>Set _dataStartOffset to the aligned position</description></item>
    /// </list>
    /// 
    /// **Row Count Determination:**
    /// <list type="bullet">
    /// <item><description>Calculate remaining rows: metadata.RowCount - currentRow</description></item>
    /// <item><description>Take minimum of remaining rows and metadata.MixPageRowCount</description></item>
    /// <item><description>Ensures we don't read beyond dataset end or page capacity</description></item>
    /// </list>
    /// 
    /// **Alignment Requirements:**
    /// The 8-byte alignment is required by the SAS file format to ensure proper data
    /// structure boundaries and may improve memory access performance.
    /// 
    /// **Subheader Size:**
    /// The subheader size is format-dependent: 3 * IntegerSize where IntegerSize
    /// is 4 bytes for 32-bit format and 8 bytes for 64-bit format.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create a mixed page starting from row 1000 in the dataset
    /// var mixPage = new MixDataPage(pageBuffer, metadata, decompressor, 1000);
    /// 
    /// Console.WriteLine($"Mixed page contains {mixPage._rowCount} rows");
    /// Console.WriteLine($"Data starts at offset {mixPage._dataStartOffset}");
    /// </code>
    /// </example>
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

    /// <summary>
    /// Enumerates the data rows contained in the data section of this mixed page.
    /// </summary>
    /// <returns>
    /// An enumerable sequence of ReadOnlyMemory&lt;byte&gt; instances, where each instance
    /// represents one complete data row from the page's data section.
    /// </returns>
    /// <remarks>
    /// This method provides access to the observation rows stored in the data section
    /// of the mixed page. The enumeration process:
    /// 
    /// **Row Extraction:**
    /// <list type="number">
    /// <item><description>Start at _dataStartOffset (after subheaders and alignment)</description></item>
    /// <item><description>Extract rows sequentially, each of length Metadata.RowLength</description></item>
    /// <item><description>Continue for _rowCount rows or until page boundary</description></item>
    /// <item><description>Perform boundary checking to prevent buffer overruns</description></item>
    /// </list>
    /// 
    /// **Boundary Validation:**
    /// The method includes safety checks to ensure that row extraction doesn't exceed
    /// the page buffer boundaries. If a row would extend beyond the buffer, enumeration
    /// stops gracefully to prevent memory access violations.
    /// 
    /// **Performance Characteristics:**
    /// <list type="bullet">
    /// <item><description>Lazy enumeration - rows are yielded on demand</description></item>
    /// <item><description>No memory copying - returns slices of the original buffer</description></item>
    /// <item><description>O(1) access time per row with early termination on boundary</description></item>
    /// </list>
    /// 
    /// **Memory Management:**
    /// Returned memory segments are slices of the original page buffer and remain valid
    /// as long as the page buffer is not disposed. No additional memory allocation
    /// occurs during enumeration.
    /// 
    /// **Error Handling:**
    /// The method uses defensive programming practices:
    /// <list type="bullet">
    /// <item><description>Boundary checking prevents buffer overruns</description></item>
    /// <item><description>Early termination on invalid conditions</description></item>
    /// <item><description>Graceful handling of edge cases</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var mixPage = new MixDataPage(pageBuffer, metadata, decompressor, 500);
    /// 
    /// foreach (var row in mixPage.EnumerateRows())
    /// {
    ///     // Each row contains all columns for one observation
    ///     var rowSpan = row.Span;
    ///     
    ///     // Extract individual columns based on metadata
    ///     foreach (var column in columns)
    ///     {
    ///         var columnData = rowSpan.Slice(column.Offset, column.Length);
    ///         // Process column data...
    ///     }
    /// }
    /// </code>
    /// </example>
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