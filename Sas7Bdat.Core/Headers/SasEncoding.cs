using System.Text;

namespace Sas7Bdat.Core.Headers
{
    /// <summary>
    /// Provides encoding detection and conversion services for SAS7BDAT files.
    /// </summary>
    /// <remarks>
    /// SAS files can be created with various character encodings depending on the platform,
    /// locale, and SAS configuration used during creation. This class maps SAS encoding
    /// byte identifiers to their corresponding .NET encoding names and provides
    /// methods to obtain Encoding objects for text data conversion.
    /// 
    /// The class supports a comprehensive range of encodings including:
    /// <list type="bullet">
    /// <item><description>Unicode encodings (UTF-8, UTF-16, etc.)</description></item>
    /// <item><description>Western European encodings (ISO-8859 series, Windows-125x series)</description></item>
    /// <item><description>Eastern European and Cyrillic encodings</description></item>
    /// <item><description>Asian encodings (Chinese, Japanese, Korean)</description></item>
    /// <item><description>Legacy DOS code pages (CP437, CP850, etc.)</description></item>
    /// </list>
    /// 
    /// The static constructor registers the CodePagesEncodingProvider to ensure
    /// that legacy and extended encodings are available in .NET applications.
    /// When encoding detection fails, the class falls back to Windows-1252,
    /// which is the most common encoding for SAS files created on Windows systems.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get encoding name from SAS byte identifier
    /// string encodingName = SasEncoding.GetEncodingName(62); // Returns "WINDOWS-1252"
    /// 
    /// // Get actual .NET Encoding object
    /// Encoding encoding = SasEncoding.GetEncodingByName("WINDOWS-1252");
    /// 
    /// // Use encoding to decode text data
    /// string text = encoding.GetString(byteData);
    /// </code>
    /// </example>
    internal static class SasEncoding
    {
        /// <summary>
        /// The default encoding name used when no specific encoding can be determined.
        /// </summary>
        /// <value>The string "WINDOWS-1252" representing the Windows Western European encoding.</value>
        /// <remarks>
        /// Windows-1252 is used as the default because it's the most commonly used encoding
        /// for SAS files created on Windows systems, which represent the majority of SAS
        /// installations. This encoding provides reasonable compatibility for Western European
        /// text data and includes common symbols and accented characters.
        /// </remarks>
        private const string DefaultEncoding = "WINDOWS-1252";

        /// <summary>
        /// Initializes the SasEncoding class by registering additional encoding providers.
        /// </summary>
        /// <remarks>
        /// This static constructor ensures that the CodePagesEncodingProvider is registered,
        /// which provides access to legacy and extended character encodings that are not
        /// available by default in .NET Core/.NET 5+. This registration is essential for
        /// supporting the full range of encodings that SAS files may use.
        /// 
        /// Without this registration, many of the legacy code page encodings (CP437, CP850, etc.)
        /// and some international encodings would not be available, leading to encoding
        /// conversion failures.
        /// </remarks>
        static SasEncoding()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// Maps a SAS encoding byte identifier to its corresponding encoding name.
        /// </summary>
        /// <param name="encodingByte">The byte value that identifies the encoding in the SAS file header.</param>
        /// <returns>
        /// The name of the encoding corresponding to the byte identifier, or the default encoding
        /// if the byte value is not recognized.
        /// </returns>
        /// <remarks>
        /// This method provides the mapping between SAS internal encoding identifiers and
        /// standard encoding names. The mapping covers a comprehensive range of encodings
        /// organized by category:
        /// 
        /// **Unicode and Standard Encodings:**
        /// <list type="bullet">
        /// <item><description>20: UTF-8</description></item>
        /// <item><description>28: US-ASCII</description></item>
        /// </list>
        /// 
        /// **ISO-8859 Series (Western and International):**
        /// <list type="bullet">
        /// <item><description>29-40: ISO-8859-1 through ISO-8859-15 (various European languages)</description></item>
        /// <item><description>227: ISO-8859-14 (Celtic)</description></item>
        /// <item><description>242: ISO-8859-13 (Baltic Rim)</description></item>
        /// </list>
        /// 
        /// **DOS Code Pages:**
        /// <list type="bullet">
        /// <item><description>41-50: Various CP encodings (CP437, CP850, CP852, etc.)</description></item>
        /// <item><description>55-59: Additional code pages (CP720, CP737, etc.)</description></item>
        /// </list>
        /// 
        /// **Windows Code Pages:**
        /// <list type="bullet">
        /// <item><description>60-68: WINDOWS-1250 through WINDOWS-1258 (European, Cyrillic, etc.)</description></item>
        /// </list>
        /// 
        /// **Asian Encodings:**
        /// <list type="bullet">
        /// <item><description>118-142: Various Chinese, Japanese, and Korean encodings</description></item>
        /// <item><description>167-172: ISO-2022 series for Asian languages</description></item>
        /// <item><description>205: GB18030 (Chinese)</description></item>
        /// <item><description>248: SHIFT_JIS (Japanese)</description></item>
        /// </list>
        /// 
        /// When an unrecognized byte value is encountered, the method returns the default
        /// encoding (Windows-1252) to ensure graceful degradation rather than failure.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Common Western European encoding
        /// string encoding1 = SasEncoding.GetEncodingName(62); // "WINDOWS-1252"
        /// 
        /// // UTF-8 encoding
        /// string encoding2 = SasEncoding.GetEncodingName(20); // "UTF-8"
        /// 
        /// // Japanese encoding
        /// string encoding3 = SasEncoding.GetEncodingName(248); // "SHIFT_JIS"
        /// 
        /// // Unknown encoding (falls back to default)
        /// string encoding4 = SasEncoding.GetEncodingName(999); // "WINDOWS-1252"
        /// </code>
        /// </example>
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

