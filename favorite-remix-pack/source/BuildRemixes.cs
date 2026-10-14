using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using DuckGame;

// Local remixes of the user's installed maps. The originals are only read.
class BuildRemixes {
 static string TypeName(BinaryClassChunk o) { return o.GetProperty<string>("type").Split(',')[0].Replace("DuckGame.", ""); }
 static float X(BinaryClassChunk o) { return o.GetProperty<float>("x"); }
 static float Y(BinaryClassChunk o) { return o.GetProperty<float>("y"); }
 static void Set(BinaryClassChunk o, string key, object value) {
  var values = o.GetProperty(key, true) as List<object>;
  if (values == null) o.AddProperty(key, value);
  else { values.Clear(); values.Add(value); }
 }
 static BinaryClassChunk Object(string type, float x, float y) {
  var o = new BinaryClassChunk();
  o.AddProperty("type", "DuckGame." + type + ", DuckGame");
  o.AddProperty("x", x); o.AddProperty("y", y);
  o.AddProperty("chance", 1f); o.AddProperty("accessible", true);
  o.AddProperty("chanceGroup", -1); o.AddProperty("flipHorizontal", false);
  return o;
 }
 static void Add(LevelData l, string type, float x, float y) { l.objects.Add(Object(type, x, y)); }
 static void Replace(LevelData l, string oldType, string newType) {
  foreach (var o in l.objects.objects.Where(o => TypeName(o) == oldType))
   Set(o, "type", "DuckGame." + newType + ", DuckGame");
 }
 static void Contains(BinaryClassChunk o, string weapon) { Set(o, "contains", "DuckGame." + weapon + ", DuckGame"); }
 static void Platform(LevelData l, string tile, int x, int y, int count) {
  for (int i = 0; i < count; i++) {
   var o = Object(tile, x + 16 * i, y);
   int neighbors = (i > 0 ? 16 : 0) | (i < count - 1 ? 64 : 0);
   o.AddProperty("frame", (int)AutoBlock.fixedNeighborFrameLookupList[neighbors]);
   l.objects.Add(o);
  }
 }
 static string StableGuid(string name) {
  using (var hash = SHA256.Create()) return new Guid(hash.ComputeHash(Encoding.UTF8.GetBytes("Divits Favorite Remixes v1/" + name)).Take(16).ToArray()).ToString();
 }
 static void Remix(string root, string output, string original, string name, Action<LevelData> change) {
  var l = DuckFile.LoadLevel(File.ReadAllBytes(Path.Combine(root, original)));
  var originalSpawns = l.objects.objects.Where(o => TypeName(o) == "FreeSpawn").Select(o => X(o) + "," + Y(o)).OrderBy(s => s).ToArray();
  if (originalSpawns.Length != 4) throw new Exception("Expected four original spawns: " + original);
  change(l);
  l.metaData.guid = StableGuid(name);
  l.metaData.workshopID = 0; l.metaData.type = LevelType.Deathmatch;
  l.metaData.deathmatchReady = true; l.metaData.eightPlayer = false;
  l.metaData.eightPlayerRestricted = false;
  l.SetExtraHeaderInfo(l.metaData);
  l.previewData.preview = null;
  string file = Path.Combine(output, name + ".lev");
  var data = l.Serialize();
  using (var stream = File.Create(file)) stream.Write(data.buffer, 0, data.lengthInBytes);
  var check = DuckFile.LoadLevel(File.ReadAllBytes(file));
  var spawns = check.objects.objects.Where(o => TypeName(o) == "FreeSpawn").Select(o => X(o) + "," + Y(o)).OrderBy(s => s).ToArray();
  if (!spawns.SequenceEqual(originalSpawns) || check.metaData.guid != StableGuid(name) || check.GetResult() != DeserializeResult.Success)
   throw new Exception("Round-trip or spawn check failed: " + name);
  Console.WriteLine(name + ": OK; 4 original spawns; " + check.objects.objects.Count + " object records");
 }
 static void Main(string[] args) {
  MonoMain.moddingEnabled = false; BinaryClassChunk.fullDeserializeMode = true;
  Directory.CreateDirectory(args[1]);
  Remix(args[0], args[1], "forest10.lev", "01 Treetop Crossfire", l => {
   // Two new stepping stones bridge the gaps toward the central trees.
   Platform(l, "NatureTileset", 224, 144, 2);
   Platform(l, "NatureTileset", 384, 144, 2);
   Replace(l, "MagnetGun", "Shotgun");
   foreach (var o in l.objects.objects.Where(o => TypeName(o) == "ItemSpawner"))
    if (o.GetProperty<string>("contains").Contains("Mine")) { Contains(o, "Grenade"); Set(o, "spawnTime", 12f); }
  });
  Remix(args[0], args[1], "office10.lev", "02 Office Breakout", l => {
   Platform(l, "OfficeTileset", 224, 176, 3);
   Platform(l, "OfficeTileset", 384, 176, 3);
   Add(l, "Crate", 240, 240); Add(l, "Crate", 400, 240);
   foreach (var o in l.objects.objects.Where(o => TypeName(o) == "ItemSpawner")) {
    if (o.GetProperty<string>("contains").Contains("Pistol")) Contains(o, "Sniper");
    if (o.GetProperty<string>("contains").Contains("Mine")) Contains(o, "Grenade");
   }
  });
  Remix(args[0], args[1], "DGR/industrial08.lev", "03 Foundry Shortcuts", l => {
   // Symmetric landing pads around x=24, between the outer teleporter lanes and center.
   Platform(l, "IndustrialTileset", -112, 192, 2);
   Platform(l, "IndustrialTileset", 144, 192, 2);
   Replace(l, "DeathCrate", "Crate");
   Replace(l, "EnergyScimitar", "Chainsaw");
   foreach (var o in l.objects.objects.Where(o => TypeName(o) == "ItemBoxRandom")) {
    Set(o, "type", "DuckGame.ItemBox, DuckGame"); Contains(o, Y(o) < 150 ? "Sniper" : "Shotgun");
   }
  });
  Remix(args[0], args[1], "snow09.lev", "04 Snowdrop Scramble", l => {
   // Landing shelves flank the starting cabin, helping players return from the ground.
   Platform(l, "SnowTileset", 176, 208, 2);
   Platform(l, "SnowTileset", 416, 208, 2);
   foreach (var o in l.objects.objects.Where(o => TypeName(o) == "ItemBox"))
    Contains(o, Y(o) < 100 ? "GrenadeLauncher" : "Shotgun");
   Add(l, "Crate", 272, 160); Add(l, "Crate", 336, 160);
  });
  Remix(args[0], args[1], "space05.lev", "05 Pocket Reactor", l => {
   // Keep the tiny arena; cover can be destroyed to open the lower firing lane.
   Add(l, "Crate", 304, 160); Add(l, "Crate", 336, 160);
   foreach (var o in l.objects.objects.Where(o => TypeName(o) == "ItemSpawner")) {
    Contains(o, Y(o) < 140 ? "Shotgun" : "Grenade");
    Set(o, "spawnTime", Y(o) < 140 ? 10f : 12f);
   }
  });
 }
}
