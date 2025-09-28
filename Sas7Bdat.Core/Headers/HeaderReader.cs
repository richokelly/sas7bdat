using Sas7Bdat.Core.Serializers;

namespace Sas7Bdat.Core.Headers;

/// <summary>
/// Reads and parses SAS7BDAT file headers to extract metadata and column information.
/// </summary>
/// <param name="filePath">The path to the SAS file to read.</param>
/// <remarks>
/// The HeaderReader is responsible for the initial parsing of SAS7BDAT files, extracting
/// critical file format information from the file header including endianness, format version,
/// platform information, and basic file metadata.
/// 
/// The reading process occurs in two phases:
/// <list type="number">
/// <item><description>Initial header validation and format detection using the first 288 bytes</description></item>
/// <item><description>Full header processing if the header extends beyond the initial block</description></item>
/// </list>
/// 
/// The class maintains internal alignment variables (_align1, _align2, _totalAlign) that are
/// calculated based on the file format version and are essential for correctly positioning
/// reads within the header structure.
/// 
/// After header processing, control is transferred to a MetadataReader for detailed column
/// and structural metadata extraction from the data pages.
/// </remarks>
/// <example>
/// <code>
/// var headerReader = new HeaderReader("/path/to/file.sas7bdat");
/// var (metadata, columns) = await headerReader.ReadMetadataAsync(cancellationToken);
/// 
/// Console.WriteLine($"File contains {metadata.RowCount} rows and {columns.Length} columns");
/// Console.WriteLine($"Endianness: {metadata.Endianness}, Format: {metadata.Format}");
/// </code>
/// </example>
internal sealed class HeaderReader(string filePath)
{
    /// <summary>
    /// Primary alignment offset used for positioning reads within the header structure.
    /// </summary>
    /// <remarks>
    /// This value is calculated based on file format characteristics and affects the
    /// positioning of various metadata fields within the header. It's set to 4 for
    /// certain format configurations, 0 otherwise.
    /// </remarks>
    private int _align1;

    /// <summary>
    /// Secondary alignment offset used for 64-bit format files.
    /// </summary>
    /// <remarks>
    /// This value is set to 4 for 64-bit format files and 0 for 32-bit files.
    /// It affects the positioning of metadata fields that are format-dependent.
    /// </remarks>
    private int _align2;

    /// <summary>
    /// Combined total alignment offset calculated as the sum of _align1 and _align2.
    /// </summary>
    /// <remarks>
    /// This value is used for positioning reads of metadata fields that are affected
    /// by both primary and secondary alignment considerations.
    /// </remarks>
    private int _totalAlign;

