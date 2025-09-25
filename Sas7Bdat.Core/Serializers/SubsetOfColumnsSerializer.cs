namespace Sas7Bdat.Core.Serializers;

internal class SubsetOfColumnsSerializer(ReadOnlyMemory<SasColumnInfo> columns, HashSet<int> columnIndices) : ColumnSerializer(columns)
{
    public override void Deserialize(ReadOnlyMemory<byte> rowData, Span<object?> destination)
    {
        var i = 0;
        var valueIndex = 0;
        foreach (var column in _columns.Span)
        {
            if (!columnIndices.Contains(i++)) continue;
            destination[valueIndex++] = ExtractValue(rowData, column);
        }
    }
}