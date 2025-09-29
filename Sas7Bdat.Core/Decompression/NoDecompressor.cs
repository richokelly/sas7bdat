namespace Sas7Bdat.Core.Decompression;

/// <summary>
/// A pass-through decompressor that handles uncompressed SAS data by directly copying input to output.
/// </summary>
/// <remarks>
/// This decompressor is used when SAS files specify no compression (Compression.None).
/// It provides a consistent interface for handling both compressed and uncompressed data
/// through the same decompression pipeline.
/// 
/// The implementation is extremely efficient as it performs a direct memory copy operation
/// with no processing overhead. This is the fastest decompression option and is commonly
/// used for files where compression would provide minimal benefit or when read performance
/// is prioritized over storage space.
/// 
/// This class is thread-safe and stateless, making it suitable for concurrent use across
/// multiple threads without synchronization.
/// </remarks>
/// <example>
/// <code>
/// var decompressor = new NoDecompressor();
/// var sourceData = new byte[] { 1, 2, 3, 4, 5 };
/// var destination = new byte[sourceData.Length];
/// 
/// decompressor.Decompress(sourceData, destination);
/// // destination now contains exact copy of sourceData
/// </code>
/// </example>
public class NoDecompressor : IDecompressor
{
    /// <summary>
    /// Copies the compressed data directly to the destination buffer without any processing.
    /// </summary>
    /// <param name="compressed">The source data to copy (though not actually compressed).</param>
    /// <param name="destination">The destination buffer where data will be copied.</param>
    /// <remarks>
    /// This method performs a direct memory copy from source to destination. The term "compressed"
    /// is used for interface consistency, but the data is actually uncompressed.
    /// 
    /// The destination buffer must be at least as large as the source data. If the destination
    /// is larger, only the amount of data from the source will be copied, leaving the remaining
    /// destination bytes unchanged.
    /// 
    /// Performance characteristics:
    /// <list type="bullet">
    /// <item><description>O(n) time complexity where n is the data size</description></item>
    /// <item><description>Uses optimized memory copy operations</description></item>
    /// <item><description>No additional memory allocation</description></item>
    /// <item><description>Minimal CPU overhead</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="InvalidDataException">
    /// Thrown when the destination buffer is smaller than the source data.
    /// </exception>
    /// <example>
    /// <code>
    /// var noDecompressor = new NoDecompressor();
    /// var pageData = ReadUncompressedPageFromFile();
    /// var outputBuffer = new byte[pageData.Length];
    /// 
    /// noDecompressor.Decompress(pageData, outputBuffer);
    /// // outputBuffer contains the exact same data as pageData
    /// </code>
    /// </example>
    public void Decompress(ReadOnlySpan<byte> compressed, Span<byte> destination)
    {
        if (destination.Length < compressed.Length) throw new InvalidDataException("Destination buffer is smaller than the source data");
        compressed.CopyTo(destination);
    }
}