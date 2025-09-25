namespace Sas7Bdat.Core.Headers;

internal enum SubheaderType
{
    Unknown,
    RowSize,
    ColumnSize,
    SubheaderCounts,
    ColumnText,
    ColumnName,
    ColumnAttributes,
    FormatAndLabel,
    ColumnList,
    Data
}