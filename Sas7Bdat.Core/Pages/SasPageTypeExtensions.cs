namespace Sas7Bdat.Core.Pages;

/// <summary>
/// Provides extension methods for convenient testing of SasPageType characteristics.
/// </summary>
/// <remarks>
/// This static class provides a set of convenience methods that simplify the testing
/// of page type characteristics using the flag-based SasPageType enumeration. These
/// methods encapsulate the bitwise operations needed to check for specific page types
/// and characteristics, making the code more readable and maintainable.
/// 
/// **Design Benefits:**
/// <list type="bullet">
/// <item><description>Improved code readability with descriptive method names</description></item>
/// <item><description>Encapsulation of bitwise flag operations</description></item>
/// <item><description>Consistent testing patterns across the codebase</description></item>
/// <item><description>Reduced chance of errors in flag manipulation</description></item>
/// </list>
/// 
/// **Usage Pattern:**
/// These methods are typically used in page processing logic where different
/// handling is required based on page characteristics:
/// 
/// <code>
/// if (pageType.IsDataPage())
/// {
///     // Handle data pages
/// }
/// else if (pageType.IsMetaPage())
/// {
///     // Handle metadata pages
/// }
/// else if (pageType.IsMixPage())
/// {
///     // Handle mixed pages
/// }
/// </code>
/// </remarks>
/// <example>
/// <code>
/// var pageType = (SasPageType)metadata.Endianness.ReadUInt16At(buffer, offset);
/// 
/// // Test for specific page types
/// if (pageType.IsDataPage())
/// {
///     Console.WriteLine("Processing data page");
/// }
/// 
/// // Test for page characteristics
/// if (pageType.HasDeletedRecords())
/// {
///     Console.WriteLine("Page contains deleted records");
/// }
/// 
/// if (pageType.IsCompressed())
/// {
///     Console.WriteLine("Page content is compressed");
/// }
/// </code>
/// </example>
internal static class SasPageTypeExtensions
{
    /// <summary>
    /// Determines whether the page type represents a data page.
    /// </summary>
    /// <param name="pageType">The page type to test.</param>
    /// <returns>
    /// true if the page type has the Data flag set, indicating it contains observation data;
    /// otherwise, false.
    /// </returns>
    /// <remarks>
    /// This method tests for the presence of the Data flag (0x0100) in the page type value.
    /// Data pages contain observation rows and may have additional modifier flags such as
    /// HasDeleted or Compressed.
    /// 
    /// **Detected Page Types:**
    /// <list type="bullet">
    /// <item><description>SasPageType.Data (256): Pure data pages</description></item>
    /// <item><description>SasPageType.DataWithDeleted (384): Data pages with deleted records</description></item>
    /// <item><description>Any combination with Data flag: Data | Compressed, etc.</description></item>
    /// </list>
    /// 
    /// **Usage Context:**
    /// This method is used in page processing logic to determine if a page should be
    /// handled by DataDataPage or similar data-focused processing logic.
    /// </remarks>
    /// <example>
    /// <code>
    /// if (pageType.IsDataPage())
    /// {
    ///     var dataPage = new DataDataPage(buffer, metadata, decompressor);
    ///     ProcessDataRows(dataPage.EnumerateRows());
    /// }
    /// </code>
    /// </example>
    public static bool IsDataPage(this SasPageType pageType)
    {
        return pageType.HasFlag(SasPageType.Data);
    }

