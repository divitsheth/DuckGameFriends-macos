from pathlib import Path
import shutil,subprocess,json,urllib.request,hashlib,tarfile,re,os,plistlib
ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT/'friends-app/build'
APP=OUT/'Duck Game Friends.app'
RES=APP/'Contents/Resources'
MONO=Path('/tmp/duckfriends/mono-sonoma/mono/6.14.1')
ORIGINAL=Path('/Applications/DuckGameRebuilt.app/Contents/Resources/game')
def run(*args):return subprocess.check_output([str(a) for a in args],stderr=subprocess.STDOUT,text=True)
if APP.exists():shutil.rmtree(APP)
(RES/'Runtime/bin').mkdir(parents=True)
(APP/'Contents/MacOS').mkdir()
shutil.copytree(ORIGINAL,RES/'game',symlinks=False)
for folder in ['4.5','gac']:
 shutil.copytree(MONO/'lib/mono'/folder,RES/'Runtime/lib/mono'/folder,symlinks=True)
shutil.copytree(MONO/'etc',RES/'Runtime/etc',symlinks=True)
for f in MONO.glob('lib/*.dylib'):shutil.copy2(f,RES/'Runtime/lib'/f.name)
shutil.copy2(MONO/'bin/mono-sgen',RES/'Runtime/bin/mono-sgen')
shutil.copytree(ROOT/'favorite-remix-pack/maps',RES/'Maps')
shutil.copy2('/Applications/DuckGameRebuilt.app/Contents/Resources/AppIcon.icns',RES/'AppIcon.icns')
print(run('mcs','-out:/tmp/duckfriends/tools/PatchRelease.exe','-r:/tmp/duckfriends/tools/Mono.Cecil.dll',ROOT/'friends-app/PatchRelease.cs'))
print(run('mono','/tmp/duckfriends/tools/PatchRelease.exe',ORIGINAL/'DuckGame.exe',RES/'game/DuckGame.exe'))
print(run('mcs','-out:'+str(RES/'game/FriendsBootstrap.exe'),'-r:'+str(RES/'game/DuckGame.exe'),'-r:'+str(RES/'game/FNA.dll'),ROOT/'friends-app/FriendsBootstrap.cs'))
print(run('swiftc','-O','-target','arm64-apple-macos14.0',ROOT/'friends-app/Launcher.swift','-o',APP/'Contents/MacOS/DuckGameFriends'))
# Native dependencies come from official Homebrew bottles for macOS 14, rather
# than linking to libraries that happen to exist on the builder's Mac.
cache=Path('/tmp/duckfriends/portable-bottles');cache.mkdir(exist_ok=True)
fetched={}
notices=RES/'Licenses';notices.mkdir()
for f in ['LICENSE','COPYING.LIB']:
 if (MONO/f).exists():shutil.copy2(MONO/f,notices/('mono-'+f))
def bottle(name):
 if name in fetched:return fetched[name]
 print('Resolving portable dependency:',name,flush=True)
 metadata=json.load(urllib.request.urlopen('https://formulae.brew.sh/api/formula/'+name+'.json'))
 files=metadata['bottle']['stable']['files']
 if 'arm64_sequoia' not in files:raise RuntimeError('No macOS15 bottle for '+name)
 b=files['arm64_sequoia'];folder=cache/name
 if not folder.exists():
  tok=json.load(urllib.request.urlopen('https://ghcr.io/token?service=ghcr.io&scope=repository:homebrew/core/'+name+':pull'))['token']
  data=urllib.request.urlopen(urllib.request.Request(b['url'],headers={'Authorization':'Bearer '+tok})).read()
  assert hashlib.sha256(data).hexdigest()==b['sha256']
  archive=cache/(name+'.tar.gz');archive.write_bytes(data)
  folder.mkdir()
  with tarfile.open(archive) as t:t.extractall(folder,filter='data')
 prefix=next(p for p in (folder/name).iterdir() if p.is_dir() and (p/'lib').exists())
 fetched[name]=prefix
 license_dir=notices/name;license_dir.mkdir(exist_ok=True)
 for p in prefix.rglob('*'):
  if p.is_file() and re.match(r'^(LICENSE|COPYING|COPYRIGHT)([.\-_]|$)',p.name,re.I):
   shutil.copy2(p,license_dir/(str(p.relative_to(prefix)).replace('/','_')))
 return prefix

