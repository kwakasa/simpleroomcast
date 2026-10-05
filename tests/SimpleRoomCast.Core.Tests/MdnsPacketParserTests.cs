using System.Buffers.Binary;
using System.Net;
using System.Text;
using SimpleRoomCast.Core.Discovery;
using Xunit;

namespace SimpleRoomCast.Core.Tests;

public sealed class MdnsPacketParserTests
{
    private const int DnsHeaderLength = 12;
    private const ushort ResponseWithAuthoritativeAnswerFlags = 0x8400;
    private const ushort CacheFlushInternetClass = 0x8001;
    private const uint FixtureRecordTimeToLiveSeconds = 120;
    private const ushort FixtureAirPlayPort = 7000;
    private const ushort DefaultSrvPriority = 0;
    private const ushort DefaultSrvWeight = 0;
    private const byte CompressionPointerMarker = 0xc0;
    private const byte NameTerminator = 0;
    private const int RootLabelOffset = 0;
    private const int UInt16Length = 2;
    private const int UInt32Length = 4;
    private const ushort AirPlayResponseRecordCount = 4;
    private const long StreamStartPosition = 0;
    private static readonly IPAddress FixtureAddress = IPAddress.Parse("192.168.1.10");

    [Fact]
    public void ReadName_ResolvesCompressionPointer()
    {
        using var stream = new MemoryStream();
        WriteName(stream, "local");
        var offset = checked((int)stream.Position);
        WriteLabel(stream, "test");
        stream.WriteByte(CompressionPointerMarker);
        stream.WriteByte(RootLabelOffset);
        var packet = stream.ToArray();

        var name = MdnsPacketParser.ReadName(packet, ref offset);

        Assert.Equal("test.local", name);
        Assert.Equal(packet.Length, offset);
    }

    [Fact]
    public void Parse_ReturnsAirPlayServiceRecords()
    {
        var packet = BuildAirPlayResponse();

        var records = MdnsPacketParser.Parse(packet);

        var pointer = Assert.Single(records.OfType<PtrRecord>());
        Assert.Equal("_airplay._tcp.local", pointer.Name);
        Assert.Equal("Living Room._airplay._tcp.local", pointer.Target);

        var service = Assert.Single(records.OfType<SrvRecord>());
        Assert.Equal((int)FixtureAirPlayPort, service.Port);
        Assert.Equal("Sonos-ABC.local", service.Target);

        var text = Assert.Single(records.OfType<TxtRecord>());
        Assert.Equal("Sonos", text.Values["manufacturer"]);
        Assert.Equal("Arc", text.Values["model"]);

        var address = Assert.Single(records.OfType<AddressRecord>());
        Assert.Equal(FixtureAddress, address.Address);
    }

    [Fact]
    public void Parse_RejectsTruncatedPacket()
    {
        Assert.Throws<InvalidDataException>(() =>
            MdnsPacketParser.Parse(new byte[DnsHeaderLength - 1]));
    }

    private static byte[] BuildAirPlayResponse()
    {
        using var stream = new MemoryStream();
        WriteUInt16(stream, ushort.MinValue);
        WriteUInt16(stream, ResponseWithAuthoritativeAnswerFlags);
        WriteUInt16(stream, ushort.MinValue);
        WriteUInt16(stream, AirPlayResponseRecordCount);
        WriteUInt16(stream, ushort.MinValue);
        WriteUInt16(stream, ushort.MinValue);

        WriteRecord(stream, "_airplay._tcp.local", DnsRecordType.Ptr, data =>
            WriteName(data, "Living Room._airplay._tcp.local"));
        WriteRecord(stream, "Living Room._airplay._tcp.local", DnsRecordType.Srv, data =>
        {
            WriteUInt16(data, DefaultSrvPriority);
            WriteUInt16(data, DefaultSrvWeight);
            WriteUInt16(data, FixtureAirPlayPort);
            WriteName(data, "Sonos-ABC.local");
        });
        WriteRecord(stream, "Living Room._airplay._tcp.local", DnsRecordType.Txt, data =>
        {
            WriteTxt(data, "manufacturer=Sonos");
            WriteTxt(data, "model=Arc");
        });
        WriteRecord(stream, "Sonos-ABC.local", DnsRecordType.A, data =>
            data.Write(FixtureAddress.GetAddressBytes()));

        return stream.ToArray();
    }

    private static void WriteRecord(
        Stream stream,
        string name,
        DnsRecordType type,
        Action<MemoryStream> writeData)
    {
        WriteName(stream, name);
        WriteUInt16(stream, (ushort)type);
        WriteUInt16(stream, CacheFlushInternetClass);
        WriteUInt32(stream, FixtureRecordTimeToLiveSeconds);
        using var data = new MemoryStream();
        writeData(data);
        WriteUInt16(stream, checked((ushort)data.Length));
        data.Position = StreamStartPosition;
        data.CopyTo(stream);
    }

    private static void WriteTxt(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        stream.WriteByte(checked((byte)bytes.Length));
        stream.Write(bytes);
    }

    private static void WriteName(Stream stream, string name)
    {
        foreach (var label in name.Split('.'))
        {
            WriteLabel(stream, label);
        }

        stream.WriteByte(NameTerminator);
    }

    private static void WriteLabel(Stream stream, string label)
    {
        var bytes = Encoding.UTF8.GetBytes(label);
        stream.WriteByte(checked((byte)bytes.Length));
        stream.Write(bytes);
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[UInt16Length];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[UInt32Length];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }
}
