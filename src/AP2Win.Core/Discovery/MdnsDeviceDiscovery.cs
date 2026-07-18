using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AP2Win.Core.Discovery;

public sealed class MdnsDeviceDiscovery : IDeviceDiscovery
{
    private const int MdnsPort = 5353;
    private const int AnyAvailablePort = 0;
    private const int DnsHeaderLength = 12;
    private const int DnsQuestionCountOffset = 4;
    private const ushort InternetClass = 1;
    private const int MaximumDnsLabelLength = 63;
    private const byte DnsNameTerminator = 0;
    private const int UInt16Length = 2;
    private const int PreferredServiceRank = 0;
    private const int FallbackServiceRank = 1;
    private static readonly IPAddress MulticastAddress = IPAddress.Parse("224.0.0.251");
    private static readonly string[] ServiceTypes = ["_airplay._tcp.local", "_raop._tcp.local"];

    public async Task<IReadOnlyList<AirPlayDevice>> DiscoverAsync(
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Discovery duration must be positive.");
        }

        using var socket = CreateSocket();
        await socket.SendToAsync(BuildQuery(), SocketFlags.None, new IPEndPoint(MulticastAddress, MdnsPort), cancellationToken);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(duration);
        var records = new List<DnsRecord>();
        var buffer = new byte[ushort.MaxValue];

        while (!deadline.IsCancellationRequested)
        {
            try
            {
                var result = await socket.ReceiveFromAsync(
                    buffer,
                    SocketFlags.None,
                    new IPEndPoint(IPAddress.Any, AnyAvailablePort),
                    deadline.Token);
                records.AddRange(MdnsPacketParser.Parse(buffer.AsSpan(0, result.ReceivedBytes)));
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (InvalidDataException)
            {
                // Ignore unrelated or malformed multicast traffic and continue discovery.
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return BuildDevices(records);
    }

    private static Socket CreateSocket()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        socket.Bind(new IPEndPoint(IPAddress.Any, MdnsPort));
        socket.SetSocketOption(
            SocketOptionLevel.IP,
            SocketOptionName.AddMembership,
            new MulticastOption(MulticastAddress));
        return socket;
    }

    private static byte[] BuildQuery()
    {
        using var stream = new MemoryStream();
        stream.Write(new byte[DnsQuestionCountOffset]);
        WriteUInt16(stream, ServiceTypes.Length);
        stream.Write(new byte[DnsHeaderLength - DnsQuestionCountOffset - UInt16Length]);

        foreach (var serviceType in ServiceTypes)
        {
            WriteName(stream, serviceType);
            WriteUInt16(stream, (ushort)DnsRecordType.Ptr);
            WriteUInt16(stream, InternetClass);
        }

        return stream.ToArray();
    }

    private static IReadOnlyList<AirPlayDevice> BuildDevices(IReadOnlyList<DnsRecord> records)
    {
        var ptrRecords = records.OfType<PtrRecord>()
            .Where(record => ServiceTypes.Contains(record.Name, StringComparer.OrdinalIgnoreCase))
            .Distinct()
            .ToArray();
        var srvRecords = records.OfType<SrvRecord>().ToLookup(record => record.Name, StringComparer.OrdinalIgnoreCase);
        var txtRecords = records.OfType<TxtRecord>().ToLookup(record => record.Name, StringComparer.OrdinalIgnoreCase);
        var addresses = records.OfType<AddressRecord>().ToLookup(record => record.Name, StringComparer.OrdinalIgnoreCase);
        var devices = new List<AirPlayDevice>();

        foreach (var ptr in ptrRecords)
        {
            var srv = srvRecords[ptr.Target].FirstOrDefault();
            if (srv is null)
            {
                continue;
            }

            var properties = txtRecords[ptr.Target]
                .SelectMany(record => record.Values)
                .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);
            var name = GetInstanceName(ptr.Target, ptr.Name);
            var id = GetDeviceId(properties, ptr.Target, name);
            devices.Add(new AirPlayDevice(
                id,
                name,
                ptr.Name,
                srv.Target,
                srv.Port,
                addresses[srv.Target].Select(record => record.Address).Distinct().ToArray(),
                properties));
        }

        return devices
            .GroupBy(device => device.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(device => device.ServiceType.StartsWith("_airplay", StringComparison.OrdinalIgnoreCase)
                    ? PreferredServiceRank
                    : FallbackServiceRank)
                .First())
            .OrderBy(device => device.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string GetInstanceName(string target, string serviceType)
    {
        var suffix = $".{serviceType}";
        var name = target.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? target[..^suffix.Length]
            : target;
        var at = name.IndexOf('@');
        return at >= 0 ? name[(at + 1)..] : name;
    }

    private static string GetDeviceId(
        IReadOnlyDictionary<string, string> properties,
        string target,
        string name)
    {
        foreach (var key in new[] { "deviceid", "pi", "pk" })
        {
            if (properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.IsNullOrWhiteSpace(name) ? target : name;
    }

    private static void WriteName(Stream stream, string name)
    {
        foreach (var label in name.Split('.'))
        {
            var bytes = Encoding.UTF8.GetBytes(label);
            if (bytes.Length is 0 or > MaximumDnsLabelLength)
            {
                throw new InvalidOperationException($"Invalid DNS label: {label}");
            }

            stream.WriteByte((byte)bytes.Length);
            stream.Write(bytes);
        }

        stream.WriteByte(DnsNameTerminator);
    }

    private static void WriteUInt16(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[UInt16Length];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, checked((ushort)value));
        stream.Write(bytes);
    }
}
