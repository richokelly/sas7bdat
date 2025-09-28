namespace Sas7Bdat.Core.Headers;

/// <summary>
/// Defines constants used for identifying compression algorithms and subheader types in SAS7BDAT file headers.
/// </summary>
/// <remarks>
/// This class contains magic strings and identifier values that are embedded in SAS file headers
/// to indicate various file characteristics such as compression algorithms and subheader configurations.
/// These constants are essential for correctly parsing and interpreting the binary structure of SAS files.
/// 
/// The compression identifiers help determine which decompression algorithm should be used when
/// reading compressed data pages, while the subheader identifiers are used to classify different
/// types of metadata sections within the file structure.
/// </remarks>
internal sealed class HeaderConstants
{
    /// <summary>
    /// The magic string identifier for RLE (Run-Length Encoding) compression in SAS files.
    /// </summary>
    /// <value>The string "SASYZCRL" used to identify RLE compression.</value>
    /// <remarks>
    /// This string appears in the column text subheader when a SAS file uses RLE compression.
    /// When this identifier is found, the file's data pages are compressed using the RLE algorithm
    /// and must be decompressed using the corresponding RleDecompressor implementation.
    /// 
    /// RLE compression is effective for data with many repeated values or patterns.
    /// </remarks>
    public const string RleCompression = "SASYZCRL";

    /// <summary>
    /// The magic string identifier for RDC (Ross Data Compression) compression in SAS files.
    /// </summary>
    /// <value>The string "SASYZCR2" used to identify RDC compression.</value>
    /// <remarks>
    /// This string appears in the column text subheader when a SAS file uses RDC compression.
    /// When this identifier is found, the file's data pages are compressed using the RDC algorithm
    /// and must be decompressed using the corresponding RdcDecompressor implementation.
    /// 
    /// RDC is a proprietary compression algorithm developed by SAS that typically provides
    /// better compression ratios than RLE but may be slower to decompress.
    /// </remarks>
    public const string RdcCompression = "SASYZCR2";

    /// <summary>
    /// The identifier value for truncated subheaders in SAS file pages.
    /// </summary>
    /// <value>The integer value 1 indicating a truncated subheader.</value>
    /// <remarks>
    /// This constant is used in the compression field of page subheaders to indicate that
    /// the subheader has been truncated and should be skipped during processing.
    /// 
    /// Truncated subheaders typically occur when there isn't enough space on a page to
    /// contain the complete subheader information, and they don't contain useful data
    /// for metadata extraction.
    /// </remarks>
    public const int TruncatedSubheaderId = 1;

    /// <summary>
    /// The identifier value for compressed subheaders in SAS file pages.
    /// </summary>
    /// <value>The integer value 4 indicating a compressed subheader.</value>
    /// <remarks>
    /// This constant is used in the compression field of page subheaders to indicate that
    /// the subheader data is compressed and requires decompression before processing.
    /// 
    /// Compressed subheaders contain metadata that has been compressed using the same
    /// algorithm as the file's data pages and must be decompressed to extract meaningful
    /// column and structure information.
    /// </remarks>
    public const int CompressedSubheaderId = 4;

    /// <summary>
    /// The type identifier for compressed subheaders.
    /// </summary>
    /// <value>The integer value 1 indicating the compressed subheader type.</value>
    /// <remarks>
    /// This constant is used in the type field of page subheaders to classify the
    /// subheader as containing compressed data. It works in conjunction with
    /// CompressedSubheaderId to fully identify compressed metadata sections.
    /// 
    /// This type classification helps the parser determine the appropriate handling
    /// method for the subheader content during metadata extraction.
    /// </remarks>
    public const int CompressedSubheaderType = 1;
}