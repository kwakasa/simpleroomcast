# SimpleRoomCast

Stream Windows audio to Sonos. Simply.

SimpleRoomCast is an experimental Windows utility aiming to provide TuneBlade's
convenience for Sonos: a compact tray app, remembered room selection, start/stop
and volume controls, and reliable reconnection.

The intended transport uses local Sonos control and an HTTP audio stream that
the speaker fetches from the PC. AirPlay is not required. The next step is to
evaluate reuse of [RoomRelay](https://github.com/guicn555/RoomRelay)'s working core.

## Current status

This is a renamed prototype, not a working TuneBlade replacement yet. It has a
CLI, legacy AirPlay mDNS discovery, TCP probing, and capture-source selection.
Sonos discovery/control, audio capture/streaming, and tray UI remain planned.

```powershell
dotnet run --project src/SimpleRoomCast.Cli -- list
dotnet run --project src/SimpleRoomCast.Cli -- probe "Living Room"
dotnet run --project src/SimpleRoomCast.Cli -- inspect "Living Room"
dotnet run --project src/SimpleRoomCast.Cli -- capture "Living Room"
dotnet run --project src/SimpleRoomCast.Cli -- capture "Living Room" --capture-source loopback
```

- `list` currently discovers `_airplay._tcp` and `_raop._tcp` services.
- `probe` tests the discovered device's TCP endpoint.
- `inspect` emits a JSON discovery/TXT snapshot and probe result.
- `capture` reports that streaming is unimplemented. It accepts
  `--capture-source vb-cable|loopback`, defaulting to `vb-cable`.

## Planned audio routing

```text
Windows output picker: CABLE Input (VB-Audio Virtual Cable)
    -> CABLE Output recording endpoint
    -> SimpleRoomCast
    -> local HTTP stream
    -> selected Sonos room
```

Users can select CABLE Input as the system output or route individual apps to it
through the Windows volume mixer. SimpleRoomCast will capture CABLE Output.
Physical speakers do not automatically receive audio routed into the cable.

VB-CABLE is separately installed donationware. Installation requires administrator
access and a restart. SimpleRoomCast will not bundle the driver or change the
default Windows output automatically. Direct WASAPI loopback remains a planned
fallback for setups without VB-CABLE.

Virtual-cable capture avoids unwanted physical-endpoint processing but does not
remove Sonos buffering. Music and background audio are the primary use cases;
gaming, calls, and precise video lip-sync are outside the MVP's latency promise.

## Development

The current prototype uses .NET 8. macOS supports portable CLI/protocol/unit-test
development. Windows is required for WASAPI, Media Foundation, GUI, and speaker
integration testing.

On Apple silicon:

```bash
brew install dotnet@8
export DOTNET_ROOT="/opt/homebrew/opt/dotnet@8/libexec"
export PATH="/opt/homebrew/opt/dotnet@8/bin:$PATH"
```

Add the exports to `~/.zshrc` for future sessions. `global.json` selects compatible
.NET 8 feature bands.

```bash
dotnet restore SimpleRoomCast.sln
dotnet build SimpleRoomCast.sln --configuration Release
dotnet test SimpleRoomCast.sln --configuration Release
```

See [PLAN.md](PLAN.md) for milestones and the core-reuse decision. The existing
GPLv2-or-later license is preserved; no RoomRelay source has been imported yet.
SimpleRoomCast is independent and is not affiliated with Sonos or TuneBlade.
