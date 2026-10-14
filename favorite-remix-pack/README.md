# Divits Favorite Remixes

Five local custom maps based on maps you selected from the numbered gallery.
The original maps remain unchanged. These retain the original geometry and
spawn positions, with the changes listed below.

| Remix | Your reference | Changes |
| --- | --- | --- |
| 01 Treetop Crossfire | #052 Forest 10 | Two pairs of stepping stones toward the central trees; central shotgun; lower grenade dispensers replace mines. Original swords and armor stay. |
| 02 Office Breakout | #098 Office 10 | Two extra upper crossing platforms; breakable crates on the middle floor; central sniper replaces pistol; grenades replace the mine dispenser. |
| 03 Foundry Shortcuts | #034 Industrial 08 | New landing pads toward the center; breakable central crate tower; fixed sniper/shotgun boxes; chainsaws on both lower sides. Original teleporters and conveyor belts stay. |
| 04 Snowdrop Scramble | #129 Snow 09 | Extra shelves below the starting cabin; two cover crates in the cabin; shotgun boxes below and grenade-launcher boxes above. |
| 05 Pocket Reactor | #145 Space 05 | Tiny original arena with a breakable center barricade; upper shotgun dispenser and lower grenade dispenser. |

## Play

The five `.lev` files are installed at:
`~/.config/DuckGame/Levels/Divits Favorite Remixes/`.

In the multiplayer lobby, open **Match Settings → Custom Levels** and enable the
maps in **Divits Favorite Remixes**. Set the custom-map percentage to 100% and
the normal/random-map percentages to 0% to play only the selected custom maps.
Restart the game if the new folder is not visible.

Designed for 2–4 players. Each map was loaded in the actual game's editor test
mode with four players, checking that all four were alive after 12 seconds.
The added platforms were checked against the original terrain for overlaps.
Balance, route difficulty, and weapon feel still need human playtesting.

## Local sources

- `source/BuildRemixes.cs`: loads the five shipped `.lev` files, applies the edits,
  assigns stable new map IDs, and checks that all original spawns survive a
  serialization round trip.
- `source/RenderPreviews.cs`: uses the actual game's preview renderer and embeds
  each generated thumbnail in its map.
- `source/RuntimeCheck.cs`: the four-player load/spawn smoke test.
- `previews/`: game-rendered thumbnails; `comparison.png`: originals versus remixes.
- `validation.txt`: results from the runtime checks.

The original level designs belong to Duck Game / DuckGameRebuilt and its
contributors. This pack is a local remix of the user's installed game content.
The user's full 28-map reference list is in `../custom-map-pack/favorites.txt`.

`./build.sh` regenerates the map files from the installed originals using Mono.
It does not automatically install them or generate the rendered previews.
Preview generation and editor tests require a writable runtime copy outside
the macOS app bundle, as the editor writes `Content/_tempPlayLevel.lev`.
