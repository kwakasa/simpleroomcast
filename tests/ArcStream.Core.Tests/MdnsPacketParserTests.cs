using System.Buffers.Binary;
using System.Net;
using System.Text;
using ArcStream.Core.Discovery;
using Xunit;

namespace ArcStream.Core.Tests;

public sealed class MdnsPacketParserTests
{
    [Fact]
    public void ReadName_ResolvesCompressionPointer()
    {
        byte[] packet =
        [
            5, (byte)'l', (byte)'o', (byte)'c', (byte)'a', (byte)'l', 0,
            4, (byte)'t', (byte)'e', (byte)'s', (byte)'t', 0xc0, 0x00
        ];
        var offset = 7;

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
        Assert.Equal(7000, service.Port);
        Assert.Equal("Sonos-ABC.local", service.Target);

        var text = Assert.Single(records.OfType<TxtRecord>());
        Assert.Equal("Sonos", text.Values["manufacturer"]);
        Assert.Equal("Arc", text.Values["model"]);

        var address = Assert.Single(records.OfType<AddressRecord>());
        Assert.Equal(IPAddress.Parse("192.168.1.10"), address.Address);
    }

    [Fact]
    public void Parse_RejectsTruncatedPacket()
    {
        Assert.Throws<InvalidDataException>(() => MdnsPacketParser.Parse(new byte[11]));
    }

    private static byte[] BuildAirPlayResponse()
    {
        using var stream = new MemoryStream();
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0x8400);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 4);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0);

        WriteRecord(stream, "_airplay._tcp.local", 12, data =>
            WriteName(data, "Living Room._airplay._tcp.local"));
        WriteRecord(stream, "Living Room._airplay._tcp.local", 33, data =>
        {
            WriteUInt16(data, 0);
            WriteUInt16(data, 0);
            WriteUInt16(data, 7000);
            WriteName(data, "Sonos-ABC.local");
        });
        WriteRecord(stream, "Living Room._airplay._tcp.local", 16, data =>
        {
            WriteTxt(data, "manufacturer=Sonos");
            WriteTxt(data, "model=Arc");
        });
        WriteRecord(stream, "Sonos-ABC.local", 1, data =>
            data.Write([192, 168, 1, 10]));

        return stream.ToArray();
    }

    private static void WriteRecord(Stream stream, string name, ushort type, Action<MemoryStream> writeData)
    {
        WriteName(stream, name);
        WriteUInt16(stream, type);
        WriteUInt16(stream, 0x8001);
        WriteUInt32(stream, 120);
        using var data = new MemoryStream();
        writeData(data);
        WriteUInt16(stream, checked((ushort)data.Length));
        data.Position = 0;
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
            var bytes = Encoding.UTF8.GetBytes(label);
            stream.WriteByte(checked((byte)bytes.Length));
            stream.Write(bytes);
        }

        stream.WriteByte(0);
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }
}
