namespace Sas7Bdat.Core.Pages;

/// <summary>
/// Defines the types and characteristics of pages in SAS7BDAT files using flag-based enumeration.
/// </summary>
/// <remarks>
/// SasPageType uses a flag-based enumeration system that allows pages to have multiple
/// characteristics simultaneously. The enum combines base page types with modifier flags
/// to create a comprehensive classification system for all possible page variations.
/// 
/// **Design Pattern:**
/// The enumeration uses bit flags where:
/// <list type="bullet">
/// <item><description>Base types are mutually exclusive (Meta, Data, Mix, etc.)</description></item>
/// <item><description>Modifier flags can be combined with base types</description></item>
/// <item><description>Some combinations create well-known composite types</description></item>
/// </list>
/// 
/// **Page Type Hierarchy:**
/// <list type="bullet">
/// <item><description>Meta (0): Pure metadata pages with subheaders only</description></item>
/// <item><description>Data (256): Pure data pages with observation rows only</description></item>
/// <item><description>Mix (512): Mixed pages with both subheaders and data</description></item>
/// <item><description>AMD (1024): Attribute metadata pages</description></item>
/// <item><description>MetadataContinuation (16384): Extended metadata pages</description></item>
/// <item><description>Special (32768): Special-purpose pages</description></item>
/// </list>
/// 
/// **Usage in Practice:**
/// Page types are detected by reading a 16-bit value from the page header and
/// interpreting it according to this enumeration. Extension methods provide
/// convenient ways to test for specific page characteristics.
/// </remarks>
[Flags]
public enum SasPageType : ushort
{
    /// <summary>
    /// Metadata page containing subheaders with column definitions and file information.
    /// </summary>
    /// <value>0 (0x0000)</value>
    /// <remarks>
    /// Meta pages appear primarily at the beginning of SAS files and contain subheaders
    /// with metadata about columns, formats, labels, and other structural information.
    /// These pages typically don't contain observation data, focusing instead on
    /// defining the dataset schema and characteristics.
    /// 
    /// Common subheader types found in Meta pages:
    /// <list type="bullet">
    /// <item><description>RowSize: Dataset structure information</description></item>
    /// <item><description>ColumnSize: Number of columns</description></item>
    /// <item><description>ColumnText: Raw text for names, formats, labels</description></item>
    /// <item><description>ColumnName: Column name mappings</description></item>
    /// <item><description>ColumnAttributes: Storage specifications</description></item>
    /// <item><description>FormatAndLabel: Display formats and labels</description></item>
    /// </list>
    /// </remarks>
    Meta = 0x0000,                  // 0

    /// <summary>
    /// Data page containing observation rows without metadata subheaders.
    /// </summary>
    /// <value>256 (0x0100)</value>
    /// <remarks>
    /// Data pages contain the bulk of the dataset content - the actual observation rows
    /// organized according to the column layout defined in the metadata pages. These
    /// pages have no subheaders, just sequential rows of fixed-width data.
    /// 
    /// Characteristics of Data pages:
    /// <list type="bullet">
    /// <item><description>Contain only observation rows, no metadata</description></item>
    /// <item><description>Rows start immediately after the page header</description></item>
    /// <item><description>Each row has the length specified in file metadata</description></item>
    /// <item><description>Row count indicated by the page's BlockCount field</description></item>
    /// </list>
    /// 
    /// Data pages represent the majority of pages in large SAS datasets.
    /// </remarks>
    Data = 0x0100,                  // 256

