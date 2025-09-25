namespace Sas7Bdat.Core.Tests;

public record JsonDataset
{
    public SasFileMetadata Metadata { get; set; } = new();
    public object?[][] Data { get; set; } = [];
}