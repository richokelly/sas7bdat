using Sas7Bdat.Core.Serializers;

namespace Sas7Bdat.Core;

public record struct SasColumnInfo(
    string Name,
    string Label,
    string Format,
    ColumnType Type,
    int Offset,
    int Length,
    int Index,
    IDataSerializer DataSerializer);