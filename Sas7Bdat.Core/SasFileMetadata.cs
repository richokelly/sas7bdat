using Sas7Bdat.Core.Decompression;

namespace Sas7Bdat.Core;

/// <summary>
/// Contains comprehensive metadata information about a SAS dataset file.
/// </summary>
/// <remarks>
/// This class encapsulates all the structural and descriptive information about a SAS7BDAT file,
/// including file format details, creation information, compression settings, and data organization.
/// The metadata is typically read from the file header during the file opening process and is
/// essential for correctly interpreting the file's binary structure and content.
/// </remarks>
public sealed class SasFileMetadata
{
    /// <summary>
    /// Gets or sets the length of each data row in bytes.
    /// </summary>
    /// <value>The fixed size of each row's data in bytes.</value>
    /// <remarks>
    /// SAS datasets use fixed-width rows where each column occupies a predetermined number of bytes.
    /// This value represents the total width of all columns combined and is essential for
    /// calculating column offsets and reading row data efficiently.
    /// </remarks>
    public int RowLength { get; set; }

    /// <summary>
    /// Gets or sets the first column count parameter from the file header.
    /// </summary>
    /// <value>An internal column count value used by the SAS file format.</value>
    /// <remarks>
    /// This is an internal SAS file format parameter that may differ from ColumnCount
    /// in certain file configurations. It's used internally for file structure validation
    /// and parsing but is generally not needed for normal data access operations.
    /// </remarks>
    public int ColCountP1 { get; set; }

    /// <summary>
    /// Gets or sets the second column count parameter from the file header.
    /// </summary>
    /// <value>An internal column count value used by the SAS file format.</value>
    /// <remarks>
    /// This is another internal SAS file format parameter related to column organization.
    /// Like ColCountP1, it's primarily used for internal file structure handling
    /// and validation purposes.
    /// </remarks>
    public int ColCountP2 { get; set; }

    /// <summary>
    /// Gets or sets the number of rows in mixed-type pages.
    /// </summary>
    /// <value>The count of rows stored in pages that contain mixed content types.</value>
    /// <remarks>
    /// Some SAS files use mixed pages that can contain both data rows and metadata.
    /// This value helps track how many actual data rows are stored in such pages,
    /// which is important for accurate row counting and data extraction.
    /// </remarks>
    public long MixPageRowCount { get; set; }

    /// <summary>
    /// Gets or sets the LCS (Length of Character Set) parameter.
    /// </summary>
    /// <value>A parameter related to character set handling in the file.</value>
    /// <remarks>
    /// This is an internal SAS parameter that affects how character data is stored
    /// and interpreted. It's related to the character encoding and may influence
    /// string parsing operations.
    /// </remarks>
    public int Lcs { get; set; }

    /// <summary>
    /// Gets or sets the LCP (Length of Character Page) parameter.
    /// </summary>
    /// <value>A parameter related to character page organization in the file.</value>
    /// <remarks>
    /// This internal parameter affects how character data is organized within pages
    /// and may influence the layout of string columns and text data storage.
    /// </remarks>
    public int Lcp { get; set; }

    /// <summary>
    /// Gets the appropriate decompressor for the file's compression type.
    /// </summary>
    /// <value>
    /// An IDecompressor instance that can decompress data pages according to the file's compression setting.
    /// </value>
    /// <remarks>
    /// This property returns the correct decompressor implementation based on the Compression property:
    /// <list type="bullet">
    /// <item><description>Compression.Rle → RleDecompressor for Run-Length Encoding</description></item>
    /// <item><description>Compression.Rdc → RdcDecompressor for Ross Data Compression</description></item>
    /// <item><description>Compression.None → NoDecompressor (pass-through)</description></item>
    /// </list>
    /// 
    /// The decompressor is used internally during data reading to handle compressed pages transparently.
    /// Users of the library typically don't need to interact with decompressors directly.
    /// </remarks>
    /// <example>
    /// <code>
    /// var decompressor = metadata.Decompressor;
    /// var decompressedData = decompressor.Decompress(compressedPageData);
    /// </code>
    /// </example>
    public IDecompressor Decompressor
    {
        get
        {
            return Compression switch
            {
                Compression.Rle => new RleDecompressor(),
                Compression.Rdc => new RdcDecompressor(),
                _ => new NoDecompressor()
            };
        }
    }

    /// <summary>
    /// Gets or sets the byte order (endianness) used in the file.
    /// </summary>
    /// <value>An Endian value indicating whether the file uses big-endian or little-endian byte ordering.</value>
    /// <remarks>
    /// The endianness affects how multibyte values (integers, floating-point numbers) are stored
    /// and must be known to correctly interpret binary data in the file.
    /// </remarks>
    public Endian Endianness { get; set; }

    /// <summary>
    /// Gets or sets the architecture format of the file (32-bit or 64-bit).
    /// </summary>
    /// <value>A Format value indicating the architecture used when the file was created.</value>
    /// <remarks>
    /// The format affects the size of pointers, page headers, and certain data structures within the file.
    /// 64-bit files can handle larger datasets and have different internal layouts than 32-bit files.
    /// </remarks>
    public Format Format { get; set; }

    /// <summary>
    /// Gets or sets the operating system platform where the file was created.
    /// </summary>
    /// <value>A Platform value indicating the source operating system.</value>
    /// <remarks>
    /// Platform information can affect character encoding defaults, file path conventions,
    /// and other platform-specific behaviors that may influence data interpretation.
    /// </remarks>
    public Platform Platform { get; set; }

