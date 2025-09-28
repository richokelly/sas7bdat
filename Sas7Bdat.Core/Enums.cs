namespace Sas7Bdat.Core
{
    /// <summary>
    /// Specifies the byte order (endianness) used for reading binary data.
    /// </summary>
    /// <remarks>
    /// Endianness determines the order in which bytes are arranged when storing multi-byte values.
    /// This is crucial for correctly interpreting binary data from different systems and file formats.
    /// </remarks>
    public enum Endian : byte
    {
        /// <summary>
        /// Little-endian byte order. The least significant byte is stored first.
        /// </summary>
        /// <remarks>
        /// Commonly used by Intel x86 and x64 processors and many modern systems.
        /// </remarks>
        Little = 1,

        /// <summary>
        /// Big-endian byte order. The most significant byte is stored first.
        /// </summary>
        /// <remarks>
        /// Commonly used by network protocols and some older systems like PowerPC and SPARC.
        /// Also known as "network byte order."
        /// </remarks>
        Big = 2
    }

    /// <summary>
    /// Specifies the data format architecture used in the SAS file.
    /// </summary>
    /// <remarks>
    /// The format determines the size of pointers, addresses, and certain data structures
    /// within the SAS file format.
    /// </remarks>
    public enum Format : byte
    {
        /// <summary>
        /// 32-bit format architecture.
        /// </summary>
        /// <remarks>
        /// Used for SAS files created on 32-bit systems or with 32-bit compatibility mode.
        /// Typically uses 4-byte pointers and addresses.
        /// </remarks>
        Bit32 = 1,

        /// <summary>
        /// 64-bit format architecture.
        /// </summary>
        /// <remarks>
        /// Used for SAS files created on 64-bit systems.
        /// Typically uses 8-byte pointers and addresses, allowing for larger file sizes.
        /// </remarks>
        Bit64 = 2
    }

    /// <summary>
    /// Identifies the operating system platform where the SAS file was created.
    /// </summary>
    /// <remarks>
    /// Platform information helps determine platform-specific behaviors and data formats
    /// that may affect how the file should be interpreted.
    /// </remarks>
    public enum Platform : byte
    {
        /// <summary>
        /// Unknown or unrecognized platform.
        /// </summary>
        /// <remarks>
        /// Used when the platform cannot be determined from the file metadata
        /// or when encountering unsupported platform identifiers.
        /// </remarks>
        Unknown = 0,

        /// <summary>
        /// Unix-based operating system platform.
        /// </summary>
        /// <remarks>
        /// Includes various Unix variants such as AIX, Solaris, Linux, and other POSIX-compliant systems.
        /// </remarks>
        Unix = 1,

        /// <summary>
        /// Microsoft Windows operating system platform.
        /// </summary>
        /// <remarks>
        /// Covers various versions of Windows where SAS files might be created.
        /// </remarks>
        Windows = 2
    }

    /// <summary>
    /// Specifies the compression algorithm used for data pages in the SAS file.
    /// </summary>
    /// <remarks>
    /// Compression reduces file size but requires decompression during reading.
    /// Different algorithms offer different trade-offs between compression ratio and speed.
    /// </remarks>
    public enum Compression : byte
    {
        /// <summary>
        /// No compression is applied to the data.
        /// </summary>
        /// <remarks>
        /// Fastest to read but results in larger file sizes.
        /// Data is stored in its original uncompressed format.
        /// </remarks>
        None = 0,

        /// <summary>
        /// Run-Length Encoding (RLE) compression.
        /// </summary>
        /// <remarks>
        /// Efficient for data with many repeated values or patterns.
        /// Works by encoding consecutive identical bytes as a count and value pair.
        /// </remarks>
        Rle = 1,

        /// <summary>
        /// RDC (Ross Data Compression) compression.
        /// </summary>
        /// <remarks>
        /// A proprietary compression algorithm used by SAS.
        /// Generally provides better compression ratios than RLE but may be slower to decompress.
        /// </remarks>
        Rdc = 2
    }

    /// <summary>
    /// Defines the logical data type of a column for display and interpretation purposes.
    /// </summary>
    /// <remarks>
    /// Column types help determine how data should be presented to users and what operations
    /// are valid. This is separate from the underlying storage format.
    /// </remarks>
    public enum ColumnType : byte
    {
        /// <summary>
        /// Unknown or unrecognized column type.
        /// </summary>
        /// <remarks>
        /// Used as a fallback when the column type cannot be determined from metadata.
        /// </remarks>
        Unknown = 0,

        /// <summary>
        /// Text or character string data.
        /// </summary>
        /// <remarks>
        /// Contains textual information that should be treated as strings.
        /// May include various character encodings.
        /// </remarks>
        String = 1,

        /// <summary>
        /// Numeric data (integers, floating-point numbers).
        /// </summary>
        /// <remarks>
        /// Contains numerical values that can be used in mathematical operations.
        /// Typically stored as double-precision floating-point numbers in SAS.
        /// </remarks>
        Number = 2,

        /// <summary>
        /// Date and time combined data.
        /// </summary>
        /// <remarks>
        /// Represents a specific point in time including both date and time components.
        /// Usually stored as a numeric value representing seconds since a SAS epoch.
        /// </remarks>
        DateTime = 3,

        /// <summary>
        /// Date-only data (without time component).
        /// </summary>
        /// <remarks>
        /// Represents a calendar date without time information.
        /// Stored as days since the SAS date epoch.
        /// </remarks>
        Date = 4,

        /// <summary>
        /// Time-only data (duration or time of day).
        /// </summary>
        /// <remarks>
        /// Represents either a duration or time of day without date information.
        /// Typically stored as seconds since midnight or as a duration in seconds.
        /// </remarks>
        Time = 5
    }

    /// <summary>
    /// Defines how data is physically stored in the file at the byte level.
    /// </summary>
    /// <remarks>
    /// Storage type determines the binary format and parsing method needed to read
    /// the raw data from the file before any type conversion or formatting is applied.
    /// </remarks>
    public enum StorageType : byte
    {
        /// <summary>
        /// Unknown or unrecognized storage format.
        /// </summary>
        /// <remarks>
        /// Used when the storage format cannot be determined from file metadata.
        /// </remarks>
        Unknown = 0,

        /// <summary>
        /// Data is stored as character/string bytes.
        /// </summary>
        /// <remarks>
        /// Raw data consists of character bytes that need to be decoded using
        /// the appropriate character encoding (e.g., ASCII, UTF-8, Windows-1252).
        /// </remarks>
        String = 1,

        /// <summary>
        /// Data is stored in binary numeric format.
        /// </summary>
        /// <remarks>
        /// Raw data consists of binary-encoded numbers, typically as IEEE 754
        /// double-precision floating-point values in SAS files.
        /// </remarks>
        Number = 2
    }
}