namespace SimpleRoomCast.Core.Audio;

public enum AudioCaptureSource
{
    VirtualCable
}

public sealed record AudioCaptureEndpoint(string Id, string Name);

public static class AudioCaptureSources
{
    public const string VirtualCableOption = "vb-cable";

    public static AudioCaptureSource Default => AudioCaptureSource.VirtualCable;

    public static bool TryParse(string value, out AudioCaptureSource source)
    {
        if (value.Equals(VirtualCableOption, StringComparison.OrdinalIgnoreCase))
        {
            source = AudioCaptureSource.VirtualCable;
            return true;
        }

        source = default;
        return false;
    }

    public static AudioCaptureEndpoint? FindVirtualCableOutput(
        IEnumerable<AudioCaptureEndpoint> recordingEndpoints)
    {
        ArgumentNullException.ThrowIfNull(recordingEndpoints);

        return recordingEndpoints.FirstOrDefault(endpoint =>
            endpoint.Name.Contains("CABLE Output", StringComparison.OrdinalIgnoreCase) &&
            endpoint.Name.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase));
    }
}
