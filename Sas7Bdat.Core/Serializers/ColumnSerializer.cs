using System.Runtime.CompilerServices;

namespace Sas7Bdat.Core.Serializers;

internal class ColumnSerializer(ReadOnlyMemory<SasColumnInfo> columns)
{
    protected readonly ReadOnlyMemory<SasColumnInfo> Columns = columns;


    public virtual void Deserialize(ReadOnlySpan<byte> rowData, Span<object?> destination)
    {
        var span = Columns.Span;
        for (var i = 0; i < span.Length; i++)
        {
            destination[i] = ExtractValue(rowData, span[i]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]

    protected static object? ExtractValue(ReadOnlySpan<byte> rowData, SasColumnInfo column)
    {
        return column.DataSerializer.Deserialize(rowData.Slice(column.Offset, column.Length));
    }
}

public interface IDataSerializer
{
    object? Deserialize(ReadOnlySpan<byte> bytes);
}