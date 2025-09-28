namespace Sas7Bdat.Core.Headers;

/// <summary>
/// Provides signature-based identification of subheader types in SAS7BDAT files.
/// </summary>
/// <remarks>
/// SAS7BDAT files contain various types of subheaders, each identified by a unique byte signature
/// at the beginning of the subheader data. These signatures differ between 32-bit and 64-bit
/// file formats, requiring format-specific identification logic.
/// 
/// Subheaders contain different types of metadata:
/// <list type="bullet">
/// <item><description>RowSize: Row and column count information, row length specifications</description></item>
/// <item><description>ColumnSize: Total number of columns in the dataset</description></item>
/// <item><description>ColumnText: Raw text blocks containing names, formats, and labels</description></item>
/// <item><description>ColumnName: Mappings from column indices to names within text blocks</description></item>
/// <item><description>ColumnAttributes: Storage offsets, lengths, and data types</description></item>
/// <item><description>FormatAndLabel: Format specifications and descriptive labels</description></item>
/// <item><description>ColumnList: Column organization information</description></item>
/// <item><description>SubheaderCounts: Counts of various subheader types</description></item>
/// </list>
/// 
/// The signature-based identification is essential for correctly parsing subheader content
/// and extracting the appropriate metadata for dataset structure reconstruction.
/// </remarks>
internal static class SubheaderSignatures
{
    /// <summary>
    /// Contains format-specific constants for 32-bit SAS files.
    /// </summary>
    /// <remarks>
    /// These constants define byte offsets within subheaders that are specific to 32-bit
    /// SAS file format. The offsets account for the smaller pointer and integer sizes
    /// used in 32-bit format files.
    /// </remarks>
    public static class Format32
    {
        /// <summary>
        /// The byte offset for LCS (Length of Character Set) information in 32-bit format subheaders.
        /// </summary>
        /// <value>354 bytes from the start of the subheader.</value>
        /// <remarks>
        /// LCS affects how character data is stored and interpreted throughout the file.
        /// This offset is used to locate the LCS value within RowSize subheaders.
        /// </remarks>
        public const int LcsOffset = 354;

        /// <summary>
        /// The byte offset for LCP (Length of Character Page) information in 32-bit format subheaders.
        /// </summary>
        /// <value>378 bytes from the start of the subheader.</value>
        /// <remarks>
        /// LCP affects character page organization and text field processing.
        /// This offset is used to locate the LCP value within RowSize subheaders.
        /// </remarks>
        public const int LcpOffset = 378;

        /// <summary>
        /// The byte offset for compression and creator information in 32-bit format subheaders.
        /// </summary>
        /// <value>16 bytes from the start of the subheader.</value>
        /// <remarks>
        /// This offset is used within ColumnText subheaders to locate compression algorithm
        /// identifiers and creator/procedure information that describes how the file was created.
        /// </remarks>
        public const int CompressionOffset = 16;
    }

    /// <summary>
    /// Contains format-specific constants for 64-bit SAS files.
    /// </summary>
    /// <remarks>
    /// These constants define byte offsets within subheaders that are specific to 64-bit
    /// SAS file format. The offsets account for the larger pointer and integer sizes
    /// used in 64-bit format files, resulting in different field positioning.
    /// </remarks>
    public static class Format64
    {
        /// <summary>
        /// The byte offset for LCS (Length of Character Set) information in 64-bit format subheaders.
        /// </summary>
        /// <value>682 bytes from the start of the subheader.</value>
        /// <remarks>
        /// LCS affects how character data is stored and interpreted throughout the file.
        /// This offset is used to locate the LCS value within RowSize subheaders in 64-bit files.
        /// The larger offset compared to 32-bit format reflects the expanded data structures.
        /// </remarks>
        public const int LcsOffset = 682;

