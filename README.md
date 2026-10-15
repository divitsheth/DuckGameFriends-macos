# Duck Game Friends for macOS

[**Download the latest DMG**](https://github.com/divitsheth/DuckGameFriends-macos/releases/latest)

Duck Game for **Apple Silicon Macs running macOS 15 or newer and Intel Macs running macOS 11 or newer**, with an in-game menu for private 2–8 player online games using short room codes.

- Full original game and original maps, plus five custom remix maps.
- Host a room, copy its room code, and join from another Mac.
- Bundled Mono runtime; no Homebrew setup needed to play.
- Local play and the original challenge content are included. Arcade requires a named player profile, rather than a guest profile.

## Install and play

1. Download the DMG from Releases and drag **Duck Game Friends** into Applications.
2. Open it. The app is Developer ID signed but **not Apple-notarized**; if blocked, use System Settings → Privacy & Security → Open Anyway.
3. The game opens on the Duck Game Friends menu (keyboard or controller). One person picks **HOST A ROOM** and sends the room code (like `K7QD-2MXH`, shown in the lobby and already copied) to friends. Friends pick **JOIN A ROOM** and type or paste it. Everyone must join before starting the match.

No VPN, accounts, or router setup: games connect through a small [room relay](relay/). Each player should own Duck Game. Keep room codes private: anyone with the code can join while a slot is free.

See [Quick Start](friends-app/Quick%20Start.html) and [technical notes](friends-app/README.md).

## Changes and maps

- [In-game menu, launcher and release patcher](friends-app/)
- [Five final remix maps and previews](favorite-remix-pack/)
- [Original map catalog](map-catalog/)
- [Earlier map drafts](custom-map-pack/) — retained as source material; not the five maps bundled in the release.
- [Room relay server](relay/) and the in-game relay transport, [`NCRelay.cs`](friends-app/NCRelay.cs).
- macOS LAN fixes in `DuckNetwork.cs` and `NCBasic.cs`.

Eight separate game processes completed an eight-player match through the deployed internet relay, and two players on separate Macs have played through it.

## Credits and source

Based on [philipp-eisen/DuckGameRebuilt-macos](https://github.com/philipp-eisen/DuckGameRebuilt-macos), itself based on [DuckGameRebuilt](https://github.com/TheFlyingFoool/DuckGameRebuilt). Duck Game and third-party components belong to their respective authors; existing notices are retained. See [upstream documentation](README-upstream.md).

The Friends bundle was prepared from upstream release **v1.4.7-macos.8**, with targeted release patches. The build scripts currently document a local staging workflow, not a self-contained CI build. The upstream build workflow is manual-only here because it packages a different app and requires its own signing setup.
