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
    public ReadOnlyMemory<SasColumnInfo> Columns { get; }

    private SasDataReader(string filePath, SasFileMetadata metadata, ReadOnlyMemory<SasColumnInfo> columns)
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
            bufferSize: Math.Max(Metadata.PageLength, 4096),
            useAsync: true);

        stream.Position = Metadata.HeaderLength;

        using var buffer = new PooledMemory<byte>(Metadata.PageLength);
        using var values = new PooledMemory<object?>(length);

        var decompressor = Metadata.Decompressor;
        var totalRowsProcessed = 0L;
        var rowsReturned = 0L;
        var rowsToSkip = options.SkipRows ?? 0;
        var maxRows = options.MaxRows ?? long.MaxValue;

        while (true)
        {
            var bytesRead = await stream.ReadAsync(buffer.Memory, ct);
            if (bytesRead < Metadata.PageLength)
                break;

            var page = SasPageFactory.CreatePage(buffer.Memory, Metadata, decompressor, totalRowsProcessed);
            foreach (var row in page.EnumerateRows())
            {
                if (totalRowsProcessed++ >= Metadata.RowCount)
                    yield break;

                if (rowsToSkip-- > 0)
                    continue;

                if (rowsReturned++ >= maxRows)
                    yield break;

                serializer.Deserialize(row, values.Span);
                yield return values.Memory[..length];

                if (ct.IsCancellationRequested)
                    yield break;
            }

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