    /// <summary>
    /// Gets or sets the character encoding used for text data in the file.
    /// </summary>
    /// <value>The name of the character encoding. Defaults to "WINDOWS-1252".</value>
    /// <remarks>
    /// This encoding is used to convert byte sequences to strings when reading text columns.
    /// Common values include "WINDOWS-1252", "UTF-8", "ISO-8859-1", and various locale-specific encodings.
    /// The encoding must be correctly identified to properly decode text data.
    /// </remarks>
    public string Encoding { get; set; } = "WINDOWS-1252";

    /// <summary>
    /// Gets or sets the name of the dataset as stored in the SAS file.
    /// </summary>
    /// <value>The dataset name, or an empty string if not specified.</value>
    /// <remarks>
    /// This corresponds to the SAS dataset name and may be used for identification purposes.
    /// It's separate from the physical filename and represents the logical dataset name within SAS.
    /// </remarks>
    public string DatasetName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file type identifier.
    /// </summary>
    /// <value>A string identifying the specific type of SAS file.</value>
    /// <remarks>
    /// For SAS7BDAT files, this typically contains version and format information
    /// that helps identify the specific variant of the SAS file format in use.
    /// </remarks>
    public string FileType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date and time when the file was originally created.
    /// </summary>
    /// <value>The creation timestamp of the file.</value>
    /// <remarks>
    /// This timestamp is embedded in the file metadata and represents when the SAS dataset
    /// was first created, not the filesystem creation time.
    /// </remarks>
    public DateTime DateCreated { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the file was last modified.
    /// </summary>
    /// <value>The last modification timestamp of the file.</value>
    /// <remarks>
    /// This timestamp reflects the last time the dataset content was modified within SAS,
    /// which may differ from the filesystem modification time.
    /// </remarks>
    public DateTime DateModified { get; set; }

    /// <summary>
    /// Gets or sets the length of the file header in bytes.
    /// </summary>
    /// <value>The size of the header section in bytes.</value>
    /// <remarks>
    /// The header contains all metadata and column definitions. Data pages begin immediately
    /// after the header section. This value is essential for positioning file reads correctly.
    /// </remarks>
    public int HeaderLength { get; set; }

    /// <summary>
    /// Gets or sets the length of each data page in bytes.
    /// </summary>
    /// <value>The fixed size of each data page in the file.</value>
    /// <remarks>
    /// SAS files organize data into fixed-size pages. All pages have the same length,
    /// which is specified in the file header. This value is crucial for navigation
    /// and memory allocation during reading operations.
    /// </remarks>
    public int PageLength { get; set; }

    /// <summary>
    /// Gets or sets the total number of data pages in the file.
    /// </summary>
    /// <value>The count of data pages following the header.</value>
    /// <remarks>
    /// This count includes all pages in the file, regardless of their type (data pages, metadata pages, etc.).
    /// It can be used to estimate file size and validate file completeness.
    /// </remarks>
    public int PageCount { get; set; }

    /// <summary>
    /// Gets or sets the SAS software release version that created the file.
    /// </summary>
    /// <value>A string representing the SAS version information.</value>
    /// <remarks>
    /// This information can be useful for understanding feature compatibility and
    /// identifying any version-specific behaviors in the file format.
    /// </remarks>
    public string SasRelease { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of SAS server that created the file.
    /// </summary>
    /// <value>A string identifying the SAS server type.</value>
    /// <remarks>
    /// Different SAS server types (Base SAS, SAS/CONNECT, etc.) may create files
    /// with slightly different characteristics or capabilities.
    /// </remarks>
    public string SasServerType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the operating system type where the file was created.
    /// </summary>
    /// <value>A string describing the OS type (e.g., "WIN", "LIN", "UNIX").</value>
    /// <remarks>
    /// This provides more detailed platform information than the Platform property
    /// and may include specific OS variant details.
    /// </remarks>
    public string OsType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the operating system name where the file was created.
    /// </summary>
    /// <value>A string containing the specific OS name and version.</value>
    /// <remarks>
    /// This typically contains detailed version information about the operating system,
    /// such as "Windows 10" or "Red Hat Enterprise Linux 8".
    /// </remarks>
    public string OsName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the compression algorithm used for data pages.
    /// </summary>
    /// <value>A Compression value indicating the compression method in use.</value>
    /// <remarks>
    /// When compression is enabled, data pages are compressed to reduce file size.
    /// The compression type determines which decompression algorithm must be used
    /// when reading the file.
    /// </remarks>
    public Compression Compression { get; set; }

    /// <summary>
    /// Gets or sets the name of the user or process that created the file.
    /// </summary>
    /// <value>A string identifying the file creator.</value>
    /// <remarks>
    /// This typically contains the username or process name that was responsible
    /// for creating the SAS dataset.
    /// </remarks>
    public string Creator { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the SAS procedure that created the file.
    /// </summary>
    /// <value>A string identifying the creating SAS procedure.</value>
    /// <remarks>
    /// When a SAS dataset is created by a specific procedure (PROC SORT, PROC SQL, etc.),
    /// this field may contain the procedure name for audit and debugging purposes.
    /// </remarks>
    public string CreatorProc { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of data rows in the dataset.
    /// </summary>
    /// <value>The total count of observation rows in the dataset.</value>
    /// <remarks>
    /// This is the logical row count and represents the number of complete observations
    /// in the dataset. It's used for progress tracking and validation during reading operations.
    /// </remarks>
    public long RowCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of columns in the dataset.
    /// </summary>
    /// <value>The count of variables/columns defined in the dataset.</value>
    /// <remarks>
    /// This represents the total number of variables in the dataset schema.
    /// Each column has associated metadata including name, type, and storage information.
    /// </remarks>
    public int ColumnCount { get; set; }

}