    /// <summary>
    /// Mixed page containing both metadata subheaders and observation data.
    /// </summary>
    /// <value>512 (0x0200)</value>
    /// <remarks>
    /// Mix pages combine the functionality of Meta and Data pages, containing both
    /// subheaders with metadata and a data section with observation rows. These pages
    /// typically appear in the transition area between pure metadata and pure data
    /// sections of the file.
    /// 
    /// Structure of Mix pages:
    /// <list type="number">
    /// <item><description>Page header with type and counts</description></item>
    /// <item><description>Subheader descriptors</description></item>
    /// <item><description>Subheader content (metadata)</description></item>
    /// <item><description>Alignment padding to 8-byte boundary</description></item>
    /// <item><description>Data section with observation rows</description></item>
    /// </list>
    /// 
    /// The data section capacity is calculated based on remaining space after
    /// accounting for subheaders and alignment requirements.
    /// </remarks>
    Mix = 0x0200,                   // 512

    /// <summary>
    /// Attribute metadata page containing detailed column and format information.
    /// </summary>
    /// <value>1024 (0x0400)</value>
    /// <remarks>
    /// AMD (Attribute Metadata) pages contain detailed information about column
    /// attributes, formats, and other extended metadata that supplements the basic
    /// column definitions found in regular Meta pages.
    /// 
    /// AMD pages may contain:
    /// <list type="bullet">
    /// <item><description>Extended column attribute information</description></item>
    /// <item><description>Detailed format specifications</description></item>
    /// <item><description>Additional metadata not found in basic Meta pages</description></item>
    /// <item><description>Cross-references to other metadata structures</description></item>
    /// </list>
    /// 
    /// These pages are processed during the metadata reading phase to gather
    /// comprehensive column information.
    /// </remarks>
    Amd = 0x0400,                   // 1024

    /// <summary>
    /// Continuation of metadata information across multiple pages.
    /// </summary>
    /// <value>16384 (0x4000)</value>
    /// <remarks>
    /// MetadataContinuation pages are used when metadata information spans multiple
    /// pages due to large amounts of column information, extensive text data, or
    /// other factors that exceed the capacity of a single metadata page.
    /// 
    /// These pages contain:
    /// <list type="bullet">
    /// <item><description>Continuation of subheaders from previous metadata pages</description></item>
    /// <item><description>Additional column information</description></item>
    /// <item><description>Extended text blocks for names, formats, and labels</description></item>
    /// <item><description>References linking back to primary metadata</description></item>
    /// </list>
    /// 
    /// Processing requires coordination with preceding metadata pages to assemble
    /// complete column information.
    /// </remarks>
    MetadataContinuation = 0x4000,  // 16384

    /// <summary>
    /// Special-purpose page with implementation-specific content.
    /// </summary>
    /// <value>32768 (0x8000)</value>
    /// <remarks>
    /// Special pages contain implementation-specific content that doesn't fit into
    /// the standard Meta, Data, or Mix categories. The exact content and purpose
    /// depend on the specific SAS implementation and file version.
    /// 
    /// These pages may contain:
    /// <list type="bullet">
    /// <item><description>Extended file metadata</description></item>
    /// <item><description>Implementation-specific control information</description></item>
    /// <item><description>Proprietary data structures</description></item>
    /// <item><description>Version-specific extensions</description></item>
    /// </list>
    /// 
    /// Special pages are typically skipped during standard dataset reading operations.
    /// </remarks>
    Special = 0x8000,               // 32768

    // Modifier flags that can be combined with base types

    /// <summary>
    /// Modifier flag indicating the page contains deleted records or uses extended format.
    /// </summary>
    /// <value>128 (0x0080)</value>
    /// <remarks>
    /// This flag has context-dependent meanings:
    /// 
    /// **When combined with Data pages (HasDeleted):**
    /// Indicates that the page contains records that have been marked as deleted
    /// but not physically removed from the file. These records may need special
    /// handling during data processing.
    /// 
    /// **When combined with other page types (Extended):**
    /// Indicates that the page uses an extended format with additional capabilities
    /// or modified structure compared to the standard format.
    /// 
    /// The interpretation depends on the base page type and context within the file.
    /// </remarks>
    HasDeleted = 0x0080,     // 128 - pages with deleted records

