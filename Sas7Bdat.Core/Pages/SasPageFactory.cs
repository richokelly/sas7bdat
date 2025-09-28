using Sas7Bdat.Core.Decompression;

namespace Sas7Bdat.Core.Pages;

/// <summary>
/// Provides factory methods for creating appropriate SasDataPage instances based on page type.
/// </summary>
/// <remarks>
/// SasPageFactory implements the factory pattern to create the correct page implementation
/// based on the page type detected in the page header. This approach encapsulates the
/// page type detection logic and ensures that each page is processed with the appropriate
/// implementation that understands its specific structure and content layout.
/// 
/// **Page Type Detection:**
/// The factory examines the page type field in the page header to determine which
/// concrete page class should be instantiated:
/// <list type="bullet">
/// <item><description>Data pages: Pages containing only observation rows</description></item>
/// <item><description>Meta pages: Pages containing metadata subheaders</description></item>
/// <item><description>Mix pages: Pages containing both subheaders and data rows</description></item>
/// <item><description>Unknown pages: Unrecognized page types (fallback)</description></item>
/// </list>
/// 
/// **Benefits of Factory Pattern:**
/// <list type="bullet">
/// <item><description>Centralized page creation logic</description></item>
/// <item><description>Type-safe page instantiation</description></item>
/// <item><description>Extensibility for new page types</description></item>
/// <item><description>Consistent parameter validation</description></item>
/// </list>
/// 
/// **Usage Context:**
/// The factory is typically used during file reading operations where pages are
/// processed sequentially and the appropriate handling logic must be selected
/// based on runtime examination of page headers.
/// </remarks>
/// <example>
/// <code>
/// // Read a page buffer from the file
/// var pageBuffer = new byte[metadata.PageLength];
/// await stream.ReadAsync(pageBuffer);
/// 
/// // Create the appropriate page instance
/// var page = SasPageFactory.CreatePage(pageBuffer, metadata, decompressor, currentRowIndex);
/// 
/// // Process rows according to the page type
/// foreach (var row in page.EnumerateRows())
/// {
///     ProcessRow(row);
/// }
/// </code>
/// </example>
internal static class SasPageFactory
{
    /// <summary>
    /// Creates an appropriate SasDataPage instance based on the page type detected in the buffer.
    /// </summary>
    /// <param name="pageBuffer">The memory buffer containing the complete page data.</param>
    /// <param name="metadata">The file metadata containing format and structure information.</param>
    /// <param name="decompressor">The decompressor for handling compressed content.</param>
    /// <param name="currentRow">The zero-based index of the current row position in the dataset (used for mixed pages).</param>
    /// <returns>
    /// A SasDataPage instance of the appropriate concrete type based on the detected page type.
    /// </returns>
    /// <remarks>
    /// This method implements the page type detection and instantiation logic:
    /// 
    /// **Buffer Validation:**
    /// <list type="bullet">
    /// <item><description>Verifies that the buffer size matches the expected page length</description></item>
    /// <item><description>Ensures sufficient data is available for page header parsing</description></item>
    /// </list>
    /// 
    /// **Page Type Detection:**
    /// <list type="number">
    /// <item><description>Calculate page bit offset based on file format (16 for 32-bit, 32 for 64-bit)</description></item>
    /// <item><description>Read page type from the header using the file's endianness</description></item>
    /// <item><description>Use extension methods to categorize the page type</description></item>
    /// </list>
    /// 
    /// **Page Instance Creation:**
    /// Based on the detected page type:
    /// <list type="bullet">
    /// <item><description>Data pages → DataDataPage for pure observation data</description></item>
    /// <item><description>Meta pages → MetaDataPage for metadata with potential embedded rows</description></item>
    /// <item><description>Mix pages → MixDataPage for combined metadata and data sections</description></item>
    /// <item><description>Unknown types → UnknownDataPage as a safe fallback</description></item>
    /// </list>
    /// 
    /// **Parameters for Mixed Pages:**
    /// The currentRow parameter is particularly important for mixed pages as it affects
    /// row count calculations and ensures that the page doesn't attempt to read beyond
    /// the actual dataset bounds.
    /// 
    /// **Error Handling:**
    /// <list type="bullet">
    /// <item><description>Buffer size validation prevents parsing errors</description></item>
    /// <item><description>Unknown page types are handled gracefully with UnknownDataPage</description></item>
    /// <item><description>All concrete page types handle their specific error conditions</description></item>
    /// </list>
    /// 
    /// **Performance Considerations:**
    /// <list type="bullet">
    /// <item><description>Minimal overhead for page type detection</description></item>
    /// <item><description>No unnecessary data copying or processing</description></item>
    /// <item><description>Efficient extension method usage for type categorization</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when the buffer size is smaller than the expected page length specified
    /// in the metadata, indicating insufficient data for proper page processing.
    /// </exception>
    /// <example>
    /// <code>
    /// // Create pages for different scenarios
    /// 
    /// // For data-only pages
    /// var dataPage = SasPageFactory.CreatePage(buffer, metadata, decompressor);
    /// 
    /// // For mixed pages with row tracking
    /// var mixPage = SasPageFactory.CreatePage(buffer, metadata, decompressor, 1000);
    /// 
    /// // Handle the created page polymorphically
    /// SasDataPage page = SasPageFactory.CreatePage(buffer, metadata, decompressor);
    /// switch (page)
    /// {
    ///     case DataDataPage dataPage:
    ///         Console.WriteLine("Processing pure data page");
    ///         break;
    ///     case MetaDataPage metaPage:
    ///         Console.WriteLine("Processing metadata page");
    ///         break;
    ///     case MixDataPage mixPage:
    ///         Console.WriteLine("Processing mixed page");
    ///         break;
    ///     case UnknownDataPage unknownPage:
    ///         Console.WriteLine("Skipping unknown page type");
    ///         break;
    /// }
    /// </code>
    /// </example>
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