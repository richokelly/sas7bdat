using System.Text;
using Sas7Bdat.Core.Pages;
using Sas7Bdat.Core.Serializers;

namespace Sas7Bdat.Core.Headers;

/// <summary>
/// Reads detailed column metadata from SAS7BDAT file pages following the header.
/// </summary>
/// <remarks>
/// The MetadataReader processes data pages that follow the file header to extract comprehensive
/// column information including names, types, formats, labels, and storage characteristics.
/// Unlike the HeaderReader which focuses on file-level metadata, this class extracts the
/// detailed column schema that defines the structure of the dataset.
/// 
/// The reader processes various types of pages and subheaders:
/// <list type="bullet">
/// <item><description>AMD (Attribute Metadata) pages containing column definitions</description></item>
/// <item><description>Meta and MetadataContinuation pages with extended metadata</description></item>
/// <item><description>Mix and Mix2 pages containing both metadata and data</description></item>
/// <item><description>Data and DataWithDeleted pages (processing stops when encountered)</description></item>
/// </list>
/// 
/// The column information is extracted from several subheader types:
/// <list type="bullet">
/// <item><description>ColumnText: Contains raw text for names, formats, and labels</description></item>
/// <item><description>ColumnName: Maps column names to positions in the text</description></item>
/// <item><description>ColumnAttributes: Defines storage offsets, lengths, and data types</description></item>
/// <item><description>FormatAndLabel: Associates formats and labels with columns</description></item>
/// <item><description>RowSize: Provides row and column count information</description></item>
/// <item><description>ColumnSize: Specifies the total number of columns</description></item>
/// </list>
/// 
/// The class maintains separate collections for each type of column information and
/// combines them into comprehensive SasColumnInfo objects during the final assembly phase.
/// </remarks>
/// <example>
/// <code>
/// var metadataReader = new MetadataReader(fileStream, sasMetadata);
/// var columns = await metadataReader.ReadMetadataAsync(cancellationToken);
/// 
/// foreach (var column in columns)
/// {
///     Console.WriteLine($"Column: {column.Name}, Type: {column.ColumnType}, Format: {column.Format}");
/// }
/// </code>
/// </example>
internal sealed class MetadataReader
{
    /// <summary>
    /// The file stream positioned after the header for reading data pages.
    /// </summary>
    private readonly Stream _stream;

    /// <summary>
    /// The file metadata containing format and structure information.
    /// </summary>
    private readonly SasFileMetadata _metadata;

    /// <summary>
    /// The endianness used for reading multi-byte values.
    /// </summary>
    private readonly Endian _endian;

    /// <summary>
    /// The file format (32-bit or 64-bit) affecting data structure sizes.
    /// </summary>
    private readonly Format _format;

    /// <summary>
    /// The text encoding used for decoding string data.
    /// </summary>
    private readonly Encoding _encoding;

    /// <summary>
    /// The size in bytes of integer values (4 for 32-bit, 8 for 64-bit format).
    /// </summary>
    private readonly int _integerSize;

    /// <summary>
    /// The bit offset within pages where page headers begin (16 for 32-bit, 32 for 64-bit).
    /// </summary>
    private readonly int _pageBitOffset;

    /// <summary>
    /// Collection of text segments containing column names, formats, and labels.
    /// </summary>
    /// <remarks>
    /// This list stores raw text blocks extracted from ColumnText subheaders.
    /// Other subheaders reference into these text blocks using indices and offsets
    /// to extract specific column information.
    /// </remarks>
    private readonly List<string> _columnTexts = [];

    /// <summary>
    /// Collection of column names in their defined order.
    /// </summary>
    private readonly List<string> _columnNames = [];

    /// <summary>
    /// Collection of column format specifications.
    /// </summary>
    private readonly List<string> _columnFormats = [];

    /// <summary>
    /// Collection of column descriptive labels.
    /// </summary>
    private readonly List<string> _columnLabels = [];

    /// <summary>
    /// Collection of byte offsets where each column's data begins within a row.
    /// </summary>
    private readonly List<int> _columnDataOffsets = [];

