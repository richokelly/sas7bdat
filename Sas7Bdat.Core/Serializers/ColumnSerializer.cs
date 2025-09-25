namespace Sas7Bdat.Core.Serializers;

internal class ColumnSerializer(ReadOnlyMemory<SasColumnInfo> columns)
{
    protected readonly ReadOnlyMemory<SasColumnInfo> _columns = columns;

    public virtual void Deserialize(ReadOnlyMemory<byte> rowData, Span<object?> destination)
    {
        var i = 0;
        foreach (var column in _columns.Span)
        {
            destination[i++] = ExtractValue(rowData, column);
        }
    }

    protected object? ExtractValue(ReadOnlyMemory<byte> rowData, SasColumnInfo column)
    {
        return column.DataSerializer.Deserialize(rowData.Slice(column.Offset, column.Length).Span);
    }
}

public interface IDataSerializer
{
    object? Deserialize(ReadOnlySpan<byte> bytes);
}