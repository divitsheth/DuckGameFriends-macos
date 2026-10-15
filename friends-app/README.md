# Duck Game Friends

Private 2–8 player Duck Game on Apple Silicon Macs running macOS 15 or newer, Intel Macs running macOS 11 or newer, and Windows 10 or 11 PCs.

Deliverables: `~/Downloads/DuckGameFriends-<version>.dmg` (releases up to 1.3.0 were named `-AppleSilicon.dmg`) and `~/Downloads/DuckGameFriends-<version>-Windows.zip`.
Installed app: `/Applications/Duck Game Friends.app`.

The image includes the game, Mono 6.14.1 for Apple Silicon and Mono 6.12 for Intel, five favorite-map remixes, an in-game Host/Join menu, and Quick Start.html. Each player needs their own legitimate game copy. The host sends an eight-character room code; it is both the relay room and the lobby password. No VPN, accounts, or router forwarding are needed. The host must keep the game running. Join before starting a round; mid-game joining is disabled.

## Online play

Duck Game's LAN mode connects every player to every other player, not only to the host. Rather than requiring a shared VPN, every game sends its packets to the room relay, which forwards them by slot.

- `relay/relay.js` is a dependency-free Node UDP server. A room has up to eight slots; slot 0 is the host, whose join message sets the size (rooms from 1.1 and 1.2, which don't send one, get four). It forwards packets between slots, answers the menu's room checks, and closes a room when the host goes quiet for 20 seconds. Rooms exist only in memory. Deployed on Fly.io as `duckfriends-relay` (`duckfriends-relay.fly.dev:7777`, dedicated IPv4 for UDP); deploy with `fly deploy` from `relay/`; test with `node --test` from `relay/`.
- `NCRelay.cs` subclasses the game's `NCBasic` LAN transport inside the game process. Each slot has a virtual endpoint (`10.77.0.1` host, `.2`–`.8` guests), so the addresses the host shares with guests resolve to relay slots on every Mac. The original game assembly is unchanged; `FriendsBootstrap.cs` installs the transport before hosting or joining.
- `FriendsMenu.cs` is the first screen, drawn with the game's own menu box, font, logo and duck and driven by keyboard or controller. It generates room codes, asks the relay whether a room exists and has space, copies the host's code and reads a copied code when joining, and pins the room code to the lobby. `Launcher.swift` only installs the runtime and starts the game in `menu` mode. `DUCK_FRIENDS_RELAY=host:port` overrides the relay for testing; `DUCK_FRIENDS_TEST_MENU=host|join` presses through the menu for automated runs.

## Intel Macs

One universal app serves both kinds of Mac. Homebrew no longer publishes Intel builds of Mono or most libraries, so `build_intel.py` makes the Intel half separately: Mono 6.12 (with libgdiplus) from Mono's official universal installer goes into `Runtime-x86_64`, and SDL2 2.32.10, FNA3D, FAudio and Theorafile are built from source for x86_64 at the same versions the Apple Silicon build ships. `update_bundle.py` adds that runtime, merges the four game libraries into universal files with `lipo`, and builds the launcher for both architectures; the launcher starts whichever runtime matches the Mac. Intel requires macOS 11; Apple Silicon still requires macOS 15 (`LSMinimumSystemVersionByArchitecture`).

    python3 build_intel.py
    python3 update_bundle.py "<previous release>/Duck Game Friends.app" 1.4.0 5
    python3 package_dmg.py

## Windows

`package_windows.py` turns the macOS build into a Windows zip. DuckGameRebuilt runs natively on Windows under .NET Framework 4.8, which Windows includes, so no runtime is bundled. The Friends bootstrap is compiled a second time as `Duck Game Friends.exe`: a 32-bit Windows program, because DuckGameRebuilt's Windows libraries are 32-bit, and compiled against .NET Framework 4.8 reference assemblies (from the Mono installer `build_intel.py` unpacks) so it calls nothing Windows lacks. The game folder's own Windows FNA3D and FAudio are older than FNA.dll, so textures fail to load with them. The "Windows native libraries" workflow (`.github/workflows/windows-natives.yml`) builds them from this repository's FNA sources, with the official SDL2 2.32.10 and a statically linked C runtime. The script downloads its latest artifact. The bootstrap's clipboard goes through SDL, and it installs the remix maps into the game's own level folder itself, on both systems.

The "Windows play test" workflow plays a Windows zip from a (draft) release on a GitHub Windows machine through the live relay. Modes: `self` (host and join on one machine), `menu` (the same through the Friends menu and clipboard), `typed` (the joiner's clipboard holds an old code and the host's code is typed with keystrokes), and `host`/`join` (play against another computer).

    python3 package_windows.py

## Verification

- 1.5.2: a 1.5.2 host and an older guest played a match through the live relay; the relay saved the match record, with the host's game report and both players' traffic, and `relay/matches.py` printed it.
- 1.5.1: the join screen with an old code already filled in from the clipboard, then the host's code typed with real keystrokes and Enter: the two copies joined and completed a match, on the Mac (System Events keystrokes) and on a GitHub Windows machine (the play test's `typed` mode).
- 1.5.0: on GitHub's Windows Server 2025 machines (software Direct3D 11), two Windows copies completed a match, both by direct host/join and through the menu with the code read from the clipboard. A Mac host with a Windows guest completed matches in three of four runs; the failed run left no error text and happened while an earlier test was still running on the Mac. A Windows host with a Mac guest completed a match. On the Mac, menu host and clipboard join still work, and maps still install. Not tested: a Windows PC with a real GPU, keyboard or controller.

