namespace Sas7Bdat.Core.Serializers;

internal class SubsetOfColumnsSerializer(ReadOnlyMemory<SasColumnInfo> columns, HashSet<int> columnIndices) : ColumnSerializer(columns)
{
    public override void Deserialize(ReadOnlySpan<byte> rowData, Span<object?> destination)
    {
        var valueIndex = 0;
        var span = Columns.Span;
        for (var i = 0; i < span.Length; i++)
        {
            if (!columnIndices.Contains(i)) continue;
            destination[valueIndex++] = ExtractValue(rowData, span[i]);
        }
    }
}