namespace Sas7Bdat.Core.Pages;

internal static class SasPageTypeExtensions
{
    public static bool IsDataPage(this SasPageType pageType)
    {
        return pageType.HasFlag(SasPageType.Data);
    }

    public static bool IsMetaPage(this SasPageType pageType)
    {
        return pageType == SasPageType.Meta;
    }

    public static bool IsMixPage(this SasPageType pageType)
    {
        return pageType.HasFlag(SasPageType.Mix);
    }

    public static bool HasDeletedRecords(this SasPageType pageType)
    {
        return pageType.HasFlag(SasPageType.HasDeleted);
    }

    public static bool IsCompressed(this SasPageType pageType)
    {
        return pageType.HasFlag(SasPageType.Compressed);
    }

    public static bool IsExtended(this SasPageType pageType)
    {
        return pageType.HasFlag(SasPageType.Extended);
    }
}