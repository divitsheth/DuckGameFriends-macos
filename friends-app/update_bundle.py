from pathlib import Path
import shutil,subprocess,plistlib,hashlib,sys
# Rebuild only the Friends components on top of a previously verified release
# app, when the full build_bundle.py inputs (upstream app, Mono and Homebrew
# bottles) are not staged. Game content, runtime and native libraries are reused
# byte-for-byte. The Intel half from build_intel.py is added when the base app
# does not have it yet. Then run package_dmg.py to sign and package.
#   python3 update_bundle.py "/Volumes/Duck Game Friends/Duck Game Friends.app" 1.1.0 2
ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT/'friends-app/build'
APP=OUT/'Duck Game Friends.app'
RES=APP/'Contents/Resources'
base,version,build=Path(sys.argv[1]),sys.argv[2],sys.argv[3]
def run(*args):return subprocess.check_output([str(a) for a in args],stderr=subprocess.STDOUT,text=True,errors='replace')
if APP.exists():shutil.rmtree(APP)
OUT.mkdir(exist_ok=True)
shutil.copytree(base,APP,symlinks=True)
mono=RES/'Runtime'
env={'MONO_CFG_DIR':str(mono/'etc'),'MONO_GAC_PREFIX':str(mono),'PATH':'/usr/bin:/bin'}
print(subprocess.check_output([str(mono/'bin/mono-sgen'),str(mono/'lib/mono/4.5/mcs.exe'),'-out:'+str(RES/'game/FriendsBootstrap.exe'),'-r:'+str(RES/'game/DuckGame.exe'),'-r:'+str(RES/'game/FNA.dll'),str(ROOT/'friends-app/FriendsBootstrap.cs'),str(ROOT/'friends-app/NCRelay.cs'),str(ROOT/'friends-app/FriendsMenu.cs')],env=env,stderr=subprocess.STDOUT,text=True,errors='replace'))
intel=OUT/'intel'
if not (RES/'Runtime-x86_64').exists():
 if not intel.exists():raise SystemExit('Run build_intel.py first.')
 shutil.copytree(intel/'Runtime-x86_64',RES/'Runtime-x86_64',symlinks=True)
 for lib in (intel/'game-x86_64').glob('*.dylib'):
  run('lipo','-create',RES/'game'/lib.name,lib,'-output',RES/'game'/lib.name)
launcher=OUT/'launcher';launcher.mkdir(exist_ok=True)
for target in ['arm64-apple-macos14.0','x86_64-apple-macos11.0']:
 print(run('swiftc','-O','-target',target,ROOT/'friends-app/Launcher.swift','-o',launcher/target))
run('lipo','-create',*sorted(launcher.iterdir()),'-output',APP/'Contents/MacOS/DuckGameFriends')
info=plistlib.loads((APP/'Contents/Info.plist').read_bytes())
# Apple Silicon libraries come from Homebrew bottles for macOS 15; the Intel ones are built for macOS 11.
info.update({'CFBundleShortVersionString':version,'CFBundleVersion':build,'LSMinimumSystemVersion':'11.0','LSMinimumSystemVersionByArchitecture':{'arm64':'15.0','x86_64':'11.0'}})
(APP/'Contents/Info.plist').write_bytes(plistlib.dumps(info))
# The installed copy is keyed by the game files it was made from, native libraries included.
digest=hashlib.sha256()
for p in [RES/'game/DuckGame.exe',RES/'game/FriendsBootstrap.exe']+sorted((RES/'game').glob('*.dylib')):digest.update(p.read_bytes())
runtime=digest.hexdigest()[:12]
(RES/'runtime-version.txt').write_text(runtime)
shutil.copy2(ROOT/'friends-app/Quick Start.html',RES/'Quick Start.html')
macho=[p for p in APP.rglob('*') if p.is_file() and not p.is_symlink() and 'Mach-O' in run('file','-b',p)]
(OUT/'native-manifest.txt').write_text('\n'.join(str(p.relative_to(APP)) for p in macho)+'\n')
print('BUNDLE_READY',APP,'version',version,'runtime',runtime)
