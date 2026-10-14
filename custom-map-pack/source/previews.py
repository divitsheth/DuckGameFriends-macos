from pathlib import Path
from PIL import Image,ImageDraw
import re
root=Path(__file__).parent
repo=root.parent.parent
content=repo/'deps/Content'
src=(repo/'DuckGame/src/DuckGame/AutoBlock.cs').read_text(encoding='utf-8-sig')
lookup=list(map(int,re.search(r'fixedNeighborFrameLookupList = new byte\[256\]\s*\{(.*?)\}',src,re.S).group(1).replace('\n','').split(',')[:-1])) if False else [int(x) for x in re.findall(r'\d+',re.search(r'fixedNeighborFrameLookupList = new byte\[256\]\s*\{(.*?)\}',src,re.S).group(1))]
assets={'CityTileset':'cityTileset','NatureTileset':'natureTileset','IndustrialTileset':'industrialTileset','WoodScaffoldingTileset':'woodScaffolding'}
colors=['#281630','#143331','#1c2731','#173525','#242635']
for idx,file in enumerate(sorted(root.glob('*.tsv'))):
 rows=[l.split('\t') for l in file.read_text().splitlines()]
 tiles={(int(p[2]),int(p[3])):p[1] for p in rows if p[0]=='T'}
 im=Image.new('RGBA',(544,352),colors[idx]);d=ImageDraw.Draw(im)
 for y in range(32,336,32):d.line((0,y,543,y),fill=(255,255,255,7))
 for (x,y),t in tiles.items():
  sheet=Image.open(content/(assets[t]+'.png')).convert('RGBA')
  offsets=[(1,1),(-1,1),(1,-1),(-1,-1),(-1,0),(0,1),(1,0),(0,-1)]
  mask=sum(1<<i for i,(dx,dy) in enumerate(offsets) if tiles.get((x+dx,y+dy))==t)
  frame=lookup[mask]; cols=sheet.width//16
  tile=sheet.crop((frame%cols*16,frame//cols*16,frame%cols*16+16,frame//cols*16+16))
  im.alpha_composite(tile,(x*16,y*16))
 for kind,t,x,y in rows:
  if kind=='T' or t.endswith('Background'):continue
  x,y=int(x)*16+8,int(y)*16+8
  if t=='FreeSpawn':d.ellipse((x-5,y-6,x+5,y+4),fill='#f8d560');d.rectangle((x+1,y-5,x+8,y-2),fill='#f99232')
  elif t=='Crate':d.rectangle((x-7,y-7,x+7,y+7),fill='#926446',outline='#d8ae74');d.line((x-5,y-5,x+5,y+5),fill='#d8ae74')
  else:d.rectangle((x-6,y-2,x+6,y+1),fill='#e1edf3');d.rectangle((x-3,y+1,x,y+5),fill='#b7c1cd')
 im.convert('RGB').save(file.with_suffix('.png'))
print('Rendered five map-selection previews')
