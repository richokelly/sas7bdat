using System.Runtime.CompilerServices;
using Sas7Bdat.Core.Headers;
using Sas7Bdat.Core.Serializers;
using Sas7Bdat.Core.Pages;

namespace Sas7Bdat.Core;

/// <summary>
/// Provides high-performance, asynchronous reading capabilities for SAS7BDAT files.
/// </summary>
/// <remarks>
/// SasDataReader is the primary entry point for reading SAS dataset files (.sas7bdat).
/// It provides efficient streaming access to large datasets with support for:
/// <list type="bullet">
/// <item><description>Asynchronous I/O operations for better scalability</description></item>
/// <item><description>Memory-efficient streaming through large files</description></item>
/// <item><description>Column subset selection to reduce memory usage</description></item>
/// <item><description>Row filtering and pagination support</description></item>
/// <item><description>Automatic decompression of compressed SAS files</description></item>
/// <item><description>Custom buffer sizing for performance tuning</description></item>
/// </list>
/// 
/// The reader maintains a file lock during its lifetime to ensure data consistency
/// and should be properly disposed after use.
/// </remarks>
/// <example>
/// <code>
/// // Basic usage
/// await using var reader = await SasDataReader.OpenFileAsync("data.sas7bdat");
/// 
/// await foreach (var row in reader.ReadRowsAsync())
/// {
///     // Process each row as ReadOnlyMemory&lt;object?&gt;
///     Console.WriteLine($"First column: {row.Span[0]}");
/// }
/// 
/// // With options
/// var options = new RecordReadOptions
/// {
///     SelectedColumns = new HashSet&lt;string&gt; { "Name", "Age" },
///     MaxRows = 1000
/// };
/// 
/// await foreach (var row in reader.ReadRowsAsync(options))
/// {
///     // Process filtered rows
/// }
/// </code>
/// </example>
public sealed class SasDataReader : IAsyncDisposable
{
    /// <summary>
    /// The path to the SAS file being read.
    /// </summary>
    private readonly string _filePath;

    /// <summary>
    /// File stream used to maintain an exclusive lock on the file during reading.
    /// </summary>
    private readonly FileStream _fileLock;

    /// <summary>
    /// Gets the metadata information for the SAS file.
    /// </summary>
    /// <value>
    /// A SasFileMetadata object containing file-level information such as creation date,
    /// encoding, compression type, row count, and other structural details.
    /// </value>
    /// <remarks>
    /// This metadata is read from the file header during initialization and provides
    /// essential information about the file format and contents.
    /// </remarks>
    public SasFileMetadata Metadata { get; }

    /// <summary>
    /// Gets the column definitions for the SAS dataset.
    /// </summary>
    /// <value>
    /// An array of SasColumnInfo structures describing each column in the dataset,
    /// including names, types, formats, and storage information.
    /// </value>
    /// <remarks>
    /// The columns are ordered according to their position in the original SAS dataset.
    /// This information is used for column selection, type conversion, and data serialization.
    /// </remarks>
    public SasColumnInfo[] Columns { get; }

