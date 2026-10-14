from pathlib import Path
ROOT=Path(__file__).parent
maps=[]
class Map:
 def __init__(self,name,tile,bg):
  self.name,self.tile=name,tile; self.tiles={}; self.objects=[('O',bg,16,9)];maps.append(self)
 def floor(self,a,b,y,depth=1):
  for x in range(a,b+1):
   for r in range(y,y+depth):self.tiles[x,r]=self.tile
  return self
 def item(self,t,x,y):self.objects.append(('O',t,x,y));return self
 def pair(self,t,x,y):self.item(t,x,y);self.item(t,32-x,y);return self
 def spawn(self,x,y):return self.pair('FreeSpawn',x,y)
 def save(self):
  nodes=[('T',t,x,y) for (x,y),t in sorted(self.tiles.items())]+self.objects
  ROOT.joinpath(self.name+'.tsv').write_text('\n'.join('\t'.join(map(str,n)) for n in nodes)+'\n')

m=Map('01 Rooftop Rumble','CityTileset','CityBackground')
m.floor(2,10,19,2).floor(22,30,19,2).floor(7,12,16).floor(20,25,16).floor(12,20,13).floor(2,6,13).floor(26,30,13)
m.spawn(4,18).spawn(4,12).pair('Pistol',6,18).pair('Shotgun',10,15).pair('Crate',8,18).pair('Grenade',5,12).item('Sniper',16,12)

m=Map('02 Bridge Brawl','WoodScaffoldingTileset','NatureBackground')
m.floor(1,8,19,2).floor(24,31,19,2).floor(13,19,19).floor(5,10,16).floor(22,27,16).floor(7,25,13).floor(1,4,13).floor(28,31,13)
m.spawn(3,18).spawn(9,12).pair('Shotgun',5,18).pair('Pistol',12,12).pair('Crate',14,12).pair('Grenade',8,15).item('GrenadeLauncher',16,18)

m=Map('03 Bunker Crossfire','IndustrialTileset','IndustrialBackground')
m.floor(1,31,19,2).floor(1,31,6).floor(2,7,16).floor(25,30,16).floor(7,25,13).floor(10,10,17,2).floor(22,22,17,2)
for y in range(7,19):m.floor(1,1,y).floor(31,31,y)
m.spawn(4,18).spawn(9,12).pair('Shotgun',6,18).pair('Pistol',11,12).pair('Crate',14,18).pair('Grenade',5,15).item('LaserRifle',16,12)

m=Map('04 Canopy Clash','NatureTileset','NatureBackground')
m.floor(2,30,19,2).floor(3,9,16).floor(23,29,16).floor(9,14,13).floor(18,23,13).floor(13,19,10)
m.spawn(4,18).spawn(11,12).pair('Pistol',6,18).pair('Shotgun',7,15).pair('Crate',12,18).pair('Grenade',13,12).item('Sniper',16,9)

m=Map('05 Crateyard','IndustrialTileset','CityBackground')
m.floor(1,31,19,2).floor(3,9,16).floor(23,29,16).floor(12,20,13)
m.spawn(3,18).spawn(5,15).pair('Pistol',5,18).pair('Shotgun',8,15).pair('Grenade',14,12).item('AK47',16,12)
for x,y in [(10,18),(10,17),(11,18),(13,18),(15,18)]:m.pair('Crate',x,y)

for m in maps:m.save()
print('Created five distinct layouts')
