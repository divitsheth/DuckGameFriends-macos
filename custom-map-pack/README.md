# Divits Party Pack

Five original arenas for 2–4 local players, using built-in Duck Game objects.

| Map | Play style |
| --- | --- |
| Rooftop Rumble | Rooftop jumps, open gaps, central sniper rifle |
| Bridge Brawl | Long wooden bridge, lower islands, grenade launcher |
| Bunker Crossfire | Enclosed arena, two fighting lanes, central laser rifle |
| Canopy Clash | Grass platforms, vertical climbing, exposed high ground |
| Crateyard | Industrial platforms and destructible crate cover |

## Play

Installed in `~/.config/DuckGame/Levels/Divits Party Pack/`.

In the multiplayer lobby, open **Match Settings → Custom Levels**, open
**Divits Party Pack**, and enable the maps. Restart the game if the folder is not
visible. The maps can also be opened individually in the level editor.

## Edit and rebuild

`source/layouts.py` defines the geometry, weapons, crates, and four spawn points.
Coordinates use a 16-pixel grid. `source/*.tsv` contains the generated layouts.
`source/previews.py` generates schematic thumbnails (requires Pillow).
`source/BuildMaps.cs` uses the installed game's own binary serializer and checks
object types, four supported spawns, and serialization round trips.

Run `./build.sh` from this folder to rebuild the `.lev` files. The script uses
Mono and the installed app; it does not automatically replace your installed maps.
Copy the five files from `maps/` to the folder above to install another build.

## Verification

All maps passed the binary-format and spawn checks. Each was also loaded through
the actual game's editor test mode in an isolated save directory, checking that
four ducks remained alive after 12 seconds. This is a loading and spawn smoke
test; multiplayer balance still needs human playtesting.

The app bundle prevents the editor from writing its temporary test level, so
the runtime checks used a separate copy of the game resources outside the app
bundle. Normal custom-map selection only needs the installed `.lev` files.