    /// <summary>
    /// Collection of the length in bytes that each column occupies within a row.
    /// </summary>
    private readonly List<int> _columnDataLengths = [];

    /// <summary>
    /// Collection of the storage data types (String or Number) for each column.
    /// </summary>
    private readonly List<StorageType> _columnDataTypes = [];

    /// <summary>
    /// Initializes a new instance of the MetadataReader class.
    /// </summary>
    /// <param name="stream">The file stream positioned after the header for reading data pages.</param>
    /// <param name="metadata">The file metadata containing format and structure information.</param>
    /// <remarks>
    /// The constructor initializes format-dependent values that are used throughout the
    /// metadata reading process:
    /// 
    /// <list type="bullet">
    /// <item><description>Integer size: 4 bytes for 32-bit format, 8 bytes for 64-bit format</description></item>
    /// <item><description>Page bit offset: 16 bits for 32-bit format, 32 bits for 64-bit format</description></item>
    /// <item><description>Text encoding: Resolved from the metadata encoding specification</description></item>
    /// </list>
    /// 
    /// These values affect how subheaders are parsed and how data structures are interpreted
    /// within the metadata pages.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when stream or metadata parameters are null.
    /// </exception>
    public MetadataReader(Stream stream, SasFileMetadata metadata)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _endian = metadata.Endianness;
        _format = metadata.Format;
        _encoding = SasEncoding.GetEncodingByName(metadata.Encoding);
        _integerSize = _format == Format.Bit64 ? 8 : 4;
        _pageBitOffset = _format == Format.Bit64 ? 32 : 16;
    }

    /// <summary>
    /// Asynchronously reads and processes metadata pages to extract complete column information.
    /// </summary>
    /// <param name="ct">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// a list of SasColumnInfo objects describing all columns in the dataset.
    /// </returns>
    /// <remarks>
    /// This method processes pages sequentially until it encounters data pages or reaches
    /// the end of the file. The processing flow:
    /// 
    /// 1. **Page Reading**: Reads each page into a buffer
    /// 2. **Header Parsing**: Extracts page type and subheader count
    /// 3. **Page Filtering**: Processes only relevant page types (AMD, Meta, Mix, etc.)
    /// 4. **Subheader Processing**: Extracts column information from various subheader types
    /// 5. **Mix Page Handling**: Calculates row count for mixed-content pages
    /// 6. **Column Assembly**: Combines all extracted information into SasColumnInfo objects
    /// 
    /// The method stops processing when it encounters pure data pages (Data or DataWithDeleted)
    /// since these don't contain metadata information.
    /// 
    /// Page types processed:
    /// <list type="bullet">
    /// <item><description>AMD: Attribute metadata pages</description></item>
    /// <item><description>Meta/MetadataContinuation: Metadata pages</description></item>
    /// <item><description>Mix/Mix2: Pages containing both metadata and data</description></item>
    /// <item><description>Extended: Extended metadata pages</description></item>
    /// </list>
    /// 
    /// For Mix pages, the method calculates the MixPageRowCount by determining how many
    /// complete rows can fit in the data area after accounting for the page header and
    /// subheader information.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via the cancellation token.
    /// </exception>
    /// <example>
    /// <code>
    /// var columns = await metadataReader.ReadMetadataAsync(cancellationToken);
    /// 
    /// Console.WriteLine($"Found {columns.Count} columns:");
    /// foreach (var col in columns)
    /// {
    ///     Console.WriteLine($"  {col.Name}: {col.ColumnType} ({col.Length} bytes)");
    /// }
    /// </code>
    /// </example>
    public async Task<List<SasColumnInfo>> ReadMetadataAsync(CancellationToken ct = default)
    {
        using var pageBuffer = new PooledMemory<byte>(_metadata.PageLength);

        while (true)
        {
            var bytesRead = await _stream.ReadAsync(pageBuffer.Memory, ct);
            if (bytesRead < _metadata.PageLength)
                break;

            var pageHeader = ReadPageHeader(pageBuffer.Span);
            if (pageHeader.Type is not (SasPageType.Amd or SasPageType.Data or SasPageType.DataWithDeleted
                or SasPageType.Meta or SasPageType.MetadataContinuation or SasPageType.Mix or SasPageType.Mix2 or SasPageType.Extended)) continue;

            if (pageHeader.Type is SasPageType.Data or SasPageType.DataWithDeleted) break;

            ProcessPageSubheaders(pageBuffer.Span, pageHeader);
            if (pageHeader.Type is not (SasPageType.Mix or SasPageType.Mix2)) continue;

            if (_metadata.MixPageRowCount == 0)
            {
                var subheaderSize = 3 * _integerSize;
                var headerSize = _pageBitOffset + 8 + pageHeader.SubheaderCount * subheaderSize;

                var alignCorrection = headerSize % 8;
                if (alignCorrection != 0)
                    headerSize += 8 - alignCorrection;

                var dataAreaSize = _metadata.PageLength - headerSize;
                _metadata.MixPageRowCount = _metadata.RowLength > 0 ? dataAreaSize / _metadata.RowLength : 0;
            }

            break;
        }

        return CreateColumns();
    }

    /// <summary>
    /// Reads and parses the header information from a data page.
    /// </summary>
    /// <param name="buffer">The buffer containing the page data.</param>
    /// <returns>A PageHeader object containing the parsed header information.</returns>
    /// <remarks>
    /// Page headers contain essential information for processing the page content:
    /// <list type="bullet">
    /// <item><description>Type: Identifies the page type (AMD, Meta, Data, Mix, etc.)</description></item>
    /// <item><description>Block count: Number of data blocks on the page</description></item>
    /// <item><description>Subheader count: Number of subheaders containing metadata</description></item>
    /// </list>
    /// 
    /// The header is located at a format-dependent offset (_pageBitOffset) within the page
    /// and uses the file's endianness for multi-byte value interpretation.
    /// </remarks>
    private PageHeader ReadPageHeader(ReadOnlySpan<byte> buffer)
    {
        var type = _endian.ReadUInt16At(buffer, _pageBitOffset);
        var blockCount = _endian.ReadUInt16At(buffer, _pageBitOffset + 2);
        var subheaderCount = _endian.ReadUInt16At(buffer, _pageBitOffset + 4);
        return new PageHeader((SasPageType)type, blockCount, subheaderCount);
    }

    /// <summary>
    /// Processes all subheaders within a page to extract column metadata.
    /// </summary>
    /// <param name="buffer">The buffer containing the complete page data.</param>
    /// <param name="pageHeader">The parsed page header containing subheader count information.</param>
    /// <remarks>
    /// This method iterates through all subheaders on a page and processes them based on
    /// their signature type. Each subheader type contains different aspects of column information:
    /// 
    /// **RowSize Subheader:**
    /// - Row length, row count, column counts
    /// - LCS (Length of Character Set) and LCP (Length of Character Page) parameters
    /// - Mixed page row count information
    /// 
    /// **ColumnSize Subheader:**
    /// - Total number of columns in the dataset
    /// 
    /// **ColumnText Subheader:**
    /// - Raw text blocks containing column names, formats, labels
    /// - Compression algorithm identification
    /// - Creator and creator procedure information
    /// 
    /// **ColumnName Subheader:**
    /// - Mappings from column indices to names within text blocks
    /// - Text offset and length information for name extraction
    /// 
    /// **ColumnAttributes Subheader:**
    /// - Storage offsets and lengths for each column within rows
    /// - Data type information (String vs Number storage)
    /// 
    /// **FormatAndLabel Subheader:**
    /// - Format specifications for data presentation
    /// - Descriptive labels for columns
    /// 
    /// The method includes comprehensive validation:
    /// <list type="bullet">
    /// <item><description>Skips truncated subheaders (length = 0 or compression = TruncatedSubheaderId)</description></item>
    /// <item><description>Identifies subheader types using signature matching</description></item>
    /// <item><description>Handles format-dependent field offsets and sizes</description></item>
    /// <item><description>Processes compression information for the first ColumnText subheader</description></item>
    /// </list>
    /// </remarks>
    private void ProcessPageSubheaders(ReadOnlySpan<byte> buffer, PageHeader pageHeader)
    {
        var subheaderSize = 3 * _integerSize;

        for (var i = 0; i < pageHeader.SubheaderCount; i++)
        {
            var offset = _pageBitOffset + 8 + i * subheaderSize;
            var subheaderOffset = _endian.ReadIntegerBySizeAt(buffer, offset, _integerSize);
            var subheaderLength = _endian.ReadIntegerBySizeAt(buffer, offset + _integerSize, _integerSize);
            var compression = _endian.ReadByteAt(buffer, offset + _integerSize * 2);
            var type = _endian.ReadByteAt(buffer, offset + _integerSize * 2 + 1);
            var subheader = new PageSubheader((int)subheaderOffset, (int)subheaderLength, compression, type);

            if (subheader.Length == 0 || subheader.Compression == HeaderConstants.TruncatedSubheaderId)
                continue;

            var signature = buffer.ReadBytesAt(subheader.Offset, _integerSize);
            var subheaderType = SubheaderSignatures.IdentifySubheader(signature, _format);

            switch (subheaderType)
            {
                case SubheaderType.RowSize:
                    var lcsOffset = _format == Format.Bit64
                        ? SubheaderSignatures.Format64.LcsOffset
                        : SubheaderSignatures.Format32.LcsOffset;
                    var lcpOffset = _format == Format.Bit64
                        ? SubheaderSignatures.Format64.LcpOffset
                        : SubheaderSignatures.Format32.LcpOffset;

                    _metadata.Lcs = _endian.ReadUInt16At(buffer, subheader.Offset + lcsOffset);
                    _metadata.Lcp = _endian.ReadUInt16At(buffer, subheader.Offset + lcpOffset);
                    _metadata.RowLength = (int)_endian.ReadIntegerBySizeAt(buffer, subheader.Offset + 5 * _integerSize, _integerSize);
                    _metadata.RowCount = (long)_endian.ReadIntegerBySizeAt(buffer, subheader.Offset + 6 * _integerSize, _integerSize);
                    _metadata.ColCountP1 = (int)_endian.ReadIntegerBySizeAt(buffer, subheader.Offset + 9 * _integerSize, _integerSize);
                    _metadata.ColCountP2 = (int)_endian.ReadIntegerBySizeAt(buffer, subheader.Offset + 10 * _integerSize, _integerSize);
                    _metadata.MixPageRowCount = (long)_endian.ReadIntegerBySizeAt(buffer, subheader.Offset + 15 * _integerSize, _integerSize);
                    break;

                case SubheaderType.ColumnSize:
                    _metadata.ColumnCount = (int)_endian.ReadIntegerBySizeAt(buffer, subheader.Offset + _integerSize, _integerSize);
                    break;

                case SubheaderType.ColumnText:
                    var textLength = _endian.ReadUInt16At(buffer, subheader.Offset + _integerSize);
                    var text = buffer.ReadStringAt(subheader.Offset + _integerSize, textLength, _encoding);
                    _columnTexts.Add(text);

                    if (_columnTexts.Count == 1)
                    {
                        var compressionOffset = _format == Format.Bit64
                            ? SubheaderSignatures.Format64.CompressionOffset
                            : SubheaderSignatures.Format32.CompressionOffset;

                        if (text.Contains(HeaderConstants.RleCompression))
                            _metadata.Compression = Compression.Rle;
                        else if (text.Contains(HeaderConstants.RdcCompression))
                            _metadata.Compression = Compression.Rdc;

                        var compressionString = buffer.ReadStringAt(subheader.Offset + compressionOffset, 8, _encoding).Trim();

                        if (string.IsNullOrEmpty(compressionString))
                        {
                            _metadata.Lcs = 0;
                            _metadata.CreatorProc = buffer.ReadStringAt(
                                subheader.Offset + compressionOffset + 16, _metadata.Lcp, _encoding).Trim();
                        }
                        else if (compressionString == HeaderConstants.RleCompression)
                        {
                            _metadata.CreatorProc = buffer.ReadStringAt(
                                subheader.Offset + compressionOffset + 24, _metadata.Lcp, _encoding).Trim();
                        }
                        else if (_metadata.Lcs > 0)
                        {
                            _metadata.Lcp = 0;
                            _metadata.Creator = buffer.ReadStringAt(
                                subheader.Offset + compressionOffset, _metadata.Lcs, _encoding).Trim();
                        }
                    }
                    break;

                case SubheaderType.ColumnName:
                    var offsetMax = subheader.Offset + subheader.Length - 12 - _integerSize;

                    for (var offset1 = subheader.Offset + _integerSize + 8; offset1 <= offsetMax; offset1 += 8)
                    {
                        var idx = _endian.ReadUInt16At(buffer, offset1);
                        var nameOffset = _endian.ReadUInt16At(buffer, offset1 + 2);
                        var nameLength = _endian.ReadUInt16At(buffer, offset1 + 4);

                        var columnName = GetColumnTextSubstring(idx, nameOffset, nameLength);
                        _columnNames.Add(columnName);
                    }
                    break;

                case SubheaderType.ColumnAttributes:
                    {
                        var offsetMax1 = subheader.Offset + subheader.Length - 12 - _integerSize;

                        for (var offset1 = subheader.Offset + _integerSize + 8;
                             offset1 <= offsetMax1;
                             offset1 += _integerSize + 8)
                        {
                            var columnDataOffset = (int)_endian.ReadIntegerBySizeAt(buffer, offset1, _integerSize);
                            var columnDataLength = (int)_endian.ReadUInt32At(buffer, offset1 + _integerSize);
                            var columnDataType = _endian.ReadByteAt(buffer, offset1 + _integerSize + 6);

                            _columnDataOffsets.Add(columnDataOffset);
                            _columnDataLengths.Add(columnDataLength);
                            _columnDataTypes.Add(columnDataType == 1 ? StorageType.Number : StorageType.String);
                        }
                    }
                    break;

                case SubheaderType.FormatAndLabel:
                    {
                        var offset1 = subheader.Offset + 3 * _integerSize;

                        var formatIdx = _endian.ReadUInt16At(buffer, offset1 + 22);
                        var formatOffset = _endian.ReadUInt16At(buffer, offset1 + 24);
                        var formatLength = _endian.ReadUInt16At(buffer, offset1 + 26);
                        var labelIdx = _endian.ReadUInt16At(buffer, offset1 + 28);
                        var labelOffset = _endian.ReadUInt16At(buffer, offset1 + 30);
                        var labelLength = _endian.ReadUInt16At(buffer, offset1 + 32);

                        var columnFormat = GetColumnTextSubstring(formatIdx, formatOffset, formatLength);
                        var columnLabel = GetColumnTextSubstring(labelIdx, labelOffset, labelLength);

                        _columnFormats.Add(columnFormat);
                        _columnLabels.Add(columnLabel);
                    }
                    break;

                case SubheaderType.ColumnList:
                    break;
                case SubheaderType.SubheaderCounts:
                    break;
            }
        }
    }

    /// <summary>
    /// Extracts a substring from the column text collection using index and offset information.
    /// </summary>
    /// <param name="idx">The index of the text block in the _columnTexts collection.</param>
    /// <param name="offset">The character offset within the specified text block.</param>
    /// <param name="length">The length of the substring to extract.</param>
    /// <returns>The extracted and trimmed substring, or an empty string if extraction fails.</returns>
    /// <remarks>
    /// This method provides safe text extraction from the column text blocks with comprehensive
    /// boundary checking:
    /// 
    /// <list type="bullet">
    /// <item><description>Validates that the index is within the bounds of the text collection</description></item>
    /// <item><description>Checks that the offset doesn't exceed the text length</description></item>
    /// <item><description>Adjusts the length to prevent reading beyond the text end</description></item>
    /// <item><description>Trims whitespace from the extracted substring</description></item>
    /// </list>
    /// 
    /// The method handles edge cases gracefully:
    /// <list type="bullet">
    /// <item><description>Returns empty string for invalid indices</description></item>
    /// <item><description>Returns empty string for zero-length requests</description></item>
    /// <item><description>Returns empty string for out-of-bounds offsets</description></item>
    /// </list>
    /// 
    /// This safe extraction is essential because the indices and offsets come from the file
    /// metadata and could potentially be corrupted or invalid.
    /// </remarks>
    private string GetColumnTextSubstring(int idx, int offset, int length)
    {
        if (idx >= _columnTexts.Count || length == 0)
            return string.Empty;

        var text = _columnTexts[idx];
        if (offset >= text.Length)
            return string.Empty;

        var actualLength = Math.Min(length, text.Length - offset);
        return text.Substring(offset, actualLength).Trim();
    }

    /// <summary>
    /// Assembles the collected metadata into complete SasColumnInfo objects.
    /// </summary>
    /// <returns>A list of SasColumnInfo objects representing all columns in the dataset.</returns>
    /// <remarks>
    /// This method combines information from all the processed subheaders to create comprehensive
    /// column definitions. The assembly process:
    /// 
    /// 1. **Information Gathering**: Collects name, label, format, offset, length, and type for each column
    /// 2. **Default Handling**: Provides sensible defaults for missing information
    /// 3. **Type Inference**: Determines the logical column type from storage type and format
    /// 4. **Serializer Selection**: Chooses the appropriate data serializer for type conversion
    /// 5. **Object Creation**: Constructs complete SasColumnInfo objects
    /// 
    /// Default value handling:
    /// <list type="bullet">
    /// <item><description>Missing names: "Column{N}" where N is 1-based column number</description></item>
    /// <item><description>Missing labels: Empty string</description></item>
    /// <item><description>Missing formats: Empty string</description></item>
    /// <item><description>Missing offsets: 0</description></item>
    /// <item><description>Missing lengths: 0</description></item>
    /// <item><description>Missing types: StorageType.Unknown</description></item>
    /// </list>
    /// 
    /// The method uses FieldSerializers to:
    /// <list type="bullet">
    /// <item><description>Infer the logical column type from storage type and format string</description></item>
    /// <item><description>Select the appropriate converter for data type conversion during reading</description></item>
    /// </list>
    /// 
    /// Each SasColumnInfo object contains complete information needed for data access:
    /// <list type="bullet">
    /// <item><description>Identification: Name, label, format</description></item>
    /// <item><description>Storage: Offset, length, storage type</description></item>
    /// <item><description>Processing: Column type, data serializer</description></item>
    /// <item><description>Indexing: Zero-based column index</description></item>
    /// </list>
    /// </remarks>
    private List<SasColumnInfo> CreateColumns()
    {
        var columns = new List<SasColumnInfo>(_metadata.ColumnCount);

        for (var i = 0; i < _metadata.ColumnCount; i++)
        {
            var name = i < _columnNames.Count ? _columnNames[i] : $"Column{i + 1}";
            var label = i < _columnLabels.Count ? _columnLabels[i] : string.Empty;
            var format = i < _columnFormats.Count ? _columnFormats[i] : string.Empty;
            var offset = i < _columnDataOffsets.Count ? _columnDataOffsets[i] : 0;
            var length = i < _columnDataLengths.Count ? _columnDataLengths[i] : 0;
            var baseType = i < _columnDataTypes.Count ? _columnDataTypes[i] : StorageType.Unknown;

            var type = FieldSerializers.InferKind(baseType, format, length);
            var converter = FieldSerializers.GetSerializer(type, format, _endian, _encoding);

            columns.Add(new SasColumnInfo(name, label, format, type, offset, length, i, converter));
        }

        return columns;
    }
}