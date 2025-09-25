using System.Text;
using Sas7Bdat.Core.Headers;
using Sas7Bdat.Core.Pages;
using Sas7Bdat.Core.Serializers;

namespace Sas7Bdat.Core.Metadata;

internal sealed class MetadataReader
{
    private readonly Stream _stream;
    private readonly SasFileMetadata _metadata;
    private readonly Endian _endian;
    private readonly Format _format;
    private readonly Encoding _encoding;
    private readonly int _integerSize;
    private readonly int _pageBitOffset;

    private readonly List<string> _columnTexts = [];
    private readonly List<string> _columnNames = [];
    private readonly List<string> _columnFormats = [];
    private readonly List<string> _columnLabels = [];
    private readonly List<int> _columnDataOffsets = [];
    private readonly List<int> _columnDataLengths = [];
    private readonly List<StorageType> _columnDataTypes = [];

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

    private PageHeader ReadPageHeader(ReadOnlySpan<byte> buffer)
    {
        var type = _endian.ReadUInt16At(buffer, _pageBitOffset);
        var blockCount = _endian.ReadUInt16At(buffer, _pageBitOffset + 2);
        var subheaderCount = _endian.ReadUInt16At(buffer, _pageBitOffset + 4);
        return new PageHeader((SasPageType)type, blockCount, subheaderCount);
    }

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
                            _columnDataTypes.Add(columnDataType == 1 ? StorageType.Number: StorageType.String);
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