- 1.4.0: tested on an Apple Silicon Mac, with the Intel half running under Rosetta. Using the signed runtimes from the release build, an Intel host, an Apple Silicon guest and an Intel guest completed a match through the deployed relay. Unsigned builds also passed: an Intel host with an Apple Silicon guest, and an Apple Silicon host with two Intel guests and one Apple Silicon guest. The released app, forced to run as Intel, started the Intel runtime and reached the Friends menu. No real Intel Mac or macOS release before 15 has been tested.

- 1.3.0: eight game processes completed a match through the deployed relay; four 1.2.0 processes still completed a match through the updated relay. Relay tests cover eight-slot rooms.
- 1.2.0: one game hosted through the menu and a second joined through the menu with the code it read from the clipboard; both reached the same lobby with the room code banner shown. A join with an unknown code showed "NO ROOM WITH THAT CODE". The four-process relay match still passes with the 1.2.0 bootstrap.
- 1.1.0: four independent game processes from the installed app seated in one relay room on the deployed Fly relay, connected to each other, and completed a forced match start (`verification/relay-four-player-match.log`). The launcher's room check reported open, full, missing, and unreachable rooms correctly. Relay protocol tests: `node --test` in `relay/`.

Earlier 1.0.0 LAN verification:

- Four independent real game processes using the bundled Mono runtime connected in one lobby and entered a GameLevel with four connected players and four ducks. Repeated after Developer ID signing; all remained connected for a further ten seconds.
- An incorrect room secret was rejected: the joining client reached ConnectionError with zero connections while the host remained alone.
- Actual native launcher opened and launched local gameplay from its writable per-user game copy.
- All five installed and bundled custom map files match the previously play-tested remix source files by SHA-256.
- Disk image checksum, read-only mount, included installer checksum, app deep/strict signature, and mounted image contents verified.
- The final app was installed from the disk image and its native launcher opened successfully.
- Native library dependencies were relocated into the app. Minimum macOS versions were checked against macOS 15. A sample of the signed running process showed no Homebrew library paths.

Evidence is retained under `verification/`.

## What remains for the first remote session

All relay tests ran on one Mac. Packets crossed the internet to the relay and back, but no session between separate homes has been played yet. Relayed packets add the round trip to the relay (Virginia) to each hop.

The custom game app is Developer ID signed but **not Apple-notarized**. Apple Gatekeeper reports `Unnotarized Developer ID`. Follow the first-launch Open Anyway steps in the included guide; never disable Gatekeeper globally.

## Implementation

`FriendsBootstrap.cs` shows the menu, installs a fresh relay transport for each room, and enters the original LAN host/join flow with eight slots, using the room code as the lobby password. Leaving a lobby returns to the menu. `Launcher.swift` installs maps without overwriting existing files and starts the bundled Mono engine; it has no window of its own. Logs are in `~/Library/Logs/DuckGameFriends/game.log`.

Two upstream macOS LAN bugs are fixed in source and applied to the pinned release using `PatchRelease.cs`: Mono does not implement UDP listener enumeration, so clients bind an OS-assigned ephemeral port; the Steam-only mid-game metadata helper is skipped for LAN sessions. The installed original DuckGameRebuilt app is unchanged.

`build_bundle.py` prepares the bundle from the installed v1.4.7-macos.8 release, the official Mono Sonoma bottle, and official Homebrew Sequoia dependency bottles. Build inputs are currently staged under `/tmp/duckfriends`; this is a local packaging workflow, not portable CI. `package_dmg.py` signs with the local Developer ID and produces the image. When those staged inputs are unavailable, `update_bundle.py` rebuilds only the bootstrap, launcher, guide, and version on top of a verified release app; 1.1.0 was built this way from the 1.0.0 app. Keep build output and runtime caches out of Git.