    /// <summary>
    /// Determines whether the page type represents a pure metadata page.
    /// </summary>
    /// <param name="pageType">The page type to test.</param>
    /// <returns>
    /// true if the page type is exactly Meta (0), indicating a pure metadata page;
    /// otherwise, false.
    /// </returns>
    /// <remarks>
    /// This method tests for an exact match with SasPageType.Meta, which represents
    /// pages containing only metadata subheaders without any observation data.
    /// 
    /// **Important Note:**
    /// This method uses exact equality rather than flag testing because Meta has a
    /// value of 0, and flag testing would incorrectly match other page types that
    /// don't have specific flags set.
    /// 
    /// **Detected Page Types:**
    /// <list type="bullet">
    /// <item><description>SasPageType.Meta (0): Pure metadata pages only</description></item>
    /// </list>
    /// 
    /// **Not Detected:**
    /// <list type="bullet">
    /// <item><description>MetadataContinuation pages (different base type)</description></item>
    /// <item><description>AMD pages (different base type)</description></item>
    /// <item><description>Mixed pages (contain both metadata and data)</description></item>
    /// </list>
    /// 
    /// **Usage Context:**
    /// This method is used to identify pages that contain only structural metadata
    /// and should be processed by MetaDataPage for subheader extraction.
    /// </remarks>
    /// <example>
    /// <code>
    /// if (pageType.IsMetaPage())
    /// {
    ///     var metaPage = new MetaDataPage(buffer, metadata, decompressor);
    ///     ProcessMetadataSubheaders(metaPage);
    /// }
    /// </code>
    /// </example>
    public static bool IsMetaPage(this SasPageType pageType)
    {
        return pageType == SasPageType.Meta;
    }

    /// <summary>
    /// Determines whether the page type represents a mixed page.
    /// </summary>
    /// <param name="pageType">The page type to test.</param>
    /// <returns>
    /// true if the page type has the Mix flag set, indicating it contains both
    /// metadata subheaders and observation data; otherwise, false.
    /// </returns>
    /// <remarks>
    /// This method tests for the presence of the Mix flag (0x0200) in the page type value.
    /// Mixed pages contain both subheaders with metadata and a data section with
    /// observation rows, typically appearing in the transition between pure metadata
    /// and pure data sections of the file.
    /// 
    /// **Detected Page Types:**
    /// <list type="bullet">
    /// <item><description>SasPageType.Mix (512): Standard mixed pages</description></item>
    /// <item><description>SasPageType.Mix2 (640): Extended format mixed pages</description></item>
    /// <item><description>Any combination with Mix flag: Mix | Compressed, etc.</description></item>
    /// </list>
    /// 
    /// **Page Structure:**
    /// Mixed pages have a more complex structure than pure data or metadata pages:
    /// <list type="number">
    /// <item><description>Page header with type and subheader count</description></item>
    /// <item><description>Subheader descriptors</description></item>
    /// <item><description>Subheader content (metadata)</description></item>
    /// <item><description>Alignment to 8-byte boundary</description></item>
    /// <item><description>Data section with observation rows</description></item>
    /// </list>
    /// 
    /// **Usage Context:**
    /// This method is used to identify pages that require MixDataPage processing
    /// to handle both the metadata and data content appropriately.
    /// </remarks>
    /// <example>
    /// <code>
    /// if (pageType.IsMixPage())
    /// {
    ///     var mixPage = new MixDataPage(buffer, metadata, decompressor, currentRow);
    ///     ProcessMixedContent(mixPage);
    /// }
    /// </code>
    /// </example>
    public static bool IsMixPage(this SasPageType pageType)
    {
        return pageType.HasFlag(SasPageType.Mix);
    }

    /// <summary>
    /// Determines whether the page contains deleted records.
    /// </summary>
    /// <param name="pageType">The page type to test.</param>
    /// <returns>
    /// true if the page type has the HasDeleted flag set, indicating the page
    /// contains records marked as deleted; otherwise, false.
    /// </returns>
    /// <remarks>
    /// This method tests for the presence of the HasDeleted flag (0x0080) in the page type.
    /// Pages with deleted records may require special handling during data processing
    /// to identify, skip, or specially process the deleted entries.
    /// 
    /// **Detected Page Types:**
    /// <list type="bullet">
    /// <item><description>SasPageType.DataWithDeleted (384): Data pages with deleted records</description></item>
    /// <item><description>Any page type combined with HasDeleted flag</description></item>
    /// </list>
    /// 
    /// **Processing Implications:**
    /// When a page has deleted records:
    /// <list type="bullet">
    /// <item><description>Row enumeration may need to identify deleted entries</description></item>
    /// <item><description>Row counts may not match actual usable data</description></item>
    /// <item><description>Additional filtering logic may be required</description></item>
    /// <item><description>Data validation should account for gaps in the data</description></item>
    /// </list>
    /// 
    /// **Note:**
    /// The same bit (0x0080) is also used for the Extended flag in different contexts.
    /// This method specifically tests for the deleted records interpretation.
    /// </remarks>
    /// <example>
    /// <code>
    /// if (pageType.HasDeletedRecords())
    /// {
    ///     Console.WriteLine("Warning: Page contains deleted records");
    ///     // May need special handling for deleted record identification
    /// }
    /// </code>
    /// </example>
    public static bool HasDeletedRecords(this SasPageType pageType)
    {
        return pageType.HasFlag(SasPageType.HasDeleted);
    }

