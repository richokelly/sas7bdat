using Sas7Bdat.Core.Serializers;

namespace Sas7Bdat.Core;

public record struct SasColumnInfo(
    string Name,
    string Label,
    string Format,
    ColumnType ColumnType,
    int Offset,
    int Length,
    int Index,
    IDataSerializer DataSerializer)
{
    public readonly Type Type =
        ColumnType switch
        {
            ColumnType.String => typeof(string),
            ColumnType.Number => typeof(double?),
            ColumnType.DateTime => typeof(DateTime?),
            ColumnType.Date => typeof(DateTime?),
            ColumnType.Time => typeof(TimeSpan?),
            _ => throw new ArgumentOutOfRangeException()
        };
}