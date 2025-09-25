using Sas7Bdat.Core.Metadata;
using Sas7Bdat.Core.Pages;
using Sas7Bdat.Core.Serializers;

namespace Sas7Bdat.Core.Headers;

internal sealed class HeaderReader(string filePath)
{
    private int _align1;
    private int _align2;
    private int _totalAlign;

    public async Task<(SasFileMetadata metadata, ReadOnlyMemory<SasColumnInfo> columns)> ReadMetadataAsync(CancellationToken ct = default)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        using var initialBuffer = new PooledMemory<byte>(SasConstants.HeaderSize);
        var bytesRead = await stream.ReadAsync(initialBuffer.Memory, ct);
        if (bytesRead < SasConstants.HeaderSize)
            throw new InvalidDataException($"File is too small to be a valid SAS7BDAT file. {filePath} may be corrupted.");

        ValidateMagicNumber(initialBuffer.Span);

        var (endian, format) = DetermineFormatAndEndianness(initialBuffer.Span);

        var metadata = new SasFileMetadata
        {
            Endianness = endian,
            Format = format
        };

        ExtractInitialProperties(initialBuffer.Span[..SasConstants.HeaderSize], metadata);

        metadata.HeaderLength = (int)endian.ReadUInt32At(initialBuffer.Span[..SasConstants.HeaderSize], 196 + _align1);

        if (metadata.HeaderLength > SasConstants.HeaderSize)
        {
            using var fullBuffer = new PooledMemory<byte>(metadata.HeaderLength);
            initialBuffer.Span.CopyTo(fullBuffer.Span);

            var remainingBytes = metadata.HeaderLength - SasConstants.HeaderSize;
            bytesRead = await stream.ReadAsync(fullBuffer.Memory.Slice(SasConstants.HeaderSize, remainingBytes), ct);

            if (bytesRead < remainingBytes)
                throw new InvalidDataException($"Incomplete header. {filePath} may be corrupted.");

            ExtractFullProperties(fullBuffer.Span[..metadata.HeaderLength], metadata);

        }
        else
        {
            ExtractFullProperties(initialBuffer.Span[..SasConstants.HeaderSize], metadata);
        }

        var metadataReader = new MetadataReader(stream, metadata);
        var columns = await metadataReader.ReadMetadataAsync(ct);
        return (metadata, columns.ToArray());

    }

    private void ValidateMagicNumber(ReadOnlySpan<byte> buffer)
    {
        var magicSpan = buffer[..SasConstants.MagicNumber.Length];
        if (!magicSpan.SequenceEqual(SasConstants.MagicNumber))
            throw new InvalidDataException($"Invalid SAS7BDAT magic number. {filePath} may be corrupted.");
    }

    private (Endian endian, Format format) DetermineFormatAndEndianness(ReadOnlySpan<byte> buffer)
    {
        var format = buffer[32] == '3' ? Format.Bit64 : Format.Bit32;

        if (format == Format.Bit64)
            _align2 = 4;

        if (buffer[35] == '3')
            _align1 = 4;

        _totalAlign = _align1 + _align2;

        var endian = buffer[37] == 0x01 ? Endian.Little : Endian.Big;

        return (endian, format);
    }

    private static void ExtractInitialProperties(ReadOnlySpan<byte> buffer, SasFileMetadata metadata)
    {
        metadata.Platform = metadata.Endianness.ReadByteAt(buffer, 39) switch
        {
            (byte)'1' => Platform.Unix,
            (byte)'2' => Platform.Windows,
            _ => Platform.Unknown
        };

        var encodingByte = metadata.Endianness.ReadByteAt(buffer, 70);
        metadata.Encoding = SasEncoding.GetEncodingName(encodingByte);
    }

    private void ExtractFullProperties(ReadOnlySpan<byte> buffer, SasFileMetadata metadata)
    {
        var encoding = SasEncoding.GetEncodingByName(metadata.Encoding);
        var endian = metadata.Endianness;

        metadata.DatasetName = buffer.ReadStringAt(92, 64, encoding).Trim();
        metadata.FileType = buffer.ReadStringAt(156, 8, encoding).Trim();

        metadata.DateCreated = FieldSerializers.ConvertSasDateTimeSeconds(endian.ReadDoubleAt(buffer, 164 + _align1)) as DateTime? ?? throw new InvalidDataException($"Invalid header. {filePath} may be corrupted.");
        metadata.DateModified = FieldSerializers.ConvertSasDateTimeSeconds(endian.ReadDoubleAt(buffer, 172 + _align1)) as DateTime? ?? throw new InvalidDataException($"Invalid header. {filePath} may be corrupted.");

        metadata.PageLength = (int)endian.ReadUInt32At(buffer, 200 + _align1);
        metadata.PageCount = (int)endian.ReadUInt32At(buffer, 204 + _align1);

        metadata.SasRelease = buffer.ReadStringAt(216 + _totalAlign, 8, encoding).Trim();
        metadata.SasServerType = buffer.ReadStringAt(224 + _totalAlign, 16, encoding).Trim();
        metadata.OsType = buffer.ReadStringAt(240 + _totalAlign, 16, encoding).Trim();

        if (endian.ReadByteAt(buffer, 272 + _totalAlign) != 0)
        {
            metadata.OsName = buffer.ReadStringAt(272 + _totalAlign, 16, encoding).Trim();
        }
        else
        {
            metadata.OsName = buffer.ReadStringAt(256 + _totalAlign, 16, encoding).Trim();
        }
    }

    internal sealed class SasConstants
    {
        public const int HeaderSize = 288;

        public static readonly byte[] MagicNumber =
        [
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0x00,
            0xc2,
            0xea,
            0x81,
            0x60,
            0xb3,
            0x14,
            0x11,
            0xcf,
            0xbd,
            0x92,
            0x08,
            0x00,
            0x09,
            0xc7,
            0x31,
            0x8c,
            0x18,
            0x1f,
            0x10,
            0x11
        ];
    }
}