using Sas7Bdat.Core.Decompression;

namespace Sas7Bdat.Core;

public sealed class SasFileMetadata
{
    public Endian Endianness { get; set; }
    public Format Format { get; set; }
    public Platform Platform { get; set; }
    public string Encoding { get; set; } = "WINDOWS-1252";
    public string DatasetName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; }
    public DateTime DateModified { get; set; }
    public int HeaderLength { get; set; }
    public int PageLength { get; set; }
    public int PageCount { get; set; }
    public string SasRelease { get; set; } = string.Empty;
    public string SasServerType { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
    public string OsName { get; set; } = string.Empty;
    public Compression Compression { get; set; }
    public string Creator { get; set; } = string.Empty;
    public string CreatorProc { get; set; } = string.Empty;

    public long RowCount { get; set; }
    public int ColumnCount { get; set; }
    public int RowLength { get; set; }
    public int ColCountP1 { get; set; }
    public int ColCountP2 { get; set; }
    public long MixPageRowCount { get; set; }
    public int Lcs { get; set; }
    public int Lcp { get; set; }

    public IDecompressor Decompressor
    {
        get
        {
            return Compression switch
            {
                Compression.Rle => new RleDecompressor(),
                Compression.Rdc => new RdcDecompressor(),
                _ => new NoDecompressor()
            };
        }
    }
}