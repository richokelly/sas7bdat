namespace Sas7Bdat.Core.Pages;

[Flags]
public enum SasPageType : ushort
{
    // Base types (mutually exclusive)
    Meta = 0x0000,                  // 0
    Data = 0x0100,                  // 256
    Mix = 0x0200,                   // 512
    Amd = 0x0400,                   // 1024
    MetadataContinuation = 0x4000,  // 16384
    Special = 0x8000,               // 32768

    // Modifier flags
    HasDeleted = 0x0080,     // 128 - pages with deleted records
    Extended = 0x0080,       // 128 - extended format (same bit, context-dependent)
    Compressed = 0x1000,     // 4096 - compressed data

    // Known combinations
    DataWithDeleted = Data | HasDeleted,           // 384
    Mix2 = Mix | Extended,                         // 640
    SpecialCompressed = Special | Compressed,      // 36864

    // Base type mask for extracting primary type
    BaseTypeMask = 0xFF00
}