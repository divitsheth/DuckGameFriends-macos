# Duck Game Friends for macOS

[**Download the latest DMG**](https://github.com/divitsheth/DuckGameFriends-macos/releases/latest)

Duck Game for **Apple Silicon Macs running macOS 15 or newer**, with a native launcher for private 2–4 player games over Tailscale.

- Full original game and original maps, plus five custom remix maps.
- Host a room, copy its room code, and join from another Mac.
- Bundled Mono runtime; no Homebrew setup needed to play.
- Local play and the original challenge content are included. Arcade requires a named player profile, rather than a guest profile.

## Install and play

1. Download the DMG from Releases and drag **Duck Game Friends** into Applications.
2. Open it. The app is Developer ID signed but **not Apple-notarized**; if blocked, use System Settings → Privacy & Security → Open Anyway.
3. For remote play, install the included official Tailscale installer and connect everyone to the same Tailscale network.
4. One person selects **Host**, then privately shares the room code. Friends select **Join**. Everyone must join before starting the match.

Each player should own Duck Game. Keep room codes private: they contain the room password.

See [Quick Start](friends-app/Quick%20Start.html) and [technical notes](friends-app/README.md).

## Changes and maps

- [Native launcher and release patcher](friends-app/)
- [Five final remix maps and previews](favorite-remix-pack/)
- [Original map catalog](map-catalog/)
- [Earlier map drafts](custom-map-pack/) — retained as source material; not the five maps bundled in the release.
- macOS LAN fixes in `DuckNetwork.cs` and `NCBasic.cs`.

Four separate local game processes were tested through a four-player match, and an incorrect room password was rejected. A match across separate physical Macs over the internet has **not yet been verified**.

## Credits and source

Based on [philipp-eisen/DuckGameRebuilt-macos](https://github.com/philipp-eisen/DuckGameRebuilt-macos), itself based on [DuckGameRebuilt](https://github.com/TheFlyingFoool/DuckGameRebuilt). Duck Game and third-party components belong to their respective authors; existing notices are retained. See [upstream documentation](README-upstream.md).

The Friends bundle was prepared from upstream release **v1.4.7-macos.8**, with targeted release patches. The build scripts currently document a local staging workflow, not a self-contained CI build. The upstream build workflow is manual-only here because it packages a different app and requires its own signing setup.