    /// <summary>
    /// Determines whether the page content is compressed.
    /// </summary>
    /// <param name="pageType">The page type to test.</param>
    /// <returns>
    /// true if the page type has the Compressed flag set, indicating the page
    /// content requires decompression; otherwise, false.
    /// </returns>
    /// <remarks>
    /// This method tests for the presence of the Compressed flag (0x1000) in the page type.
    /// Compressed pages require decompression using the algorithm specified in the file
    /// metadata (RLE or RDC) before the content can be processed.
    /// 
    /// **Detected Page Types:**
    /// <list type="bullet">
    /// <item><description>SasPageType.SpecialCompressed (36864): Compressed special pages</description></item>
    /// <item><description>Any page type combined with Compressed flag</description></item>
    /// </list>
    /// 
    /// **Compression Scope:**
    /// The compression may apply to different parts of the page depending on the page type:
    /// <list type="bullet">
    /// <item><description>Data pages: Observation rows may be compressed</description></item>
    /// <item><description>Meta pages: Subheader content may be compressed</description></item>
    /// <item><description>Mixed pages: Either subheaders or data section may be compressed</description></item>
    /// </list>
    /// 
    /// **Processing Requirements:**
    /// When compression is detected:
    /// <list type="bullet">
    /// <item><description>Appropriate decompressor must be available</description></item>
    /// <item><description>Decompression must occur before content interpretation</description></item>
    /// <item><description>Additional buffer space may be needed for decompressed data</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// if (pageType.IsCompressed())
    /// {
    ///     Console.WriteLine("Page contains compressed data");
    ///     // Ensure decompressor is available and appropriate
    ///     if (decompressor is NoDecompressor)
    ///     {
    ///         throw new InvalidOperationException("Compressed page requires decompressor");
    ///     }
    /// }
    /// </code>
    /// </example>
    public static bool IsCompressed(this SasPageType pageType)
    {
        return pageType.HasFlag(SasPageType.Compressed);
    }

    /// <summary>
    /// Determines whether the page uses extended format features.
    /// </summary>
    /// <param name="pageType">The page type to test.</param>
    /// <returns>
    /// true if the page type has the Extended flag set, indicating the page
    /// uses extended format capabilities; otherwise, false.
    /// </returns>
    /// <remarks>
    /// This method tests for the presence of the Extended flag (0x0080) in the page type.
    /// Extended pages may have enhanced features, modified layouts, or additional
    /// functionality compared to their standard counterparts.
    /// 
    /// **Detected Page Types:**
    /// <list type="bullet">
    /// <item><description>SasPageType.Mix2 (640): Extended format mixed pages</description></item>
    /// <item><description>Any page type combined with Extended flag</description></item>
    /// </list>
    /// 
    /// **Extended Features:**
    /// Extended format pages may include:
    /// <list type="bullet">
    /// <item><description>Enhanced subheader structures</description></item>
    /// <item><description>Modified data organization</description></item>
    /// <item><description>Additional metadata capabilities</description></item>
    /// <item><description>Version-specific enhancements</description></item>
    /// </list>
    /// 
    /// **Important Note:**
    /// This is the same bit as HasDeleted (0x0080), but interpreted differently
    /// based on context. This method specifically tests for the extended format
    /// interpretation.
    /// 
    /// **Processing Considerations:**
    /// Extended pages may require:
    /// <list type="bullet">
    /// <item><description>Modified parsing logic for enhanced structures</description></item>
    /// <item><description>Version-aware processing</description></item>
    /// <item><description>Fallback handling for unsupported extensions</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// if (pageType.IsExtended())
    /// {
    ///     Console.WriteLine("Page uses extended format");
    ///     // May need enhanced processing logic
    /// }
    /// </code>
    /// </example>
    public static bool IsExtended(this SasPageType pageType)
    {
        return pageType.HasFlag(SasPageType.Extended);
    }
}