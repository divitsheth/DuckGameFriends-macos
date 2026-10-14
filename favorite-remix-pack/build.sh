#!/bin/bash
set -euo pipefail
pack_dir="$(cd "$(dirname "$0")" && pwd)"
game_dir="${DUCK_GAME_DIR:-/Applications/DuckGameRebuilt.app/Contents/Resources/game}"
build_dir="$(mktemp -d -t duck-remix-build)"
trap 'rm -rf "$build_dir"' EXIT
ln -s "$game_dir/DuckGame.exe" "$build_dir/DuckGame.exe"
for dll in "$game_dir"/*.dll; do
  case "${dll##*/}" in mscorlib.dll|System.*|Microsoft.*) continue ;; esac
  ln -s "$dll" "$build_dir/${dll##*/}"
done
mcs -out:"$build_dir/BuildRemixes.exe" -r:"$build_dir/DuckGame.exe" -r:"$build_dir/FNA.dll" "$pack_dir/source/BuildRemixes.cs"
mono "$build_dir/BuildRemixes.exe" "$game_dir/Content/levels/deathmatch" "$pack_dir/maps"
