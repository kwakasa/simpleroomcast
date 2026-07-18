# AP2Win MVP plan

## Outcome

Provide one dependable command that captures the Windows default playback device
and sends stereo audio over the local network to one first-generation Sonos Arc.

```text
WASAPI loopback -> PCM normalization -> ALAC -> AirPlay 2 -> Sonos Arc
```

The MVP is intended for music and casual media. It is not intended for gaming,
calls, lip-synced video, surround playback, or remote-network use.

## Scope

Included:

- Windows 10 and 11
- One selected AirPlay 2 receiver
- Default-output WASAPI loopback capture
- 48 kHz, 16-bit, stereo output
- Discovery, explicit start/stop, reconnect, and useful diagnostics
- CLI-first distribution

Excluded:

- Multi-room synchronization
- Per-application capture
- Metadata and artwork
- Video or screen mirroring
- AirPlay receiver functionality
- Virtual audio drivers
- DRM circumvention

## Milestones

### M0 - repository bootstrap

- [x] CLI command surface
- [x] Dependency-free mDNS discovery
- [x] TCP endpoint probe
- [x] Unit tests for DNS message parsing
- [ ] Verify build and tests on a machine with the .NET 8 SDK
- [ ] Run `list` and `probe` against the target Arc

Exit condition: the Arc is consistently discovered and its advertised endpoint
is reachable from Windows without Bonjour being installed.

### M1 - sender compatibility spike

- [ ] Establish a known-good AirPlay 2 session with current OwnTone
- [ ] Feed live PCM through OwnTone pipe input
- [ ] Record the Arc's mDNS TXT records and authentication behavior
- [ ] Measure startup delay and 30-minute stability
- [ ] Confirm stop, restart, and speaker power-cycle recovery

Exit condition: an open-source sender plays sustained live PCM on the exact Arc
without an Apple device participating.

### M2 - native Windows test tone

- [ ] Choose the sender-core strategy after M1 evidence
- [ ] Implement pairing and session setup
- [ ] Implement PTP/timing support needed by the Arc
- [ ] Packetize and send a generated ALAC test tone
- [ ] Add protocol transcript logging with secrets redacted

Exit condition: `ap2win play-test <speaker>` produces stable sound for five
minutes from a self-contained Windows process.

### M3 - live system audio

- [ ] Add WASAPI loopback capture
- [ ] Normalize sample format and channel layout
- [ ] Bound buffering and handle underruns
- [ ] Add clean cancellation and session teardown
- [ ] Run one-hour playback and reconnect tests

Exit condition: `ap2win capture <speaker>` streams the default Windows output
for one hour without unbounded memory growth or manual recovery.

### M4 - distributable MVP

- [ ] Add structured diagnostics and actionable errors
- [ ] Publish a self-contained x64 build
- [ ] Add a minimal installer and Windows Firewall guidance
- [ ] Test on clean Windows 10 and Windows 11 machines
- [ ] Document known latency and firmware compatibility

## Sender-core decision

Do not lock this decision before M1. The preferred order is:

1. Extract a narrow native sender library from OwnTone's GPLv2+ AirPlay output
   code and keep AP2Win GPLv2-compatible.
2. If Unix dependencies make extraction impractical, implement the observed
   narrow protocol path in managed .NET under a GPL-compatible license.
3. Do not ship WSL, Docker, or a Linux VM as the Windows MVP runtime.

## Quality gates

- No Apple software installation is required.
- No kernel driver or administrator privilege is required for normal playback.
- All network and protocol failures return a nonzero exit code.
- Logs never contain pairing secrets or reusable credentials.
- A firmware incompatibility is reported explicitly rather than as "no audio."

## Key risks

- AirPlay 2 sender behavior is reverse engineered and can change.
- PTP timing and multicast behavior may differ across Windows networks.
- The Arc may require pairing or timing behavior not exercised by older RAOP
  implementations.
- Expected buffering makes the MVP unsuitable as a low-latency game speaker.
- Reusing OwnTone code requires GPLv2-compatible distribution.
