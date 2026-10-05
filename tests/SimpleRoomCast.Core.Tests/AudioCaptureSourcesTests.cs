using SimpleRoomCast.Core.Audio;
using Xunit;

namespace SimpleRoomCast.Core.Tests;

public sealed class AudioCaptureSourcesTests
{
    [Fact]
    public void Default_IsVirtualCable()
    {
        Assert.Equal(AudioCaptureSource.VirtualCable, AudioCaptureSources.Default);
    }

    [Theory]
    [InlineData("vb-cable", AudioCaptureSource.VirtualCable)]
    [InlineData("VB-CABLE", AudioCaptureSource.VirtualCable)]
    [InlineData("loopback", AudioCaptureSource.DirectLoopback)]
    public void TryParse_RecognizesSupportedOptions(string value, AudioCaptureSource expected)
    {
        var parsed = AudioCaptureSources.TryParse(value, out var source);

        Assert.True(parsed);
        Assert.Equal(expected, source);
    }

    [Fact]
    public void FindVirtualCableOutput_SelectsRecordingSide()
    {
        AudioCaptureEndpoint[] endpoints =
        [
            new("input", "CABLE Input (VB-Audio Virtual Cable)"),
            new("microphone", "Built-in Microphone"),
            new("output", "CABLE Output (VB-Audio Virtual Cable)")
        ];

        var endpoint = AudioCaptureSources.FindVirtualCableOutput(endpoints);

        Assert.NotNull(endpoint);
        Assert.Equal("output", endpoint.Id);
    }

    [Fact]
    public void FindVirtualCableOutput_ReturnsNullWhenDriverIsMissing()
    {
        AudioCaptureEndpoint[] endpoints =
        [
            new("microphone", "Built-in Microphone")
        ];

        Assert.Null(AudioCaptureSources.FindVirtualCableOutput(endpoints));
    }
}
