namespace Sas7Bdat.Core
{
    public enum Endian : byte
    {
        Little = 1,
        Big = 2
    }

    public enum Format : byte
    {
        Bit32 = 1,
        Bit64 = 2
    }

    public enum Platform : byte
    {
        Unknown = 0,
        Unix = 1,
        Windows = 2
    }

    public enum Compression : byte
    {
        None = 0,
        Rle = 1,
        Rdc = 2
    }

    public enum ColumnType : byte
    {
        Unknown = 0,
        String = 1,
        Number = 2,
        DateTime = 3,
        Date = 4,
        Time = 5
    }

    public enum StorageType : byte
    {
        Unknown = 0,
        String = 1,
        Number = 2
    }
}