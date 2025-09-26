using System.Runtime.CompilerServices;
using Sas7Bdat.Core.Headers;
using Sas7Bdat.Core.Serializers;
using Sas7Bdat.Core.Pages;

namespace Sas7Bdat.Core;

public sealed class SasDataReader : IAsyncDisposable
{
    private readonly string _filePath;
    private readonly FileStream _fileLock;

    public SasFileMetadata Metadata { get; }
    public SasColumnInfo[] Columns { get; }

    private SasDataReader(string filePath, SasFileMetadata metadata, SasColumnInfo[] columns)
    {
        _filePath = filePath;
        Metadata = metadata;
        Columns = columns;
        _fileLock = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public static async Task<SasDataReader> OpenFileAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filePath, nameof(filePath));
        
        if (!File.Exists(filePath)) throw new FileNotFoundException($"File not found: {filePath}", filePath);

        var headerReader = new HeaderReader(filePath);
        var (metadata, columns) = await headerReader.ReadMetadataAsync(ct);

        return new SasDataReader(filePath, metadata, columns);
    }

    public async IAsyncEnumerable<ReadOnlyMemory<object?>> ReadRowsAsync(RecordReadOptions? options = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        options ??= new RecordReadOptions();

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
            bufferSize: MoreMath.Max([2*Metadata.PageLength, 8*Environment.SystemPageSize]),
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

        var buffers = (Current: buffer1, Next : buffer2);
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

    public async IAsyncEnumerable<T> ReadRecordsAsync<T>(Func<ReadOnlyMemory<object?>, T> transform, RecordReadOptions? options = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transform, nameof(transform));

        await foreach (var row in ReadRowsAsync(options, ct))
        {
            yield return transform(row);
        }
    }

    public ValueTask DisposeAsync()
    {
        return _fileLock.DisposeAsync();
    }
}