    /// <summary>
    /// Alias for HasDeleted when used in extended format contexts.
    /// </summary>
    /// <value>128 (0x0080)</value>
    /// <remarks>
    /// This is the same bit as HasDeleted but used when the context indicates
    /// extended format rather than deleted records. The interpretation depends
    /// on the base page type and file characteristics.
    /// </remarks>
    Extended = 0x0080,       // 128 - extended format (same bit, context-dependent)

    /// <summary>
    /// Modifier flag indicating the page content is compressed.
    /// </summary>
    /// <value>4096 (0x1000)</value>
    /// <remarks>
    /// When this flag is set, the page content (or portions of it) is compressed
    /// using the compression algorithm specified in the file metadata (RLE or RDC).
    /// Compressed content must be decompressed before processing.
    /// 
    /// Compression can apply to:
    /// <list type="bullet">
    /// <item><description>Data rows within the page</description></item>
    /// <item><description>Subheader content</description></item>
    /// <item><description>Mixed content depending on subheader flags</description></item>
    /// </list>
    /// 
    /// The specific compression handling depends on the page type and individual
    /// subheader compression flags.
    /// </remarks>
    Compressed = 0x1000,     // 4096 - compressed data

    // Known combinations that appear frequently

    /// <summary>
    /// Data page containing deleted records.
    /// </summary>
    /// <value>384 (Data | HasDeleted)</value>
    /// <remarks>
    /// This combination indicates a data page that contains some records marked
    /// as deleted. During data processing, these deleted records may need to be
    /// identified and handled appropriately (skipped, flagged, etc.).
    /// 
    /// The presence of deleted records doesn't affect the basic page structure
    /// but may require additional logic during row enumeration to filter out
    /// or specially process the deleted entries.
    /// </remarks>
    DataWithDeleted = Data | HasDeleted,           // 384

    /// <summary>
    /// Mixed page using extended format.
    /// </summary>
    /// <value>640 (Mix | Extended)</value>
    /// <remarks>
    /// This combination indicates a mixed page that uses extended format capabilities.
    /// Mix2 pages may have enhanced features, modified layouts, or additional
    /// functionality compared to standard Mix pages.
    /// 
    /// Extended features may include:
    /// <list type="bullet">
    /// <item><description>Enhanced subheader structures</description></item>
    /// <item><description>Modified data organization</description></item>
    /// <item><description>Additional metadata capabilities</description></item>
    /// <item><description>Version-specific enhancements</description></item>
    /// </list>
    /// </remarks>
    Mix2 = Mix | Extended,                         // 640

    /// <summary>
    /// Special page with compressed content.
    /// </summary>
    /// <value>36864 (Special | Compressed)</value>
    /// <remarks>
    /// This combination indicates a special-purpose page where the content is compressed.
    /// Such pages require both special handling for the page type and decompression
    /// of the content before processing.
    /// 
    /// Processing considerations:
    /// <list type="bullet">
    /// <item><description>Must decompress content before interpretation</description></item>
    /// <item><description>May contain implementation-specific compressed data</description></item>
    /// <item><description>Typically skipped during standard dataset reading</description></item>
    /// </list>
    /// </remarks>
    SpecialCompressed = Special | Compressed,      // 36864

    /// <summary>
    /// Mask for extracting the base page type from a composite page type value.
    /// </summary>
    /// <value>65280 (0xFF00)</value>
    /// <remarks>
    /// This mask can be used with bitwise AND operations to extract the primary
    /// page type while ignoring modifier flags. This is useful for determining
    /// the fundamental page structure without considering additional characteristics.
    /// 
    /// Usage example:
    /// <code>
    /// var baseType = (SasPageType)((int)pageType &amp; (int)SasPageType.BaseTypeMask);
    /// </code>
    /// 
    /// The mask covers the upper 8 bits where base page types are defined,
    /// effectively filtering out modifier flags in the lower 8 bits.
    /// </remarks>
    BaseTypeMask = 0xFF00
}