        /// <summary>
        /// Retrieves a .NET Encoding object for the specified encoding name.
        /// </summary>
        /// <param name="encodingName">The name of the encoding to retrieve.</param>
        /// <returns>
        /// A .NET Encoding object that can be used for character conversion, or the
        /// default Windows-1252 encoding if the specified encoding cannot be found.
        /// </returns>
        /// <remarks>
        /// This method converts encoding names to actual .NET Encoding objects that can be
        /// used for character data conversion. The method includes comprehensive mapping
        /// for all encoding names that may be returned by GetEncodingName().
        /// 
        /// **Encoding Resolution Strategy:**
        /// 1. **Direct Code Page Mapping**: Maps well-known encoding names to their code page numbers
        /// 2. **CP Prefix Parsing**: Handles "CP" prefixed names by extracting the numeric code page
        /// 3. **Name-Based Resolution**: Attempts direct resolution using the encoding name
        /// 4. **Fallback Handling**: Returns Windows-1252 if all resolution attempts fail
        /// 
        /// **Code Page Mappings:**
        /// <list type="bullet">
        /// <item><description>ASCII and Unicode: US-ASCII (20127), UTF-8 (65001)</description></item>
        /// <item><description>ISO-8859 series: 28591-28605 for ISO-8859-1 through ISO-8859-15</description></item>
        /// <item><description>Windows series: 1250-1258 for WINDOWS-1250 through WINDOWS-1258</description></item>
        /// <item><description>DOS code pages: 437, 850, 852, etc.</description></item>
        /// <item><description>Asian encodings: 932 (SHIFT_JIS), 936 (CP936), 949 (CP949), etc.</description></item>
        /// </list>
        /// 
        /// **Error Handling:**
        /// The method uses comprehensive exception handling to gracefully deal with:
        /// <list type="bullet">
        /// <item><description>Unsupported encoding names</description></item>
        /// <item><description>Invalid code page numbers</description></item>
        /// <item><description>Platform-specific encoding availability issues</description></item>
        /// </list>
        /// 
        /// When any error occurs during encoding resolution, the method returns
        /// Encoding.GetEncoding(1252) (Windows-1252) as a safe fallback that can
        /// handle most Western European text data reasonably well.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get encoding for Windows-1252
        /// Encoding enc1 = SasEncoding.GetEncodingByName("WINDOWS-1252");
        /// 
        /// // Get encoding for UTF-8
        /// Encoding enc2 = SasEncoding.GetEncodingByName("UTF-8");
        /// 
        /// // Get encoding using CP prefix
        /// Encoding enc3 = SasEncoding.GetEncodingByName("CP850");
        /// 
        /// // Invalid encoding name (returns Windows-1252 fallback)
        /// Encoding enc4 = SasEncoding.GetEncodingByName("INVALID-ENCODING");
        /// 
        /// // Use encoding to decode bytes
        /// string text = enc1.GetString(sasTextBytes);
        /// </code>
        /// </example>
        /// <exception cref="ArgumentException">
        /// This method does not throw exceptions. Invalid encoding names result in
        /// the default Windows-1252 encoding being returned.
        /// </exception>
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