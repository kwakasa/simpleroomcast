using SimpleRoomCast.Core;
using SimpleRoomCast.Core.Audio;
using SimpleRoomCast.Core.Discovery;
using System.Net.Sockets;
using System.Text.Json;

namespace SimpleRoomCast.Cli;

internal static class CliApplication
{
    private const int CommandIndex = 0;
    private const int SelectorIndex = 1;
    private const int CommandOnlyArgumentCount = 1;
    private const int CommandWithSelectorArgumentCount = 2;
    private const int CaptureWithSourceArgumentCount = 4;
    private const int CaptureSourceOptionIndex = 2;
    private const int CaptureSourceValueIndex = 3;
    private const string CaptureSourceOption = "--capture-source";
    private const int SuccessExitCode = 0;
    private const int GeneralFailureExitCode = 1;
    private const int InvalidArgumentsExitCode = 2;
    private const int NetworkFailureExitCode = 3;
    private const int NotImplementedExitCode = 4;
    private const int CancelledExitCode = 130;
    private static readonly TimeSpan DiscoveryDuration = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[CommandIndex]))
        {
            PrintHelp();
            return args.Length == 0 ? InvalidArgumentsExitCode : SuccessExitCode;
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            return args[CommandIndex].ToLowerInvariant() switch
            {
                "list" when args.Length == CommandOnlyArgumentCount => await ListAsync(cancellation.Token),
                "probe" when args.Length == CommandWithSelectorArgumentCount =>
                    await ProbeAsync(args[SelectorIndex], cancellation.Token),
                "inspect" when args.Length == CommandWithSelectorArgumentCount =>
                    await InspectAsync(args[SelectorIndex], cancellation.Token),
                "capture" when args.Length >= CommandWithSelectorArgumentCount => Capture(args),
                _ => InvalidArguments()
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return CancelledExitCode;
        }
        catch (SocketException exception)
        {
            Console.Error.WriteLine($"Network error: {exception.Message}");
            return NetworkFailureExitCode;
        }
    }

    private static async Task<int> ListAsync(CancellationToken cancellationToken)
    {
        var devices = await DiscoverAsync(cancellationToken);
        if (devices.Count == 0)
        {
            Console.WriteLine("No AirPlay devices found.");
            Console.WriteLine("Check that the speaker is awake and multicast is enabled on the LAN.");
            return GeneralFailureExitCode;
        }

        foreach (var device in devices)
        {
            Console.WriteLine($"{device.Name}  {device.Endpoint}");
            Console.WriteLine($"  id: {device.Id}");
            Console.WriteLine($"  service: {device.ServiceType}");
            PrintSelectedProperties(device);
        }

        return SuccessExitCode;
    }

    private static async Task<int> ProbeAsync(string selector, CancellationToken cancellationToken)
    {
        var devices = await DiscoverAsync(cancellationToken);
        var (device, errorCode) = SelectDevice(devices, selector);
        if (device is null)
        {
            return errorCode;
        }

        Console.WriteLine($"Probing {device.Name} at {device.Endpoint}...");
        var result = await new DeviceProbe().ProbeAsync(
            device,
            ProbeTimeout,
            cancellationToken);
        Console.WriteLine($"Reachable: {(result.Reachable ? "yes" : "no")}");
        Console.WriteLine($"Elapsed: {result.Elapsed.TotalMilliseconds:0} ms");
        Console.WriteLine($"Detail: {result.Detail}");
        PrintSelectedProperties(device);
        return result.Reachable ? SuccessExitCode : GeneralFailureExitCode;
    }

    private static async Task<int> InspectAsync(string selector, CancellationToken cancellationToken)
    {
        var devices = await DiscoverAsync(cancellationToken);
        var (device, errorCode) = SelectDevice(devices, selector);
        if (device is null)
        {
            return errorCode;
        }

        var probe = await new DeviceProbe().ProbeAsync(device, ProbeTimeout, cancellationToken);
        var snapshot = new
        {
            capturedAtUtc = DateTimeOffset.UtcNow,
            device = new
            {
                device.Id,
                device.Name,
                device.ServiceType,
                device.HostName,
                device.Port,
                addresses = device.Addresses.Select(address => address.ToString()).ToArray(),
                properties = device.Properties
                    .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            },
            probe = new
            {
                probe.Reachable,
                elapsedMilliseconds = Math.Round(probe.Elapsed.TotalMilliseconds),
                probe.Detail
            }
        };

        Console.WriteLine(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        return probe.Reachable ? SuccessExitCode : GeneralFailureExitCode;
    }

    private static (AirPlayDevice? Device, int ErrorCode) SelectDevice(
        IReadOnlyList<AirPlayDevice> devices,
        string selector)
    {
        var exactMatches = devices.Where(device =>
            device.Name.Equals(selector, StringComparison.OrdinalIgnoreCase) ||
            device.Id.Equals(selector, StringComparison.OrdinalIgnoreCase)).ToArray();
        var matches = exactMatches.Length > 0
            ? exactMatches
            : devices.Where(device => device.Name.Contains(selector, StringComparison.OrdinalIgnoreCase)).ToArray();

        if (matches.Length == 0)
        {
            Console.Error.WriteLine($"No device matched '{selector}'. Run 'simpleroomcast list' to see available devices.");
            return (null, GeneralFailureExitCode);
        }

        if (matches.Length > 1)
        {
            Console.Error.WriteLine($"'{selector}' matched multiple devices; use the exact device ID:");
            foreach (var match in matches)
            {
                Console.Error.WriteLine($"  {match.Name}: {match.Id}");
            }

            return (null, InvalidArgumentsExitCode);
        }

        return (matches.First(), SuccessExitCode);
    }

    private static int Capture(string[] args)
    {
        var source = AudioCaptureSources.Default;
        if (args.Length == CaptureWithSourceArgumentCount &&
            args[CaptureSourceOptionIndex].Equals(CaptureSourceOption, StringComparison.OrdinalIgnoreCase) &&
            AudioCaptureSources.TryParse(args[CaptureSourceValueIndex], out var requestedSource))
        {
            source = requestedSource;
        }
        else if (args.Length != CommandWithSelectorArgumentCount)
        {
            return InvalidArguments();
        }

        var selector = args[SelectorIndex];
        Console.Error.WriteLine($"Capture for '{selector}' is not implemented yet.");
        Console.Error.WriteLine($"Selected capture source: {FormatCaptureSource(source)}.");
        Console.Error.WriteLine("Next milestone: implement Sonos discovery and HTTP test-tone playback, then WASAPI capture.");
        return NotImplementedExitCode;
    }

    private static string FormatCaptureSource(AudioCaptureSource source) => source switch
    {
        AudioCaptureSource.VirtualCable => AudioCaptureSources.VirtualCableOption,
        AudioCaptureSource.DirectLoopback => AudioCaptureSources.DirectLoopbackOption,
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown audio capture source.")
    };

    private static Task<IReadOnlyList<AirPlayDevice>> DiscoverAsync(CancellationToken cancellationToken)
    {
        IDeviceDiscovery discovery = new MdnsDeviceDiscovery();
        return discovery.DiscoverAsync(DiscoveryDuration, cancellationToken);
    }

    private static void PrintSelectedProperties(AirPlayDevice device)
    {
        var selected = new[] { "manufacturer", "model", "srcvers", "features", "flags", "protovers" };
        foreach (var key in selected)
        {
            if (device.Properties.TryGetValue(key, out var value))
            {
                Console.WriteLine($"  {key}: {value}");
            }
        }
    }

    private static bool IsHelp(string value) => value is "-h" or "--help" or "help";

    private static int InvalidArguments()
    {
        Console.Error.WriteLine("Invalid arguments.");
        PrintHelp();
        return InvalidArgumentsExitCode;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("SimpleRoomCast - stream Windows audio to Sonos. Simply.");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  simpleroomcast list");
        Console.WriteLine("  simpleroomcast probe <speaker-name-or-id>");
        Console.WriteLine("  simpleroomcast inspect <speaker-name-or-id>");
        Console.WriteLine(
            "  simpleroomcast capture <speaker-name-or-id> [--capture-source vb-cable|loopback]");
        Console.WriteLine();
        Console.WriteLine("Capture defaults to vb-cable for an unprocessed signal path.");
    }
}