def dependency_path(dep):
 match=re.search(r'(?:/opt/homebrew/opt/|/opt/homebrew/Cellar/|@@HOMEBREW_PREFIX@@/opt/|@@HOMEBREW_CELLAR@@/)([^/]+)/(.*)',dep)
 if not match:raise RuntimeError('Unexpected dependency: '+dep)
 name,relative=match.groups()
 if '/Cellar/' in dep or '@@HOMEBREW_CELLAR@@' in dep:relative=relative.split('/',1)[1]
 prefix=MONO if name=='mono' else bottle(name)
 source=prefix/relative
 if not source.exists():raise RuntimeError('Missing bottle library '+str(source))
 return source

queue=[p for p in (RES/'game').rglob('*.dylib')]+list((RES/'Runtime/lib').glob('*.dylib'))+[RES/'Runtime/bin/mono-sgen']
seen=set();all_macho=[];origins={}
while queue:
 p=queue.pop(0)
 if p in seen:continue
 seen.add(p);all_macho.append(p)
 lines=[line for line in run('otool','-L',p).splitlines() if line.startswith('\t')]
 for line in lines:
  dep=line.strip().split(' (')[0]
  if dep.startswith(('/usr/lib/','/System/')):continue
  # The first dependency in a dylib is its own install ID.
  if p.suffix=='.dylib' and line==lines[0]:
   run('install_name_tool','-id','@loader_path/'+p.name,p);continue
  if dep.startswith('@loader_path/'):
   source=p.parent/dep.removeprefix('@loader_path/')
   if source.exists():queue.append(source);continue
  if dep.startswith('@rpath/') or (p.parent/Path(dep).name).exists():
   source=p.parent/Path(dep).name
   if not source.exists() and p in origins:source=origins[p].parent/Path(dep).name
   if not source.exists():raise RuntimeError('Unresolved '+dep)
  else:source=dependency_path(dep)
  dest=p.parent/source.name
  if not dest.exists():shutil.copy2(source,dest)
  if source!=dest:origins[dest]=source
  run('install_name_tool','-change',dep,'@loader_path/'+dest.name,p)
  queue.append(dest)
# Check the final dependency graph and minimum OS rather than assuming portability.
for p in all_macho:
 deps=run('otool','-L',p)
 assert '/opt/homebrew' not in deps and '@@HOMEBREW' not in deps,(p,deps)
 version=re.search(r'minos ([\d.]+)',run('vtool','-show-build',p))
 if version and tuple(map(int,version[1].split('.')[:2]))>(15,0):
  raise RuntimeError('Library requires a newer OS: '+str(p)+' '+version[1])
 run('codesign','--force','--sign','-',p)
info={'CFBundleName':'Duck Game Friends','CFBundleDisplayName':'Duck Game Friends','CFBundleIdentifier':'com.divitsheth.duckgamefriends','CFBundleExecutable':'DuckGameFriends','CFBundlePackageType':'APPL','CFBundleShortVersionString':'1.0.0','CFBundleVersion':'1','CFBundleIconFile':'AppIcon','LSMinimumSystemVersion':'15.0','NSHighResolutionCapable':True,'NSLocalNetworkUsageDescription':'Connect to your friends for private Duck Game matches.'}
(APP/'Contents/Info.plist').write_bytes(plistlib.dumps(info))
version=hashlib.sha256((RES/'game/DuckGame.exe').read_bytes()+(RES/'game/FriendsBootstrap.exe').read_bytes()).hexdigest()[:12]
(RES/'runtime-version.txt').write_text(version)
shutil.copy2(ROOT/'friends-app/Quick Start.html',RES/'Quick Start.html')
(OUT/'native-manifest.txt').write_text('\n'.join(str(p.relative_to(APP)) for p in all_macho)+'\n')
print('BUNDLE_READY',APP,'runtime',version)
