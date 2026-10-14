using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DuckGame;
// Generate normal .lev files using Duck Game's own binary serializer.
class BuildMaps {
 static BinaryClassChunk Obj(string type,float x,float y,bool flip=false) {
  var b=new BinaryClassChunk();
  b.AddProperty("type","DuckGame."+type+", DuckGame"); b.AddProperty("x",x); b.AddProperty("y",y);
  b.AddProperty("chance",1f); b.AddProperty("accessible",true); b.AddProperty("chanceGroup",-1); b.AddProperty("flipHorizontal",flip);
  return b;
 }
 static void Main(string[] args) {
  MonoMain.moddingEnabled=false;
  foreach(var spec in Directory.GetFiles(args[0],"*.tsv").OrderBy(x=>x)) {
   var level=new LevelData(); var meta=level.metaData;
   meta.guid=Guid.NewGuid().ToString(); meta.type=LevelType.Deathmatch; meta.size=LevelSize.Ginormous;
   meta.deathmatchReady=true; meta.online=true; meta.workshopID=0; meta.eightPlayer=false;
   level.SetExtraHeaderInfo(meta);
   var tiles=new Dictionary<Tuple<int,int>,string>(); var rows=File.ReadAllLines(spec);
   foreach(var row in rows) {var p=row.Split('\t'); if(p[0]=="T")tiles[Tuple.Create(int.Parse(p[2]),int.Parse(p[3]))]=p[1];}
   foreach(var row in rows) {
    var p=row.Split('\t'); if(p.Length<4)continue;
    int x=int.Parse(p[2]),y=int.Parse(p[3]);
    var b=Obj(p[1],x*16,y*16,p[0]!="T" && x>16);
    if(p[0]=="T") {
     // Use the same eight-neighbour auto-tile lookup as AutoBlock.FindFrame.
     int n=0; int[,] offsets={{1,1},{-1,1},{1,-1},{-1,-1},{-1,0},{0,1},{1,0},{0,-1}};
     for(int i=0;i<8;i++){string t;if(tiles.TryGetValue(Tuple.Create(x+offsets[i,0],y+offsets[i,1]),out t)&&t==p[1])n|=1<<i;}
     b.AddProperty("frame",(int)AutoBlock.fixedNeighborFrameLookupList[n]);
    }
    if(p[1]=="FreeSpawn"){ b.AddProperty("spawnType",0); b.AddProperty("secondSpawn",false); b.AddProperty("eightPlayerOnly",false); }
    level.objects.Add(b);
   }
   level.proceduralData.numSpawns=4; level.proceduralData.sideMask=0;
   string preview=Path.ChangeExtension(spec,".png"); if(File.Exists(preview))level.previewData.preview=Convert.ToBase64String(File.ReadAllBytes(preview));
   string file=Path.Combine(args[1],Path.GetFileNameWithoutExtension(spec)+".lev");
   File.WriteAllBytes(file,level.Serialize().buffer);
   BinaryClassChunk.fullDeserializeMode=true;
   var check=BinaryClassChunk.FromData<LevelData>(new BitBuffer(File.ReadAllBytes(file),false));
   if(check.GetResult()!=DeserializeResult.Success || check.objects.objects.Count!=level.objects.objects.Count)throw new Exception("Round trip failed: "+file);
   var spawns=check.objects.objects.Where(o=>o.GetProperty<string>("type").Contains("FreeSpawn")).ToList();
   if(spawns.Count!=4)throw new Exception("Need four spawns");
   foreach(var s in spawns) {int x=(int)s.GetProperty<float>("x")/16,y=(int)s.GetProperty<float>("y")/16;if(tiles.ContainsKey(Tuple.Create(x,y))||!tiles.ContainsKey(Tuple.Create(x,y+1)))throw new Exception("Unsafe spawn: "+file);}
   foreach(var o in check.objects.objects)if(typeof(Thing).Assembly.GetType(o.GetProperty<string>("type").Split(',')[0])==null)throw new Exception("Unknown object");
   Console.WriteLine(Path.GetFileName(file)+": "+check.objects.objects.Count+" objects, 4 supported spawns, serializer round-trip OK");
  }
 }
}
