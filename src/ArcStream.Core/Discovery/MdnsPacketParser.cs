using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace ArcStream.Core.Discovery;

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

    public static IReadOnlyList<DnsRecord> Parse(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < HeaderLength)
        {
            throw new InvalidDataException("DNS packet is shorter than its header.");
        }

        var questionCount = ReadUInt16(packet, 4);
        var answerCount = ReadUInt16(packet, 6);
        var authorityCount = ReadUInt16(packet, 8);
        var additionalCount = ReadUInt16(packet, 10);
        var offset = HeaderLength;

        for (var index = 0; index < questionCount; index++)
        {
            _ = ReadName(packet, ref offset);
            EnsureAvailable(packet, offset, 4);
            offset += 4;
        }

        var records = new List<DnsRecord>();
        var recordCount = answerCount + authorityCount + additionalCount;
        for (var index = 0; index < recordCount; index++)
        {
            var name = ReadName(packet, ref offset);
            EnsureAvailable(packet, offset, 10);
            var type = (DnsRecordType)ReadUInt16(packet, offset);
            var dataLength = ReadUInt16(packet, offset + 8);
            offset += 10;
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
            case DnsRecordType.Srv when dataLength >= 6:
            {
                var port = ReadUInt16(packet, dataOffset + 4);
                var offset = dataOffset + 6;
                return new SrvRecord(name, port, ReadName(packet, ref offset));
            }
            case DnsRecordType.Txt:
                return new TxtRecord(name, ParseTxt(packet.Slice(dataOffset, dataLength)));
            case DnsRecordType.A when dataLength == 4:
                return new AddressRecord(name, type, new IPAddress(packet.Slice(dataOffset, dataLength)));
            case DnsRecordType.Aaaa when dataLength == 16:
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
            if (length == 0)
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
            var value = separator >= 0 ? entry[(separator + 1)..] : string.Empty;
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
            EnsureAvailable(packet, current, 1);
            var length = packet[current++];
            if (length == 0)
            {
                if (!jumped)
                {
                    offset = current;
                }

                return string.Join('.', labels);
            }

            if ((length & 0xc0) == 0xc0)
            {
                EnsureAvailable(packet, current, 1);
                var pointer = ((length & 0x3f) << 8) | packet[current++];
                if (!jumped)
                {
                    offset = current;
                    jumped = true;
                }

                if (pointer >= packet.Length || ++pointerCount > 32)
                {
                    throw new InvalidDataException("DNS name contains an invalid compression pointer.");
                }

                current = pointer;
                continue;
            }

            if ((length & 0xc0) != 0 || length > 63)
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
        EnsureAvailable(packet, offset, 2);
        return BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(offset, 2));
    }

    private static void EnsureAvailable(ReadOnlySpan<byte> packet, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > packet.Length - length)
        {
            throw new InvalidDataException("DNS packet ended unexpectedly.");
        }
    }
}
