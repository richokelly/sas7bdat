namespace Sas7Bdat.Core.Decompression;

/// <summary>
/// Defines the contract for decompressing compressed data from SAS7BDAT files.
/// </summary>
/// <remarks>
/// This interface provides a standardized way to handle different compression algorithms
/// used in SAS files. Implementations should be thread-safe and stateless to allow
/// reuse across multiple decompression operations.
/// 
/// SAS files may use various compression algorithms including:
/// <list type="bullet">
/// <item><description>No compression (pass-through)</description></item>
/// <item><description>RLE (Run-Length Encoding)</description></item>
/// <item><description>RDC (Ross Data Compression)</description></item>
/// </list>
/// 
/// Each implementation handles the specific bit patterns and command structures
/// for its respective compression format.
/// </remarks>
public interface IDecompressor
{
    /// <summary>
    /// Decompresses compressed data into the specified destination buffer.
    /// </summary>
    /// <param name="compressed">The compressed data to decompress.</param>
    /// <param name="destination">The buffer where decompressed data will be written.</param>
    /// <remarks>
    /// This method decompresses the entire compressed span into the destination buffer.
    /// The destination buffer must be large enough to hold the decompressed data.
    /// 
    /// Key behaviors:
    /// <list type="bullet">
    /// <item><description>The method should handle partial data gracefully</description></item>
    /// <item><description>If the destination is larger than needed, remaining bytes may be filled with nulls</description></item>
    /// <item><description>The implementation should not write beyond the destination buffer bounds</description></item>
    /// <item><description>Corrupted or invalid compressed data should result in an exception</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the compressed data is corrupted, contains invalid command sequences,
    /// or cannot be decompressed due to format errors.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the destination buffer is too small to hold the decompressed data.
    /// </exception>
    /// <example>
    /// <code>
    /// IDecompressor decompressor = new RleDecompressor();
    /// var compressed = GetCompressedData();
    /// var destination = new byte[expectedSize];
    /// 
    /// decompressor.Decompress(compressed, destination);
    /// // destination now contains the decompressed data
    /// </code>
    /// </example>
    void Decompress(ReadOnlySpan<byte> compressed, Span<byte> destination);
}