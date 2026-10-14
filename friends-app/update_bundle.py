from pathlib import Path
import shutil,subprocess,plistlib,hashlib,sys
# Rebuild only the Friends components on top of a previously verified release
# app, when the full build_bundle.py inputs (upstream app, Mono and Homebrew
# bottles) are not staged. Game content, runtime and native libraries are reused
# byte-for-byte. Then run package_dmg.py to sign and package.
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
print(run('swiftc','-O','-target','arm64-apple-macos14.0',ROOT/'friends-app/Launcher.swift','-o',APP/'Contents/MacOS/DuckGameFriends'))
info=plistlib.loads((APP/'Contents/Info.plist').read_bytes())
info.update({'CFBundleShortVersionString':version,'CFBundleVersion':build})
(APP/'Contents/Info.plist').write_bytes(plistlib.dumps(info))
runtime=hashlib.sha256((RES/'game/DuckGame.exe').read_bytes()+(RES/'game/FriendsBootstrap.exe').read_bytes()).hexdigest()[:12]
(RES/'runtime-version.txt').write_text(runtime)
shutil.copy2(ROOT/'friends-app/Quick Start.html',RES/'Quick Start.html')
macho=[p for p in APP.rglob('*') if p.is_file() and not p.is_symlink() and 'Mach-O' in run('file','-b',p)]
(OUT/'native-manifest.txt').write_text('\n'.join(str(p.relative_to(APP)) for p in macho)+'\n')
print('BUNDLE_READY',APP,'version',version,'runtime',runtime)
