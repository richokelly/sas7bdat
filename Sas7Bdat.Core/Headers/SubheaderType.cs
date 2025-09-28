namespace Sas7Bdat.Core.Headers;

/// <summary>
/// Defines the types of subheaders that can be found in SAS7BDAT file pages.
/// </summary>
/// <remarks>
/// SAS7BDAT files organize metadata into various types of subheaders, each containing
/// specific categories of information about the dataset structure and content.
/// Understanding subheader types is essential for correctly parsing and extracting
/// metadata from SAS files.
/// 
/// Subheaders are identified by unique byte signatures and contain different aspects
/// of the dataset schema:
/// <list type="bullet">
/// <item><description>Structural information (row/column counts, sizes)</description></item>
/// <item><description>Column definitions (names, types, formats, labels)</description></item>
/// <item><description>Storage specifications (offsets, lengths, data types)</description></item>
/// <item><description>Organizational metadata (counts, lists, text blocks)</description></item>
/// </list>
/// 
/// The parsing order and relationships between subheader types are important:
/// ColumnText must be processed before ColumnName, ColumnAttributes before column
/// assembly, etc. Some subheaders are optional and may not appear in all files.
/// </remarks>
internal enum SubheaderType
{
    /// <summary>
    /// Unknown or unrecognized subheader type.
    /// </summary>
    /// <remarks>
    /// This value is used when a subheader signature doesn't match any known pattern.
    /// Unknown subheaders are typically skipped during processing to avoid parsing errors.
    /// This can occur with:
    /// <list type="bullet">
    /// <item><description>Corrupted file data</description></item>
    /// <item><description>Newer SAS format versions with unrecognized subheaders</description></item>
    /// <item><description>Proprietary or extended subheader types</description></item>
    /// </list>
    /// </remarks>
    Unknown,

    /// <summary>
    /// Contains row and column count information, row length, and character set parameters.
    /// </summary>
    /// <remarks>
    /// The RowSize subheader provides fundamental structural information about the dataset:
    /// <list type="bullet">
    /// <item><description>Row length: The fixed width of each data row in bytes</description></item>
    /// <item><description>Row count: Total number of observations in the dataset</description></item>
    /// <item><description>Column counts: Multiple counts for validation and organization</description></item>
    /// <item><description>LCS/LCP: Character set and page parameters affecting text processing</description></item>
    /// <item><description>Mixed page row count: Rows per page in mixed-content pages</description></item>
    /// </list>
    /// 
    /// This subheader is typically one of the first processed as it provides essential
    /// information needed for interpreting other metadata and data pages.
    /// </remarks>
    RowSize,

    /// <summary>
    /// Specifies the total number of columns in the dataset.
    /// </summary>
    /// <remarks>
    /// The ColumnSize subheader provides the definitive count of variables in the dataset.
    /// This information is used to:
    /// <list type="bullet">
    /// <item><description>Validate that all column metadata has been collected</description></item>
    /// <item><description>Initialize data structures for column information storage</description></item>
    /// <item><description>Ensure consistent processing across all column-related subheaders</description></item>
    /// </list>
    /// 
    /// The column count from this subheader should match the number of columns
    /// defined in other subheaders (ColumnName, ColumnAttributes, etc.).
    /// </remarks>
    ColumnSize,

    /// <summary>
    /// Contains counts of various subheader types within the file.
    /// </summary>
    /// <remarks>
    /// The SubheaderCounts subheader provides statistical information about the
    /// file structure, including how many instances of each subheader type exist.
    /// This information can be used for:
    /// <list type="bullet">
    /// <item><description>Validation that all expected subheaders have been found</description></item>
    /// <item><description>Performance optimization by pre-allocating data structures</description></item>
    /// <item><description>Diagnostic information for debugging file parsing issues</description></item>
    /// </list>
    /// 
    /// While not strictly necessary for basic dataset reading, this information
    /// helps ensure complete and accurate metadata extraction.
    /// </remarks>
    SubheaderCounts,

    /// <summary>
    /// Contains raw text blocks with column names, formats, labels, and system information.
    /// </summary>
    /// <remarks>
    /// The ColumnText subheader stores concatenated text data that is referenced by
    /// other subheaders using indices and offsets. It contains:
    /// <list type="bullet">
    /// <item><description>Column names as null-terminated or length-prefixed strings</description></item>
    /// <item><description>Format specifications for data presentation</description></item>
    /// <item><description>Descriptive labels for columns</description></item>
    /// <item><description>System information (compression algorithms, creator details)</description></item>
    /// </list>
    /// 
    /// The first ColumnText subheader often contains special system information:
    /// <list type="bullet">
    /// <item><description>Compression algorithm identifiers (SASYZCRL, SASYZCR2)</description></item>
    /// <item><description>Creator software and procedure information</description></item>
    /// <item><description>Additional file creation metadata</description></item>
    /// </list>
    /// 
    /// This subheader must be processed before others that reference its text content.
    /// </remarks>
    ColumnText,

    /// <summary>
    /// Maps column indices to names within the ColumnText blocks.
    /// </summary>
    /// <remarks>
    /// The ColumnName subheader contains a series of entries that specify how to extract
    /// column names from the ColumnText subheaders. Each entry includes:
    /// <list type="bullet">
    /// <item><description>Text block index: Which ColumnText subheader contains the name</description></item>
    /// <item><description>Offset: Character position where the name begins</description></item>
    /// <item><description>Length: Number of characters in the name</description></item>
    /// </list>
    /// 
    /// The entries appear in column order, allowing the reconstruction of the complete
    /// column name list in the correct sequence. This indirect referencing system
    /// allows efficient storage of text data while maintaining flexibility in
    /// organization and formatting.
    /// 
    /// Processing requirements:
    /// <list type="bullet">
    /// <item><description>ColumnText subheaders must be processed first</description></item>
    /// <item><description>Entries should be processed in the order they appear</description></item>
    /// <item><description>Invalid references should be handled gracefully</description></item>
    /// </list>
    /// </remarks>
    ColumnName,

