# ArcStream

ArcStream is an experimental open-source Windows CLI for sending system audio to
an AirPlay 2 speaker, initially a first-generation Sonos Arc.

The current bootstrap implements LAN discovery and endpoint probing. It does not
stream audio yet. That boundary is deliberate: discovery, device compatibility,
pairing, timing, and sustained playback are being validated before a desktop UI
or broad device support is added.

## Commands

```powershell
dotnet run --project src/ArcStream.Cli -- list
dotnet run --project src/ArcStream.Cli -- probe "Living Room"
dotnet run --project src/ArcStream.Cli -- inspect "Living Room"
dotnet run --project src/ArcStream.Cli -- capture "Living Room"
```

- `list` discovers `_airplay._tcp` and `_raop._tcp` services for three seconds.
- `probe` resolves a device by name or ID and tests its advertised TCP endpoint.
- `inspect` emits a timestamped JSON snapshot containing every advertised TXT
  property and a TCP probe result. Save this output when comparing Arc firmware
  or sender compatibility experiments.
- `capture` currently reports the remaining implementation milestone.

## Requirements

- .NET 8 SDK
- Windows 10 or later for the future WASAPI capture path
- Speaker and computer on the same multicast-enabled LAN

Build and test:

```powershell
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

See [PLAN.md](PLAN.md) for scope, milestones, and go/no-go criteria.

## Project status

This is pre-alpha research software. AirPlay 2 is not a publicly documented
sender protocol, and compatibility may change with speaker firmware.