    /// <summary>
    /// Asynchronously reads and parses the complete metadata from a SAS7BDAT file.
    /// </summary>
    /// <param name="ct">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// a tuple with the parsed file metadata and an array of column information.
    /// </returns>
    /// <remarks>
    /// This method orchestrates the complete header reading process:
    /// 
    /// 1. **Initial Validation**: Reads the first 288 bytes and validates the magic number
    /// 2. **Format Detection**: Determines endianness, format (32/64-bit), and calculates alignment offsets
    /// 3. **Basic Metadata Extraction**: Extracts platform and encoding information
    /// 4. **Extended Header Processing**: Reads additional header data if the header is larger than 288 bytes
    /// 5. **Column Metadata**: Delegates to MetadataReader for detailed column information
    /// 
    /// The method uses optimized I/O with a large buffer size (8 * SystemPageSize) for efficient
    /// reading of header data. Memory management is handled through PooledMemory to reduce
    /// garbage collection pressure.
    /// 
    /// Error handling includes validation of:
    /// <list type="bullet">
    /// <item><description>File size sufficiency for header content</description></item>
    /// <item><description>Magic number verification for file format validation</description></item>
    /// <item><description>Header completeness for extended headers</description></item>
    /// <item><description>Date/time field validity</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="InvalidDataException">
    /// Thrown when:
    /// <list type="bullet">
    /// <item><description>The file is too small to contain a valid SAS header</description></item>
    /// <item><description>The magic number doesn't match expected SAS7BDAT format</description></item>
    /// <item><description>The header is incomplete or truncated</description></item>
    /// <item><description>Date/time fields contain invalid values</description></item>
    /// </list>
    /// </exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified file doesn't exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when file access is denied.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    /// <example>
    /// <code>
    /// var reader = new HeaderReader("dataset.sas7bdat");
    /// 
    /// try
    /// {
    ///     var (metadata, columns) = await reader.ReadMetadataAsync(cancellationToken);
    ///     
    ///     Console.WriteLine($"Dataset: {metadata.DatasetName}");
    ///     Console.WriteLine($"Created: {metadata.DateCreated}");
    ///     Console.WriteLine($"Columns: {string.Join(", ", columns.Select(c => c.Name))}");
    /// }
    /// catch (InvalidDataException ex)
    /// {
    ///     Console.WriteLine($"Invalid SAS file: {ex.Message}");
    /// }
    /// </code>
    /// </example>
    public async Task<(SasFileMetadata metadata, SasColumnInfo[] columns)> ReadMetadataAsync(CancellationToken ct = default)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: Math.Max(Environment.SystemPageSize, SasConstants.HeaderSize),
            useAsync: true);

        using var initialBuffer = new PooledMemory<byte>(SasConstants.HeaderSize);
        var bytesRead = await stream.ReadAsync(initialBuffer.Memory, ct);
        if (bytesRead < SasConstants.HeaderSize)
            throw new InvalidDataException($"File is too small to be a valid SAS7BDAT file. {filePath} may be corrupted.");

        ValidateMagicNumber(initialBuffer.Span);

        var (endian, format) = DetermineFormatAndEndianness(initialBuffer.Span);

        var metadata = new SasFileMetadata
        {
            Endianness = endian,
            Format = format
        };

        ExtractInitialProperties(initialBuffer.Span[..SasConstants.HeaderSize], metadata);

        metadata.HeaderLength = (int)endian.ReadUInt32At(initialBuffer.Span[..SasConstants.HeaderSize], 196 + _align1);

        if (metadata.HeaderLength > SasConstants.HeaderSize)
        {
            using var fullBuffer = new PooledMemory<byte>(metadata.HeaderLength);
            initialBuffer.Span.CopyTo(fullBuffer.Span);

            var remainingBytes = metadata.HeaderLength - SasConstants.HeaderSize;
            bytesRead = await stream.ReadAsync(fullBuffer.Memory.Slice(SasConstants.HeaderSize, remainingBytes), ct);

            if (bytesRead < remainingBytes)
                throw new InvalidDataException($"Incomplete header. {filePath} may be corrupted.");

            ExtractFullProperties(fullBuffer.Span[..metadata.HeaderLength], metadata);

        }
        else
        {
            ExtractFullProperties(initialBuffer.Span[..SasConstants.HeaderSize], metadata);
        }

        var metadataReader = new MetadataReader(stream, metadata);
        var columns = await metadataReader.ReadMetadataAsync(ct);
        return (metadata, columns.ToArray());

    }

    /// <summary>
    /// Validates that the file begins with the correct SAS7BDAT magic number.
    /// </summary>
    /// <param name="buffer">The buffer containing the file header data.</param>
    /// <remarks>
    /// The magic number is a 32-byte sequence at the beginning of every valid SAS7BDAT file
    /// that serves as a file format identifier. This validation ensures that the file
    /// is actually a SAS7BDAT file and not some other file type.
    /// 
    /// The magic number validation is critical for preventing attempts to parse non-SAS
    /// files which could lead to unpredictable behavior or incorrect data interpretation.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    /// Thrown when the magic number doesn't match the expected SAS7BDAT format signature.
    /// </exception>
    private void ValidateMagicNumber(ReadOnlySpan<byte> buffer)
    {
        var magicSpan = buffer[..SasConstants.MagicNumber.Length];
        if (!magicSpan.SequenceEqual(SasConstants.MagicNumber))
            throw new InvalidDataException($"Invalid SAS7BDAT magic number. {filePath} may be corrupted.");
    }

    /// <summary>
    /// Determines the file format (32-bit vs 64-bit) and endianness from header bytes.
    /// </summary>
    /// <param name="buffer">The buffer containing the file header data.</param>
    /// <returns>A tuple containing the determined endianness and format.</returns>
    /// <remarks>
    /// This method examines specific bytes in the header to determine:
    /// <list type="bullet">
    /// <item><description>Format: Whether the file uses 32-bit or 64-bit architecture (byte 32)</description></item>
    /// <item><description>Endianness: Whether multi-byte values use big-endian or little-endian byte order (byte 37)</description></item>
    /// <item><description>Alignment: Calculates alignment offsets based on format characteristics (bytes 32, 35)</description></item>
    /// </list>
    /// 
    /// The alignment values (_align1, _align2, _totalAlign) are calculated here and used
    /// throughout the header parsing process to correctly position reads of metadata fields.
    /// These alignments account for differences in data structure layouts between different
    /// SAS file format versions.
    /// 
    /// Format determination:
    /// <list type="bullet">
    /// <item><description>64-bit format: byte 32 == '3', sets _align2 = 4</description></item>
    /// <item><description>32-bit format: byte 32 != '3', _align2 remains 0</description></item>
    /// </list>
    /// 
    /// Endianness determination:
    /// <list type="bullet">
    /// <item><description>Little-endian: byte 37 == 0x01</description></item>
    /// <item><description>Big-endian: byte 37 != 0x01</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var (endian, format) = DetermineFormatAndEndianness(headerBuffer);
    /// Console.WriteLine($"Format: {format}, Endianness: {endian}");
    /// // Output might be: "Format: Bit64, Endianness: Little"
    /// </code>
    /// </example>
    private (Endian endian, Format format) DetermineFormatAndEndianness(ReadOnlySpan<byte> buffer)
    {
        var format = buffer[32] == '3' ? Format.Bit64 : Format.Bit32;

        if (format == Format.Bit64)
            _align2 = 4;

        if (buffer[35] == '3')
            _align1 = 4;

        _totalAlign = _align1 + _align2;

        var endian = buffer[37] == 0x01 ? Endian.Little : Endian.Big;

        return (endian, format);
    }

    /// <summary>
    /// Extracts basic properties from the initial header portion.
    /// </summary>
    /// <param name="buffer">The buffer containing the initial header data.</param>
    /// <param name="metadata">The metadata object to populate with extracted properties.</param>
    /// <remarks>
    /// This method extracts fundamental file properties that are available in the first
    /// 288 bytes of the header:
    /// 
    /// <list type="bullet">
    /// <item><description>Platform information (byte 39): Unix, Windows, or Unknown</description></item>
    /// <item><description>Character encoding (byte 70): Used for text data interpretation</description></item>
    /// </list>
    /// 
    /// Platform mapping:
    /// <list type="bullet">
    /// <item><description>'1' (0x31) → Platform.Unix</description></item>
    /// <item><description>'2' (0x32) → Platform.Windows</description></item>
    /// <item><description>Other values → Platform.Unknown</description></item>
    /// </list>
    /// 
    /// The encoding byte is mapped through SasEncoding.GetEncodingName() to determine
    /// the character encoding used for text fields throughout the file.
    /// </remarks>
    private static void ExtractInitialProperties(ReadOnlySpan<byte> buffer, SasFileMetadata metadata)
    {
        metadata.Platform = metadata.Endianness.ReadByteAt(buffer, 39) switch
        {
            (byte)'1' => Platform.Unix,
            (byte)'2' => Platform.Windows,
            _ => Platform.Unknown
        };

        var encodingByte = metadata.Endianness.ReadByteAt(buffer, 70);
        metadata.Encoding = SasEncoding.GetEncodingName(encodingByte);
    }

    /// <summary>
    /// Extracts comprehensive properties from the complete header.
    /// </summary>
    /// <param name="buffer">The buffer containing the complete header data.</param>
    /// <param name="metadata">The metadata object to populate with extracted properties.</param>
    /// <remarks>
    /// This method extracts detailed metadata from the complete header, including:
    /// 
    /// **File Identification:**
    /// <list type="bullet">
    /// <item><description>Dataset name (offset 92, 64 bytes): The logical name of the SAS dataset</description></item>
    /// <item><description>File type (offset 156, 8 bytes): SAS file type identifier</description></item>
    /// </list>
    /// 
    /// **Temporal Information:**
    /// <list type="bullet">
    /// <item><description>Creation date (offset 164 + _align1): When the dataset was created</description></item>
    /// <item><description>Modification date (offset 172 + _align1): When the dataset was last modified</description></item>
    /// </list>
    /// 
    /// **Structure Information:**
    /// <list type="bullet">
    /// <item><description>Page length (offset 200 + _align1): Size of each data page in bytes</description></item>
    /// <item><description>Page count (offset 204 + _align1): Total number of pages in the file</description></item>
    /// </list>
    /// 
    /// **System Information:**
    /// <list type="bullet">
    /// <item><description>SAS release version (offset 216 + _totalAlign): Version of SAS that created the file</description></item>
    /// <item><description>SAS server type (offset 224 + _totalAlign): Type of SAS server used</description></item>
    /// <item><description>OS type (offset 240 + _totalAlign): Operating system type</description></item>
    /// <item><description>OS name (offset 256/272 + _totalAlign): Specific operating system name</description></item>
    /// </list>
    /// 
    /// All text fields are trimmed of whitespace and decoded using the encoding specified
    /// in the file metadata. Date/time values are converted from SAS datetime format
    /// (seconds since January 1, 1960) to .NET DateTime objects.
    /// 
    /// The OS name extraction includes logic to handle different placement of this field
    /// depending on whether certain header bytes are null.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    /// Thrown when date/time values cannot be converted from SAS format, indicating
    /// corrupted header data.
    /// </exception>
    private void ExtractFullProperties(ReadOnlySpan<byte> buffer, SasFileMetadata metadata)
    {
        var encoding = SasEncoding.GetEncodingByName(metadata.Encoding);
        var endian = metadata.Endianness;

        metadata.DatasetName = buffer.ReadStringAt(92, 64, encoding).Trim();
        metadata.FileType = buffer.ReadStringAt(156, 8, encoding).Trim();

        metadata.DateCreated = FieldSerializers.ConvertSasDateTimeSeconds(endian.ReadDoubleAt(buffer, 164 + _align1)) as DateTime? ?? throw new InvalidDataException($"Invalid header. {filePath} may be corrupted.");
        metadata.DateModified = FieldSerializers.ConvertSasDateTimeSeconds(endian.ReadDoubleAt(buffer, 172 + _align1)) as DateTime? ?? throw new InvalidDataException($"Invalid header. {filePath} may be corrupted.");

        metadata.PageLength = (int)endian.ReadUInt32At(buffer, 200 + _align1);
        metadata.PageCount = (int)endian.ReadUInt32At(buffer, 204 + _align1);

        metadata.SasRelease = buffer.ReadStringAt(216 + _totalAlign, 8, encoding).Trim();
        metadata.SasServerType = buffer.ReadStringAt(224 + _totalAlign, 16, encoding).Trim();
        metadata.OsType = buffer.ReadStringAt(240 + _totalAlign, 16, encoding).Trim();

        if (endian.ReadByteAt(buffer, 272 + _totalAlign) != 0)
        {
            metadata.OsName = buffer.ReadStringAt(272 + _totalAlign, 16, encoding).Trim();
        }
        else
        {
            metadata.OsName = buffer.ReadStringAt(256 + _totalAlign, 16, encoding).Trim();
        }
    }

    /// <summary>
    /// Contains constants used during SAS7BDAT header processing.
    /// </summary>
    /// <remarks>
    /// This nested class provides essential constants for header validation and processing:
    /// <list type="bullet">
    /// <item><description>HeaderSize: The standard size of the initial header block</description></item>
    /// <item><description>MagicNumber: The byte sequence that identifies valid SAS7BDAT files</description></item>
    /// </list>
    /// 
    /// These constants are fundamental to the file format specification and should not
    /// be modified as they correspond to the official SAS7BDAT file format structure.
    /// </remarks>
    internal sealed class SasConstants
    {
        /// <summary>
        /// The standard size in bytes of the initial SAS7BDAT file header.
        /// </summary>
        /// <value>288 bytes representing the minimum header size for all SAS7BDAT files.</value>
        /// <remarks>
        /// This represents the minimum header size that all SAS7BDAT files contain.
        /// Some files may have extended headers that are longer than this value,
        /// but the first 288 bytes always contain the core format information
        /// needed to determine how to read the rest of the file.
        /// </remarks>
        public const int HeaderSize = 288;

        /// <summary>
        /// The magic number byte sequence that identifies valid SAS7BDAT files.
        /// </summary>
        /// <value>A 32-byte array containing the SAS7BDAT file format signature.</value>
        /// <remarks>
        /// This 32-byte sequence appears at the beginning of every valid SAS7BDAT file
        /// and serves as a definitive format identifier. The sequence includes both
        /// null bytes and specific non-null bytes that form a unique signature.
        /// 
        /// Files that don't begin with this exact sequence are not valid SAS7BDAT
        /// files and should be rejected during header validation.
        /// 
        /// The magic number helps prevent attempts to parse incompatible file formats
        /// and provides early detection of file corruption.
        /// </remarks>
        public static readonly byte[] MagicNumber =
        [
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0xc2,
            0xea,
            0x81,
            0x60,
            0xb3,
            0x14,
            0x11,
            0xcf,
            0xbd,
            0x92,
            0x08,
            0x00,
            0x09,
            0xc7,
            0x31,
            0x8c,
            0x18,
            0x1f,
            0x10,
            0x11
        ];
    }
}