    /// <summary>
    /// Defines storage characteristics for each column including offsets, lengths, and data types.
    /// </summary>
    /// <remarks>
    /// The ColumnAttributes subheader provides the physical storage specification for
    /// each column within data rows. This information is essential for reading and
    /// interpreting the actual data values. Each column entry contains:
    /// <list type="bullet">
    /// <item><description>Data offset: Byte position where the column data begins within each row</description></item>
    /// <item><description>Data length: Number of bytes the column occupies in each row</description></item>
    /// <item><description>Storage type: Whether data is stored as numeric (1) or string (other values)</description></item>
    /// </list>
    /// 
    /// The storage type determines the basic parsing approach:
    /// <list type="bullet">
    /// <item><description>Numeric storage: Data is stored in binary format (typically IEEE 754 doubles)</description></item>
    /// <item><description>String storage: Data is stored as character bytes using the file's encoding</description></item>
    /// </list>
    /// 
    /// This information is combined with format specifications from FormatAndLabel
    /// subheaders to determine the final column type (String, Number, DateTime, Date, Time).
    /// The offset and length values enable direct access to column data within fixed-width rows.
    /// 
    /// Processing considerations:
    /// <list type="bullet">
    /// <item><description>Entries should appear in column order</description></item>
    /// <item><description>Offsets should not overlap between columns</description></item>
    /// <item><description>Total row length should accommodate all column data</description></item>
    /// </list>
    /// </remarks>
    ColumnAttributes,

    /// <summary>
    /// Associates format specifications and descriptive labels with columns.
    /// </summary>
    /// <remarks>
    /// The FormatAndLabel subheader provides presentation and documentation information
    /// for columns. Like ColumnName, it uses indirect references into ColumnText blocks
    /// to specify format and label strings. Each entry contains:
    /// 
    /// **Format Information:**
    /// <list type="bullet">
    /// <item><description>Format index: Which ColumnText block contains the format</description></item>
    /// <item><description>Format offset: Character position where the format begins</description></item>
    /// <item><description>Format length: Number of characters in the format specification</description></item>
    /// </list>
    /// 
    /// **Label Information:**
    /// <list type="bullet">
    /// <item><description>Label index: Which ColumnText block contains the label</description></item>
    /// <item><description>Label offset: Character position where the label begins</description></item>
    /// <item><description>Label length: Number of characters in the label text</description></item>
    /// </list>
    /// 
    /// **Format Specifications:**
    /// SAS format strings indicate how data should be presented and can influence
    /// type inference:
    /// <list type="bullet">
    /// <item><description>Numeric formats: F8.2, COMMA10.2, DOLLAR12.2, etc.</description></item>
    /// <item><description>Date formats: DATE9., MMDDYY10., DATETIME20., etc.</description></item>
    /// <item><description>Time formats: TIME8., TIMEAMPM11., etc.</description></item>
    /// <item><description>Character formats: $CHAR20., $HEX8., etc.</description></item>
    /// </list>
    /// 
    /// Labels provide human-readable descriptions of column content and are used
    /// for documentation and reporting purposes.
    /// </remarks>
    FormatAndLabel,

    /// <summary>
    /// Contains organizational information about column arrangement and grouping.
    /// </summary>
    /// <remarks>
    /// The ColumnList subheader provides additional organizational metadata about
    /// how columns are arranged within the dataset. This may include:
    /// <list type="bullet">
    /// <item><description>Column grouping information</description></item>
    /// <item><description>Alternative column ordering specifications</description></item>
    /// <item><description>Column relationship metadata</description></item>
    /// </list>
    /// 
    /// This subheader type is less commonly used and may not appear in all SAS files.
    /// When present, it typically provides supplementary information that doesn't
    /// affect basic data reading but may influence advanced processing or presentation.
    /// 
    /// Processing of ColumnList subheaders is often optional for basic dataset access,
    /// but may be important for maintaining complete metadata fidelity in file
    /// conversion or analysis applications.
    /// </remarks>
    ColumnList,

    /// <summary>
    /// Indicates subheaders containing actual data rather than metadata.
    /// </summary>
    /// <remarks>
    /// The Data subheader type is used to identify subheaders that contain actual
    /// dataset observations rather than structural metadata. When encountered during
    /// metadata processing, these subheaders typically signal the end of the metadata
    /// section and the beginning of data content.
    /// 
    /// Characteristics of Data subheaders:
    /// <list type="bullet">
    /// <item><description>Contain actual row data organized according to column specifications</description></item>
    /// <item><description>Use the row length and column layout defined in metadata subheaders</description></item>
    /// <item><description>May be compressed using algorithms specified in ColumnText subheaders</description></item>
    /// <item><description>Signal transition from metadata processing to data extraction</description></item>
    /// </list>
    /// 
    /// During metadata extraction, encountering Data subheaders usually indicates
    /// that sufficient structural information has been collected and data processing
    /// can begin. The metadata reader typically stops processing when it encounters
    /// pages containing only Data subheaders.
    /// </remarks>
    Data
}