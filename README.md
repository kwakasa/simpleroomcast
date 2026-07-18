# AP2Win

AP2Win is an experimental open-source AirPlay 2 audio sender for Windows,
initially targeting a first-generation Sonos Arc.

The current bootstrap implements LAN discovery and endpoint probing. It does not
stream audio yet. That boundary is deliberate: discovery, device compatibility,
pairing, timing, and sustained playback are being validated before a desktop UI
or broad device support is added.

## Commands

```powershell
dotnet run --project src/AP2Win.Cli -- list
dotnet run --project src/AP2Win.Cli -- probe "Living Room"
dotnet run --project src/AP2Win.Cli -- inspect "Living Room"
dotnet run --project src/AP2Win.Cli -- capture "Living Room"
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

Development is supported on macOS for the CLI, discovery, protocol, encoding,
and unit-test layers. WASAPI capture and end-to-end playback must be validated
on Windows.

On an Apple silicon Mac, install and activate the Homebrew .NET 8 SDK:

```bash
brew install dotnet@8
export DOTNET_ROOT="/opt/homebrew/opt/dotnet@8/libexec"
export PATH="/opt/homebrew/opt/dotnet@8/bin:$PATH"
```

Add the two `export` lines to `~/.zshrc` if you want them applied to future
terminal sessions. The repository's `global.json` accepts compatible .NET 8
feature bands while preventing an accidental upgrade to a later major version.

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
