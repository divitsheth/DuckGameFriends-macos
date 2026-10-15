from pathlib import Path
import shutil,subprocess,urllib.request,hashlib,tarfile,re,os
# Builds the Intel (x86_64) half of Duck Game Friends into friends-app/build/intel:
#   Runtime-x86_64/  Mono 6.12 from Mono's official universal installer, with libgdiplus.
#                    Homebrew no longer publishes Intel Mono, which the Apple Silicon runtime uses.
#   game-x86_64/     SDL2, FNA3D, FAudio and Theorafile built from source for x86_64, at the
#                    versions the Apple Silicon build ships; update_bundle.py merges them with lipo.
#   python3 build_intel.py
ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT/'friends-app/build/intel'
WORK=Path('/tmp/duckfriends/intel')
MIN_OS='11.0'
MONO_PKG=('https://download.mono-project.com/archive/6.12.0/macos-10-universal/MonoFramework-MDK-6.12.0.206.macos10.xamarin.universal.pkg','80b0dbfa59ba9ed76dbf1393998e6a2ed2d1ccc8f5850c7a46fbe31a2aea88d8')
SDL2=('https://github.com/libsdl-org/SDL/releases/download/release-2.32.10/SDL2-2.32.10.tar.gz','5f5993c530f084535c65a6879e9b26ad441169b3e25d789d83287040a9ca5165')
MONO_PREFIX='/Library/Frameworks/Mono.framework/Versions/6.12.0/lib/'
def run(*args,**kw):return subprocess.check_output([str(a) for a in args],stderr=subprocess.STDOUT,text=True,errors='replace',**kw)
def fetch(url,sha,dest):
 if not dest.exists():
  data=urllib.request.urlopen(url).read()
  dest.write_bytes(data)
 if hashlib.sha256(dest.read_bytes()).hexdigest()!=sha:raise RuntimeError('Checksum mismatch: '+url)
 return dest
def thin(source,dest):
 if run('lipo','-archs',source).split()==['x86_64']:shutil.copy2(source,dest)
 else:run('lipo',source,'-thin','x86_64','-output',dest)
WORK.mkdir(parents=True,exist_ok=True)
if OUT.exists():shutil.rmtree(OUT)
runtime=OUT/'Runtime-x86_64';game=OUT/'game-x86_64'
(runtime/'bin').mkdir(parents=True);(runtime/'lib').mkdir();game.mkdir()

# Mono runtime.
pkg=fetch(*MONO_PKG,WORK/'mono.pkg')
expanded=WORK/'mono-expanded'
if not expanded.exists():run('pkgutil','--expand-full',pkg,expanded)
mono=expanded/'mono.pkg/Payload/Library/Frameworks/Mono.framework/Versions/6.12.0'
for folder in ['4.5','gac']:shutil.copytree(mono/'lib/mono'/folder,runtime/'lib/mono'/folder,symlinks=True)
shutil.copytree(mono/'etc/mono',runtime/'etc/mono',symlinks=True)
# Drop the optional precompiled (AOT) images; Mono compiles those assemblies on demand instead.
for p in (runtime/'lib/mono').rglob('*.dylib'):p.unlink()
config=runtime/'etc/mono/config'
config.write_text(config.read_text().replace(MONO_PREFIX,'$mono_libdir/'))
thin(mono/'bin/mono-sgen64',runtime/'bin/mono-sgen')
# The runtime's own native helpers, plus libgdiplus and everything it links.
queue=['libMonoPosixHelper.dylib','libmono-native-compat.0.dylib','libmono-btls-shared.dylib','libgdiplus.0.dylib']
copied=set()
while queue:
 name=queue.pop(0)
 if name in copied:continue
 copied.add(name);dest=runtime/'lib'/name
 thin(mono/'lib'/name,dest)
 for line in run('otool','-L',dest).splitlines()[1:]:
  dep=line.strip().split(' (')[0]
  if not dep.startswith(MONO_PREFIX):continue
  base=dep.removeprefix(MONO_PREFIX)
  if base==name:run('install_name_tool','-id','@loader_path/'+name,dest)
  else:run('install_name_tool','-change',dep,'@loader_path/'+base,dest);queue.append(base)
# The names Mono's config refers to.
for alias,target in [('libgdiplus.dylib','libgdiplus.0.dylib'),('libmono-native-compat.dylib','libmono-native-compat.0.dylib')]:
 os.symlink(target,runtime/'lib'/alias)
for f in ['LICENSE','COPYING.LIB']:
 if (mono/f).exists():shutil.copy2(mono/f,runtime/('mono-'+f))

# Game libraries from source.
build=WORK/'build';shutil.rmtree(build,ignore_errors=True);build.mkdir()
sdl_archive=fetch(*SDL2,WORK/'SDL2.tar.gz')
with tarfile.open(sdl_archive) as t:t.extractall(build,filter='data')
env=dict(os.environ,MACOSX_DEPLOYMENT_TARGET=MIN_OS)
cmake=['uvx','cmake']
common=['-DCMAKE_BUILD_TYPE=Release','-DCMAKE_OSX_ARCHITECTURES=x86_64','-DCMAKE_OSX_DEPLOYMENT_TARGET='+MIN_OS,'-DCMAKE_POLICY_VERSION_MINIMUM=3.5']
run(*cmake,'-S',build/'SDL2-2.32.10','-B',build/'sdl2',*common,'-DSDL_STATIC=OFF','-DSDL_TEST=OFF',env=env)
run(*cmake,'--build',build/'sdl2','-j8',env=env)
run(*cmake,'--install',build/'sdl2','--prefix',build/'prefix',env=env)
lib=ROOT/'FNA/lib'
for name in ['FNA3D','FAudio']:
 run(*cmake,'-S',lib/name,'-B',build/name,*common,'-DCMAKE_PREFIX_PATH='+str(build/'prefix'),env=env)
 run(*cmake,'--build',build/name,'-j8',env=env)
(build/'theora').mkdir()
run('make','-f',lib/'Theorafile/Makefile','CC=clang -arch x86_64 -mmacosx-version-min='+MIN_OS,cwd=build/'theora',env=env)
for source in [build/'sdl2/libSDL2-2.0.0.dylib',build/'FNA3D/libFNA3D.0.dylib',build/'FAudio/libFAudio.0.dylib',build/'theora/libtheorafile.dylib']:
 dest=game/source.name;shutil.copy2(source.resolve(),dest)
 for line in run('otool','-L',dest).splitlines()[1:]:
  dep=line.strip().split(' (')[0]
  if dep.startswith('@rpath/'):
   if Path(dep).name==dest.name:run('install_name_tool','-id','@loader_path/'+dest.name,dest)
   else:run('install_name_tool','-change',dep,'@loader_path/'+Path(dep).name,dest)

# Check that nothing points outside the bundle and everything runs on the minimum OS.
for p in [p for p in OUT.rglob('*.dylib') if not p.is_symlink()]+[runtime/'bin/mono-sgen']:
 deps=run('otool','-L',p)
 assert '\t/Library/' not in deps and '@rpath' not in deps and '/opt/homebrew' not in deps,(p,deps)
 assert run('lipo','-archs',p).strip()=='x86_64',p
 version=re.search(r'(?:minos|version) ([\d.]+)',run('vtool','-show-build',p))
 if version and tuple(map(int,version[1].split('.')[:2]))>tuple(map(int,MIN_OS.split('.'))):
  raise RuntimeError('Library requires a newer OS: '+str(p)+' '+version[1])
 run('codesign','--force','--sign','-',p)
print('INTEL_READY',OUT)
