using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace AP2Win.Core.Discovery;

internal enum DnsRecordType : ushort
{
    A = 1,
    Ptr = 12,
    Txt = 16,
    Aaaa = 28,
    Srv = 33
}

internal abstract record DnsRecord(string Name, DnsRecordType Type);
internal sealed record PtrRecord(string Name, string Target) : DnsRecord(Name, DnsRecordType.Ptr);
internal sealed record TxtRecord(string Name, IReadOnlyDictionary<string, string> Values) : DnsRecord(Name, DnsRecordType.Txt);
internal sealed record SrvRecord(string Name, int Port, string Target) : DnsRecord(Name, DnsRecordType.Srv);
internal sealed record AddressRecord(string Name, DnsRecordType AddressType, IPAddress Address)
    : DnsRecord(Name, AddressType);

internal static class MdnsPacketParser
{
    private const int HeaderLength = 12;
    private const int QuestionCountOffset = 4;
    private const int AnswerCountOffset = 6;
    private const int AuthorityCountOffset = 8;
    private const int AdditionalCountOffset = 10;
    private const int QuestionFooterLength = 4;
    private const int RecordHeaderLength = 10;
    private const int RecordDataLengthOffset = 8;
    private const int SrvFixedFieldsLength = 6;
    private const int SrvPortOffset = 4;
    private const int Ipv4AddressLength = 4;
    private const int Ipv6AddressLength = 16;
    private const int LengthOctetSize = 1;
    private const byte NameTerminator = 0;
    private const byte CompressionMarkerMask = 0xc0;
    private const byte CompressionPointerValueMask = 0x3f;
    private const int CompressionPointerShift = 8;
    private const int MaximumCompressionPointers = 32;
    private const int MaximumLabelLength = 63;
    private const int UInt16Length = 2;
    private const byte EmptyTxtEntryLength = 0;
    private const int TxtKeyValueSeparatorLength = 1;

    public static IReadOnlyList<DnsRecord> Parse(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < HeaderLength)
        {
            throw new InvalidDataException("DNS packet is shorter than its header.");
        }

        var questionCount = ReadUInt16(packet, QuestionCountOffset);
        var answerCount = ReadUInt16(packet, AnswerCountOffset);
        var authorityCount = ReadUInt16(packet, AuthorityCountOffset);
        var additionalCount = ReadUInt16(packet, AdditionalCountOffset);
        var offset = HeaderLength;

        for (var index = 0; index < questionCount; index++)
        {
            _ = ReadName(packet, ref offset);
            EnsureAvailable(packet, offset, QuestionFooterLength);
            offset += QuestionFooterLength;
        }

        var records = new List<DnsRecord>();
        var recordCount = answerCount + authorityCount + additionalCount;
        for (var index = 0; index < recordCount; index++)
        {
            var name = ReadName(packet, ref offset);
            EnsureAvailable(packet, offset, RecordHeaderLength);
            var type = (DnsRecordType)ReadUInt16(packet, offset);
            var dataLength = ReadUInt16(packet, offset + RecordDataLengthOffset);
            offset += RecordHeaderLength;
            EnsureAvailable(packet, offset, dataLength);

            var record = ParseRecord(packet, name, type, offset, dataLength);
            if (record is not null)
            {
                records.Add(record);
            }

            offset += dataLength;
        }

        return records;
    }

    private static DnsRecord? ParseRecord(
        ReadOnlySpan<byte> packet,
        string name,
        DnsRecordType type,
        int dataOffset,
        int dataLength)
    {
        switch (type)
        {
            case DnsRecordType.Ptr:
            {
                var offset = dataOffset;
                return new PtrRecord(name, ReadName(packet, ref offset));
            }
            case DnsRecordType.Srv when dataLength >= SrvFixedFieldsLength:
            {
                var port = ReadUInt16(packet, dataOffset + SrvPortOffset);
                var offset = dataOffset + SrvFixedFieldsLength;
                return new SrvRecord(name, port, ReadName(packet, ref offset));
            }
            case DnsRecordType.Txt:
                return new TxtRecord(name, ParseTxt(packet.Slice(dataOffset, dataLength)));
            case DnsRecordType.A when dataLength == Ipv4AddressLength:
                return new AddressRecord(name, type, new IPAddress(packet.Slice(dataOffset, dataLength)));
            case DnsRecordType.Aaaa when dataLength == Ipv6AddressLength:
                return new AddressRecord(name, type, new IPAddress(packet.Slice(dataOffset, dataLength)));
            default:
                return null;
        }
    }

    private static IReadOnlyDictionary<string, string> ParseTxt(ReadOnlySpan<byte> data)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var offset = 0;

        while (offset < data.Length)
        {
            var length = data[offset++];
            if (length == EmptyTxtEntryLength)
            {
                continue;
            }

            if (offset + length > data.Length)
            {
                throw new InvalidDataException("DNS TXT entry exceeds its record length.");
            }

            var entry = Encoding.UTF8.GetString(data.Slice(offset, length));
            offset += length;
            var separator = entry.IndexOf('=');
            var key = separator >= 0 ? entry[..separator] : entry;
            var value = separator >= 0 ? entry[(separator + TxtKeyValueSeparatorLength)..] : string.Empty;
            values[key] = value;
        }

        return values;
    }

    internal static string ReadName(ReadOnlySpan<byte> packet, ref int offset)
    {
        var labels = new List<string>();
        var current = offset;
        var jumped = false;
        var pointerCount = 0;

        while (true)
        {
            EnsureAvailable(packet, current, LengthOctetSize);
            var length = packet[current++];
            if (length == NameTerminator)
            {
                if (!jumped)
                {
                    offset = current;
                }

                return string.Join('.', labels);
            }

            if ((length & CompressionMarkerMask) == CompressionMarkerMask)
            {
                EnsureAvailable(packet, current, LengthOctetSize);
                var pointer = ((length & CompressionPointerValueMask) << CompressionPointerShift) | packet[current++];
                if (!jumped)
                {
                    offset = current;
                    jumped = true;
                }

                if (pointer >= packet.Length || ++pointerCount > MaximumCompressionPointers)
                {
                    throw new InvalidDataException("DNS name contains an invalid compression pointer.");
                }

                current = pointer;
                continue;
            }

            if ((length & CompressionMarkerMask) != 0 || length > MaximumLabelLength)
            {
                throw new InvalidDataException("DNS name contains an invalid label.");
            }

            EnsureAvailable(packet, current, length);
            labels.Add(Encoding.UTF8.GetString(packet.Slice(current, length)));
            current += length;
            if (!jumped)
            {
                offset = current;
            }
        }
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> packet, int offset)
    {
        EnsureAvailable(packet, offset, UInt16Length);
        return BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(offset, UInt16Length));
    }

    private static void EnsureAvailable(ReadOnlySpan<byte> packet, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > packet.Length - length)
        {
            throw new InvalidDataException("DNS packet ended unexpectedly.");
        }
    }
}
