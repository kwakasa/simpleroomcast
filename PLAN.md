# SimpleRoomCast MVP plan

## Outcome

A small Windows tray application that sends system audio to a selected Sonos
room with the convenience of TuneBlade.

```text
Windows audio -> VB-CABLE -> WASAPI recording capture -> PCM normalization -> AAC/PCM HTTP stream -> Sonos
```

The user has confirmed RoomRelay works with the target speaker. Evaluate reuse
of its core before building another implementation. AirPlay is no longer a
requirement. Music, podcasts, and background audio are the target use cases;
Sonos buffering limits gaming, calls, and video lip-sync.

## Scope

Included:

- Windows 10 and 11; initial testing on a first-generation Sonos Arc
- One selected Sonos room or existing group via its coordinator
- VB-CABLE as the required and only capture source
- User-controlled Windows output routing, including app routing to VB-CABLE
- Stereo audio; 48 kHz / 16-bit normalization where required by the stream format
- Discovery, start/stop, Sonos volume, remembered settings, and reconnect
- Compact tray UI with optional login startup and automatic connection
- CLI diagnostics and self-contained Windows distribution

Excluded from MVP:

- AirPlay and non-Sonos receivers
- Custom drivers or bundled VB-CABLE installation
- Direct output-device loopback or microphone capture
- Independent multi-room clock synchronization or automatic group creation
- Dedicated per-process capture, DSP/EQ, artwork, and metadata editing
- Remote-network access and DRM circumvention

## Milestones

### M0 - renamed prototype

- [x] Rename solution, projects, namespaces, and CLI to SimpleRoomCast
- [x] CLI command surface restricted to VB-CABLE capture
- [x] Legacy AirPlay mDNS discovery, endpoint probe, and DNS parser tests
- [x] Verify predecessor build and tests with .NET 8 on macOS
- [x] Validate renamed build and tests on macOS

Existing `list`, `probe`, and `inspect` still use AirPlay service discovery.
They are prototype diagnostics; Sonos SSDP discovery remains to be implemented.

### M1 - Sonos core and test tone

- [ ] Inspect current RoomRelay core interfaces, dependencies, and license notices
- [ ] Choose a fork, reusable core, or narrow adaptation with attribution
- [ ] Discover Sonos through SSDP and resolve room/group coordinators
- [ ] Host a bounded local HTTP stream reachable by the selected speaker
- [ ] Implement AVTransport URI/play/stop and RenderingControl volume commands
- [ ] Play a test tone on the Arc and measure startup delay and stop/restart behavior

Exit condition: `simpleroomcast play-test <room>` plays stable audio from Windows
and stops cleanly. Network and encoding work stay in the user-mode process.

### M2 - live Windows capture

- [ ] Enumerate active render/recording endpoints with stable device IDs
- [ ] Capture VB-CABLE Output using WASAPI recording capture
- [ ] Require an active cable endpoint; show actionable installation/enablement guidance when unavailable
- [ ] Offer explicit device selection for ambiguous or renamed cable endpoints
- [ ] Normalize format/channel layout and encode AAC using Windows APIs
- [ ] Evaluate PCM as an optional stream format
- [ ] Bound queues, handle slow readers, and insert idle silence as needed
- [ ] Handle cancellation, endpoint changes, network loss, and speaker restarts
- [ ] Validate one-hour playback and reconnect on Windows

Exit condition: `simpleroomcast capture <room>` provides sustained audio without
unbounded memory growth or manual recovery.

### M3 - everyday tray experience

- [ ] Compact room selector, VB-CABLE status, start/stop, and Sonos volume
- [ ] Tray controls, clear streaming status, and actionable errors
- [ ] Persist room and cable endpoint preferences
- [ ] Optional login startup and automatic connection
- [ ] Reconnect with bounded backoff and visible status
- [ ] Explain CABLE Input -> CABLE Output setup

Exit condition: streaming is operated from the tray without repeatedly opening
advanced settings. Windows output selection remains under user control.

### M4 - distribution

- [ ] Self-contained x64 package and minimal installer
- [ ] Private-network Firewall guidance for the HTTP server
- [ ] Diagnostics with sensitive identifiers redacted where appropriate
- [ ] Clean-machine Windows tests and measured latency/compatibility notes
- [ ] Third-party license audit and required notices

## Architecture and licensing

Keep application orchestration in .NET. Prefer RoomRelay's proven Sonos transport
and native Windows capture/encoding if practical to reuse. Choose a GUI framework
after understanding the selected core's dependencies. Portable tests can run on
macOS; WASAPI, Media Foundation, UI, and speaker integration require Windows.

The existing GPLv2-or-later LICENSE is preserved. Renaming and changing transport
do not relicense code. Imported source must retain attribution and undergo a
license-compatibility check before incorporation.

## Quality gates and risks

- Normal operation requires no administrator rights; separate VB-CABLE installation does.
- Never silently change Windows output or fall back to physical loopback.
- Bind HTTP to the interface used to reach Sonos and document trusted-LAN use.
- CLI network/protocol failures return nonzero exit codes.
- Measure Sonos buffering; do not promise zero latency.
- Local Sonos interfaces and format support may vary with firmware and model.
- Recovery must handle stale topology, disabled endpoints, and Firewall blocks.