        /// <summary>
        /// The byte offset for LCP (Length of Character Page) information in 64-bit format subheaders.
        /// </summary>
        /// <value>706 bytes from the start of the subheader.</value>
        /// <remarks>
        /// LCP affects character page organization and text field processing.
        /// This offset is used to locate the LCP value within RowSize subheaders in 64-bit files.
        /// The larger offset compared to 32-bit format reflects the expanded data structures.
        /// </remarks>
        public const int LcpOffset = 706;

        /// <summary>
        /// The byte offset for compression and creator information in 64-bit format subheaders.
        /// </summary>
        /// <value>20 bytes from the start of the subheader.</value>
        /// <remarks>
        /// This offset is used within ColumnText subheaders to locate compression algorithm
        /// identifiers and creator/procedure information. The offset is slightly larger than
        /// in 32-bit format due to expanded header structures in 64-bit files.
        /// </remarks>
        public const int CompressionOffset = 20;
    }

    /// <summary>
    /// Identifies the type of a subheader based on its signature bytes and file format.
    /// </summary>
    /// <param name="signature">The signature bytes from the beginning of the subheader.</param>
    /// <param name="format">The file format (32-bit or 64-bit) that affects signature interpretation.</param>
    /// <returns>
    /// The identified SubheaderType, or SubheaderType.Unknown if the signature is not recognized.
    /// </returns>
    /// <remarks>
    /// This method performs signature matching using format-specific patterns to identify
    /// subheader types. The signatures are fixed byte sequences that uniquely identify
    /// each type of subheader within the SAS file format specification.
    /// 
    /// **64-bit Format Signatures:**
    /// The 64-bit format uses 8-byte signatures with multiple possible patterns for each type,
    /// accounting for different endianness and format variations:
    /// 
    /// <list type="bullet">
    /// <item><description>RowSize: F7 F7 F7 F7 patterns (with various complementary bytes)</description></item>
    /// <item><description>ColumnSize: F6 F6 F6 F6 patterns (with various complementary bytes)</description></item>
    /// <item><description>SubheaderCounts: FC patterns at specific positions</description></item>
    /// <item><description>ColumnText: FD patterns in the signature</description></item>
    /// <item><description>ColumnName: FF FF FF FF FF FF FF FF (all F bytes)</description></item>
    /// <item><description>ColumnAttributes: FC patterns at the end or beginning</description></item>
    /// <item><description>FormatAndLabel: FE FB patterns</description></item>
    /// <item><description>ColumnList: FE patterns at the end or beginning</description></item>
    /// </list>
    /// 
    /// **32-bit Format Signatures:**
    /// The 32-bit format uses 4-byte signatures with similar but shorter patterns:
    /// 
    /// <list type="bullet">
    /// <item><description>RowSize: F7 F7 F7 F7</description></item>
    /// <item><description>ColumnSize: F6 F6 F6 F6</description></item>
    /// <item><description>SubheaderCounts: FC patterns with FF bytes</description></item>
    /// <item><description>ColumnText: FD with trailing FF bytes</description></item>
    /// <item><description>ColumnName: FF FF FF FF (all F bytes)</description></item>
    /// <item><description>ColumnAttributes: FC with trailing FF bytes</description></item>
    /// <item><description>FormatAndLabel: FE FB patterns</description></item>
    /// <item><description>ColumnList: FE with trailing FF bytes</description></item>
    /// </list>
    /// 
    /// The method handles endianness variations by checking multiple signature patterns
    /// for each subheader type, ensuring compatibility with files created on different
    /// platforms and with different byte ordering.
    /// 
    /// **Error Handling:**
    /// When a signature doesn't match any known pattern, the method returns SubheaderType.Unknown,
    /// allowing the caller to handle unrecognized subheaders gracefully without causing
    /// parsing failures.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Read signature from subheader
    /// var signature = buffer.ReadBytesAt(subheaderOffset, integerSize);
    /// 
    /// // Identify subheader type
    /// var subheaderType = SubheaderSignatures.IdentifySubheader(signature, fileFormat);
    /// 
    /// // Process based on type
    /// switch (subheaderType)
    /// {
    ///     case SubheaderType.RowSize:
    ///         ProcessRowSizeSubheader(buffer, subheaderOffset);
    ///         break;
    ///     case SubheaderType.ColumnName:
    ///         ProcessColumnNameSubheader(buffer, subheaderOffset);
    ///         break;
    ///     // ... handle other types
    ///     case SubheaderType.Unknown:
    ///         // Skip unknown subheader
    ///         break;
    /// }
    /// </code>
    /// </example>
    public static SubheaderType IdentifySubheader(ReadOnlySpan<byte> signature, Format format)
    {
        if (format == Format.Bit64)
        {
            switch (signature)
            {
                case [0x00, 0x00, 0x00, 0x00, 0xF7, 0xF7, 0xF7, 0xF7]:
                case [0xF7, 0xF7, 0xF7, 0xF7, 0x00, 0x00, 0x00, 0x00]:
                case [0xF7, 0xF7, 0xF7, 0xF7, 0xFF, 0xFF, 0xFB, 0xFE]:
                case [0xFF, 0xFF, 0xFB, 0xFE, 0xF7, 0xF7, 0xF7, 0xF7]:
                    return SubheaderType.RowSize;
                case [0x00, 0x00, 0x00, 0x00, 0xF6, 0xF6, 0xF6, 0xF6]:
                case [0xF6, 0xF6, 0xF6, 0xF6, 0x00, 0x00, 0x00, 0x00]:
                case [0xF6, 0xF6, 0xF6, 0xF6, 0xFF, 0xFF, 0xFB, 0xFE]:
                case [0xFF, 0xFF, 0xFB, 0xFE, 0xF6, 0xF6, 0xF6, 0xF6]:
                    return SubheaderType.ColumnSize;
                case [0x00, 0xFC, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]:
                case [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFC, 0x00]:
                    return SubheaderType.SubheaderCounts;
                case [0xFD, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]:
                case [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFD]:
                    return SubheaderType.ColumnText;
                case [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]:
                    return SubheaderType.ColumnName;
                case [0xFC, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]:
                case [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFC]:
                    return SubheaderType.ColumnAttributes;
                case [0xFE, 0xFB, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]:
                case [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFB, 0xFE]:
                    return SubheaderType.FormatAndLabel;
                case [0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]:
                case [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFE]:
                    return SubheaderType.ColumnList;
            }
        }
        else
        {
            switch (signature)
            {
                case [0xF7, 0xF7, 0xF7, 0xF7]:
                    return SubheaderType.RowSize;
                case [0xF6, 0xF6, 0xF6, 0xF6]:
                    return SubheaderType.ColumnSize;
                case [0x00, 0xFC, 0xFF, 0xFF]:
                case [0xFF, 0xFF, 0xFC, 0x00]:
                    return SubheaderType.SubheaderCounts;
                case [0xFD, 0xFF, 0xFF, 0xFF]:
                case [0xFF, 0xFF, 0xFF, 0xFD]:
                    return SubheaderType.ColumnText;
                case [0xFF, 0xFF, 0xFF, 0xFF]:
                    return SubheaderType.ColumnName;
                case [0xFC, 0xFF, 0xFF, 0xFF]:
                case [0xFF, 0xFF, 0xFF, 0xFC]:
                    return SubheaderType.ColumnAttributes;
                case [0xFE, 0xFB, 0xFF, 0xFF]:
                case [0xFF, 0xFF, 0xFB, 0xFE]:
                    return SubheaderType.FormatAndLabel;
                case [0xFE, 0xFF, 0xFF, 0xFF]:
                case [0xFF, 0xFF, 0xFF, 0xFE]:
                    return SubheaderType.ColumnList;
            }
        }

        return SubheaderType.Unknown;
    }
}