using System.Text;

namespace Sas7Bdat.Core.Headers
{
    internal static class SasEncoding
    {
        private const string DefaultEncoding = "WINDOWS-1252";

        static SasEncoding()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public static string GetEncodingName(byte encodingByte)
        {
            return encodingByte switch
            {
                20 => "UTF-8",
                28 => "US-ASCII",
                29 => "ISO-8859-1",
                30 => "ISO-8859-2",
                31 => "ISO-8859-3",
                32 => "ISO-8859-4",
                33 => "ISO-8859-5",
                34 => "ISO-8859-6",
                35 => "ISO-8859-7",
                36 => "ISO-8859-8",
                37 => "ISO-8859-9",
                39 => "ISO-8859-11",
                40 => "ISO-8859-15",
                41 => "CP437",
                42 => "CP850",
                43 => "CP852",
                44 => "CP857",
                45 => "CP858",
                46 => "CP862",
                47 => "CP864",
                48 => "CP865",
                49 => "CP866",
                50 => "CP869",
                51 => "CP874",
                52 => "CP921",
                53 => "CP922",
                54 => "CP1129",
                55 => "CP720",
                56 => "CP737",
                57 => "CP775",
                58 => "CP860",
                59 => "CP863",
                60 => "WINDOWS-1250",
                61 => "WINDOWS-1251",
                62 => "WINDOWS-1252",
                63 => "WINDOWS-1253",
                64 => "WINDOWS-1254",
                65 => "WINDOWS-1255",
                66 => "WINDOWS-1256",
                67 => "WINDOWS-1257",
                68 => "WINDOWS-1258",
                118 => "CP950",
                119 => "EUC-TW",
                123 => "BIG5",
                125 => "GB18030",
                126 => "CP936",
                134 => "EUC-JP",
                136 => "CP949",
                137 => "CP942",
                138 => "CP932",
                140 => "EUC-KR",
                141 => "CP949",
                142 => "CP949",
                167 => "ISO-2022-JP",
                168 => "ISO-2022-KR",
                169 => "ISO-2022-CN",
                172 => "ISO-2022-CN-EXT",
                205 => "GB18030",
                227 => "ISO-8859-14",
                242 => "ISO-8859-13",
                248 => "SHIFT_JIS",
                _ => DefaultEncoding
            };
        }

        public static Encoding GetEncodingByName(string encodingName)
        {
            try
            {
                var codePage = encodingName switch
                {
                    "US-ASCII" => 20127,
                    "UTF-8" => 65001,
                    "ISO-8859-1" => 28591,
                    "ISO-8859-2" => 28592,
                    "ISO-8859-3" => 28593,
                    "ISO-8859-4" => 28594,
                    "ISO-8859-5" => 28595,
                    "ISO-8859-6" => 28596,
                    "ISO-8859-7" => 28597,
                    "ISO-8859-8" => 28598,
                    "ISO-8859-9" => 28599,
                    "ISO-8859-11" => 874,
                    "ISO-8859-13" => 28603,
                    "ISO-8859-14" => 28604,
                    "ISO-8859-15" => 28605,
                    "WINDOWS-1250" => 1250,
                    "WINDOWS-1251" => 1251,
                    "WINDOWS-1252" => 1252,
                    "WINDOWS-1253" => 1253,
                    "WINDOWS-1254" => 1254,
                    "WINDOWS-1255" => 1255,
                    "WINDOWS-1256" => 1256,
                    "WINDOWS-1257" => 1257,
                    "WINDOWS-1258" => 1258,
                    "CP437" => 437,
                    "CP850" => 850,
                    "CP852" => 852,
                    "CP857" => 857,
                    "CP858" => 858,
                    "CP860" => 860,
                    "CP862" => 862,
                    "CP863" => 863,
                    "CP864" => 864,
                    "CP865" => 865,
                    "CP866" => 866,
                    "CP869" => 869,
                    "CP874" => 874,
                    "CP932" => 932,
                    "CP936" => 936,
                    "CP942" => 942,
                    "CP949" => 949,
                    "CP950" => 950,
                    "EUC-JP" => 51932,
                    "EUC-KR" => 51949,
                    "EUC-TW" => 51950,
                    "GB18030" => 54936,
                    "BIG5" => 950,
                    "SHIFT_JIS" => 932,
                    "ISO-2022-JP" => 50220,
                    "ISO-2022-KR" => 50225,
                    "ISO-2022-CN" => 50227,
                    _ => -1
                };

                if (codePage != -1)
                {
                    return Encoding.GetEncoding(codePage);
                }

                if (encodingName.StartsWith("CP", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(encodingName[2..], out var cp))
                {
                    return Encoding.GetEncoding(cp);
                }

                return Encoding.GetEncoding(encodingName);
            }
            catch
            {
                return Encoding.GetEncoding(1252);
            }
        }
    }
}