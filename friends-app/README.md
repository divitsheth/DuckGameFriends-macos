# Duck Game Friends 1.0.0

Private 2–4 player Duck Game on Apple Silicon Macs running macOS 15 or newer.

Deliverable: `~/Downloads/DuckGameFriends-1.0.0-AppleSilicon.dmg`.
Installed app: `/Applications/Duck Game Friends.app`.

The image includes the game, Mono 6.14.1, five favorite-map remixes, a native Host/Join launcher, the official signed Tailscale 1.102.4 installer, and Quick Start.html. Each player needs their own legitimate game copy and a Tailscale account connected to the same personal network. The host sends the generated room code privately; it includes the room password. No router forwarding is needed. The host must keep the game running. Join before starting a round; mid-game joining is disabled.

## Verification

- Four independent real game processes using the bundled Mono runtime connected in one lobby and entered a GameLevel with four connected players and four ducks. Repeated after Developer ID signing; all remained connected for a further ten seconds.
- An incorrect room secret was rejected: the joining client reached ConnectionError with zero connections while the host remained alone.
- Actual native launcher opened and launched local gameplay from its writable per-user game copy.
- All five installed and bundled custom map files match the previously play-tested remix source files by SHA-256.
- Disk image checksum, read-only mount, included installer checksum, app deep/strict signature, and mounted image contents verified.
- The final app was installed from the disk image and its native launcher opened successfully.
- Native library dependencies were relocated into the app. Minimum macOS versions were checked against macOS 15. A sample of the signed running process showed no Homebrew library paths.

Evidence is retained under `verification/`.

## What remains for the first remote session

No second physical Mac or internet-latency session has been tested. Use a personal Tailscale network, or obtain your network administrator's approval. Invite friends to the same network, then test their connection to the host before playing.

The custom game app is Developer ID signed but **not Apple-notarized**. Apple Gatekeeper reports `Unnotarized Developer ID`. Follow the first-launch Open Anyway steps in the included guide; never disable Gatekeeper globally. The included official Tailscale installer is notarized.

## Implementation

`FriendsBootstrap.cs` enters the original LAN host/join flow with four slots and a generated room password. `Launcher.swift` validates room codes, checks Tailscale state, installs maps without overwriting existing files, and starts the bundled Mono engine. Logs are in `~/Library/Logs/DuckGameFriends/game.log`.

Two upstream macOS LAN bugs are fixed in source and applied to the pinned release using `PatchRelease.cs`: Mono does not implement UDP listener enumeration, so clients bind an OS-assigned ephemeral port; the Steam-only mid-game metadata helper is skipped for LAN sessions. The installed original DuckGameRebuilt app is unchanged.

`build_bundle.py` prepares the bundle from the installed v1.4.7-macos.8 release, the official Mono Sonoma bottle, and official Homebrew Sequoia dependency bottles. Build inputs are currently staged under `/tmp/duckfriends`; this is a local packaging workflow, not portable CI. `package_dmg.py` signs with the local Developer ID and produces the image. Keep build output and runtime caches out of Git.