    /// <summary>
    /// Initializes a new instance of the SasDataReader class.
    /// </summary>
    /// <param name="filePath">The path to the SAS file.</param>
    /// <param name="metadata">The parsed metadata from the file header.</param>
    /// <param name="columns">The column definitions from the file header.</param>
    /// <remarks>
    /// This constructor is private and should not be called directly. Use the
    /// OpenFileAsync method instead to properly initialize a SasDataReader instance.
    /// 
    /// A file lock is acquired during construction to ensure exclusive access
    /// and prevent file modifications during reading operations.
    /// </remarks>
    private SasDataReader(string filePath, SasFileMetadata metadata, SasColumnInfo[] columns)
    {
        _filePath = filePath;
        Metadata = metadata;
        Columns = columns;
        _fileLock = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 0);
    }

    /// <summary>
    /// Asynchronously opens a SAS file and creates a new SasDataReader instance.
    /// </summary>
    /// <param name="filePath">The path to the SAS7BDAT file to open.</param>
    /// <param name="ct">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// a new SasDataReader instance ready for reading data.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when filePath is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to the file is denied.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is not a valid SAS7BDAT file.</exception>
    /// <exception cref="NotSupportedException">Thrown when the file format version is not supported.</exception>
    /// <remarks>
    /// This method reads and parses the file header to extract metadata and column information.
    /// The resulting SasDataReader maintains a lock on the file until disposed.
    /// 
    /// The operation is cancellable and will respect the provided cancellation token.
    /// </remarks>
    /// <example>
    /// <code>
    /// try
    /// {
    ///     await using var reader = await SasDataReader.OpenFileAsync("data.sas7bdat", cancellationToken);
    ///     Console.WriteLine($"Dataset contains {reader.Metadata.RowCount} rows and {reader.Columns.Length} columns");
    /// }
    /// catch (FileNotFoundException)
    /// {
    ///     Console.WriteLine("SAS file not found");
    /// }
    /// catch (InvalidDataException ex)
    /// {
    ///     Console.WriteLine($"Invalid SAS file: {ex.Message}");
    /// }
    /// </code>
    /// </example>
    public static async Task<SasDataReader> OpenFileAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filePath, nameof(filePath));

        if (!File.Exists(filePath)) throw new FileNotFoundException($"File not found: {filePath}", filePath);

        var headerReader = new HeaderReader(filePath);
        var (metadata, columns) = await headerReader.ReadMetadataAsync(ct);

        return new SasDataReader(filePath, metadata, columns);
    }

    /// <summary>
    /// Asynchronously reads rows from the SAS dataset as arrays of objects.
    /// </summary>
    /// <param name="options">Options to control the reading behavior, or null for default options.</param>
    /// <param name="ct">A cancellation token to cancel the enumeration.</param>
    /// <returns>
    /// An asynchronous enumerable that yields ReadOnlyMemory&lt;object?&gt; instances,
    /// where each instance represents one row of data from the dataset.
    /// </returns>
    /// <remarks>
    /// This method provides efficient streaming access to dataset rows without loading
    /// the entire dataset into memory. Each yielded row contains values converted to
    /// appropriate .NET types based on the column definitions.
    /// 
    /// The method supports:
    /// <list type="bullet">
    /// <item><description>Column filtering through options.SelectedColumns or options.SelectedColumnIndices</description></item>
    /// <item><description>Row skipping and limiting through options.SkipRows and options.MaxRows</description></item>
    /// <item><description>Custom file buffer sizing through options.FileBufferSize</description></item>
    /// <item><description>Automatic decompression based on file metadata</description></item>
    /// </list>
    /// 
    /// Each row's memory is valid only until the next iteration. If you need to store
    /// row data beyond the current iteration, copy the values to a different data structure.
    /// 
    /// The enumeration can be cancelled at any time using the provided cancellation token.
    /// </remarks>
    /// <exception cref="InvalidDataException">Thrown when corrupted data is encountered during reading.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    /// <example>
    /// <code>
    /// var options = new RecordReadOptions
    /// {
    ///     SelectedColumns = new HashSet&lt;string&gt; { "Name", "Age", "Salary" },
    ///     SkipRows = 100,
    ///     MaxRows = 50
    /// };
    /// 
    /// await foreach (var row in reader.ReadRowsAsync(options, cancellationToken))
    /// {
    ///     var name = row.Span[0] as string;
    ///     var age = row.Span[1] as double?;
    ///     var salary = row.Span[2] as double?;
    ///     
    ///     Console.WriteLine($"{name}: Age {age}, Salary {salary:C}");
    /// }
    /// </code>
    /// </example>
    public async IAsyncEnumerable<ReadOnlyMemory<object?>> ReadRowsAsync(RecordReadOptions? options = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        options ??= new RecordReadOptions();

        var bufferSize = options.FileBufferSize ?? Math.Max(2 * Metadata.PageLength, Environment.SystemPageSize);
        var selectAll = options.ShouldSelectAllColumns();
        var indices = options.GetColumnIndices(Columns);
        var length = selectAll ? Metadata.ColumnCount : indices.Count;
        var serializer = selectAll
            ? new ColumnSerializer(Columns)
            : new SubsetOfColumnsSerializer(Columns, indices);

        await using var stream = new FileStream(
            _filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: bufferSize,
            useAsync: true);

        stream.Position = Metadata.HeaderLength;

        using var buffer1 = new PooledMemory<byte>(Metadata.PageLength);
        using var buffer2 = new PooledMemory<byte>(Metadata.PageLength);
        using var values = new PooledMemory<object?>(length);

        var decompressor = Metadata.Decompressor;
        var totalRowsProcessed = 0L;
        var rowsReturned = 0L;
        var rowsToSkip = options.SkipRows ?? 0;
        var maxRows = options.MaxRows ?? long.MaxValue;

        var buffers = (Current: buffer1, Next: buffer2);
        var readTask = stream.ReadAsync(buffers.Current.Memory, ct);
        while (true)
        {
            var bytesRead = await readTask;
            if (bytesRead < Metadata.PageLength)
                break;

            readTask = stream.ReadAsync(buffers.Next.Memory, ct);

            var page = SasPageFactory.CreatePage(buffers.Current.Memory, Metadata, decompressor, totalRowsProcessed);
            foreach (var row in page.EnumerateRows())
            {
                if (totalRowsProcessed++ >= Metadata.RowCount)
                    yield break;

                if (rowsToSkip-- > 0)
                    continue;

                if (rowsReturned++ >= maxRows)
                    yield break;

                serializer.Deserialize(row.Span, values.Span);
                yield return values.Memory[..length];

                ct.ThrowIfCancellationRequested();
            }

            buffers = (Current: buffers.Next, Next: buffers.Current);

            if (totalRowsProcessed >= Metadata.RowCount)
                break;
        }
    }

    /// <summary>
    /// Asynchronously reads rows from the SAS dataset and transforms them using a custom function.
    /// </summary>
    /// <typeparam name="T">The type of objects to return after transformation.</typeparam>
    /// <param name="transform">A function to transform each row into the desired type.</param>
    /// <param name="options">Options to control the reading behavior, or null for default options.</param>
    /// <param name="ct">A cancellation token to cancel the enumeration.</param>
    /// <returns>
    /// An asynchronous enumerable that yields objects of type T, where each object
    /// is the result of applying the transform function to a row from the dataset.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when transform is null.</exception>
    /// <exception cref="InvalidDataException">Thrown when corrupted data is encountered during reading.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the cancellation token.</exception>
    /// <remarks>
    /// This method is a convenience wrapper around ReadRowsAsync that applies a transformation
    /// function to each row. It's useful for converting rows to strongly-typed objects or
    /// performing custom data processing during enumeration.
    /// 
    /// The transform function receives a ReadOnlyMemory&lt;object?&gt; containing the row data
    /// and should return an object of type T. The memory passed to the transform function
    /// is valid only during the function call.
    /// 
    /// All the same performance characteristics and options from ReadRowsAsync apply to this method.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Transform to anonymous objects
    /// await foreach (var person in reader.ReadRecordsAsync(row => new
    /// {
    ///     Name = row.Span[0] as string,
    ///     Age = row.Span[1] as double?,
    ///     Salary = row.Span[2] as double?
    /// }))
    /// {
    ///     Console.WriteLine($"{person.Name}: {person.Age} years old");
    /// }
    /// 
    /// // Transform to custom class
    /// await foreach (var person in reader.ReadRecordsAsync(row => new Person
    /// {
    ///     Name = row.Span[0] as string ?? "",
    ///     Age = (int)(row.Span[1] as double? ?? 0),
    ///     Salary = row.Span[2] as double? ?? 0.0
    /// }))
    /// {
    ///     // Process strongly-typed Person objects
    /// }
    /// </code>
    /// </example>
    public async IAsyncEnumerable<T> ReadRecordsAsync<T>(Func<ReadOnlyMemory<object?>, T> transform, RecordReadOptions? options = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transform, nameof(transform));

        await foreach (var row in ReadRowsAsync(options, ct))
        {
            yield return transform(row);
        }
    }

    /// <summary>
    /// Asynchronously releases all resources used by the SasDataReader.
    /// </summary>
    /// <returns>A ValueTask representing the asynchronous disposal operation.</returns>
    /// <remarks>
    /// This method releases the file lock and closes any open file handles.
    /// After disposal, the SasDataReader instance should not be used for any operations.
    /// 
    /// It's important to properly dispose of SasDataReader instances to release file locks
    /// and allow other processes to access the file.
    /// </remarks>
    public ValueTask DisposeAsync()
    {
        return _fileLock.DisposeAsync();
    }
}