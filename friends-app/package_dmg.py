from pathlib import Path
import subprocess,plistlib,shutil,hashlib
ROOT=Path(__file__).resolve().parent
APP=ROOT/'build/Duck Game Friends.app'
identity='Developer ID Application: Divit Paras Sheth (B96M4A6SF3)'
def run(*a):subprocess.run([str(x) for x in a],check=True)
ent=ROOT/'build/mono.entitlements'
ent.write_bytes(plistlib.dumps({k:True for k in ['com.apple.security.cs.allow-jit','com.apple.security.cs.allow-unsigned-executable-memory','com.apple.security.cs.allow-dyld-environment-variables']}))
for relative in (ROOT/'build/native-manifest.txt').read_text().splitlines():
 p=APP/relative
 args=['codesign','--force','--sign',identity,'--timestamp','--options','runtime']
 if p.name=='mono-sgen':args+=['--entitlements',ent]
 run(*args,p)
for bundle in sorted(APP.rglob('*.bundle'),key=lambda p:len(p.parts),reverse=True):
 if (bundle/'Contents/Info.plist').exists():run('codesign','--force','--sign',identity,'--timestamp','--options','runtime',bundle)
run('codesign','--force','--sign',identity,'--timestamp','--options','runtime',APP)
run('codesign','--verify','--deep','--strict','--verbose=2',APP)
stage=ROOT/'build/DMG'
if stage.exists():shutil.rmtree(stage)
stage.mkdir()
shutil.copytree(APP,stage/APP.name,symlinks=True)
(stage/'Applications').symlink_to('/Applications')
shutil.copy2(ROOT/'Quick Start.html',stage/'Quick Start.html')
version=plistlib.loads((APP/'Contents/Info.plist').read_bytes())['CFBundleShortVersionString']
out=Path.home()/('Downloads/DuckGameFriends-'+version+'-AppleSilicon.dmg')
run('hdiutil','create','-volname','Duck Game Friends','-srcfolder',stage,'-ov','-format','UDZO',out)
run('codesign','--sign',identity,'--timestamp',out)
run('hdiutil','verify',out)
(out.with_suffix('.dmg.sha256')).write_text(hashlib.sha256(out.read_bytes()).hexdigest()+'  '+out.name+'\n')
print('DMG_READY',out)
