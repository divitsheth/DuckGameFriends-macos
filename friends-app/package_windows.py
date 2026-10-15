from pathlib import Path
import shutil,subprocess,plistlib,hashlib,struct,zipfile,tempfile
# Packages Duck Game Friends for Windows from the macOS app made by update_bundle.py.
# The game folder already holds DuckGameRebuilt's Windows libraries (32-bit SDL2,
# FNA3D, FAudio); Windows provides .NET Framework 4.8, so no runtime is bundled.
# The Friends bootstrap becomes "Duck Game Friends.exe", built as a 32-bit
# program so the process can load those libraries.
#   python3 package_windows.py
ROOT=Path(__file__).resolve().parent
APP=ROOT/'build/Duck Game Friends.app'
RES=APP/'Contents/Resources'
SRC=ROOT.parent/'friends-app'
version=plistlib.loads((APP/'Contents/Info.plist').read_bytes())['CFBundleShortVersionString']
stage=ROOT/'build/windows/Duck Game Friends'
if stage.exists():shutil.rmtree(stage)
# Everything except the macOS and Linux native libraries and launch files.
skip=lambda d,names:[n for n in names if n.endswith(('.dylib','.sh')) or '.so' in n or n in ('OSX-Linux-x64','icd.d','FriendsBootstrap.exe')]
shutil.copytree(RES/'game',stage,ignore=skip)
shutil.copytree(RES/'Maps',stage/'Maps')
shutil.copy2(SRC/'Quick Start.html',stage/'Quick Start.html')
shutil.copy2(stage/'DuckGame.exe.config',stage/'Duck Game Friends.exe.config')
shutil.copy2(stage/'Windows-x86/Steamworks.NET.dll',stage/'Steamworks.NET.dll')
# The game folder's Windows FNA3D/FAudio predate FNA.dll; use the ones built from the
# repository's FNA sources by the "Windows native libraries" workflow.
natives=ROOT/'build/windows-natives'
if not natives.exists():
 run_id=subprocess.check_output(['gh','run','list','-R','divitsheth/DuckGameFriends-macos','--workflow','windows-natives.yml','--status','success','-L','1','--json','databaseId','--jq','.[0].databaseId'],text=True).strip()
 subprocess.run(['gh','run','download',run_id,'-R','divitsheth/DuckGameFriends-macos','-n','windows-natives','-D',str(natives)],check=True)
for name in ['FNA3D.dll','FAudio.dll','SDL2.dll']:shutil.copy2(natives/name,stage/name)

# Windows icon from the app icon: PNG images in an ICO container.
with tempfile.TemporaryDirectory() as tmp:
 images=[]
 for size in [16,32,48,256]:
  png=Path(tmp)/f'{size}.png'
  subprocess.run(['sips','-s','format','png','-z',str(size),str(size),RES/'AppIcon.icns','--out',png],check=True,capture_output=True)
  images.append((size,png.read_bytes()))
 ico=struct.pack('<HHH',0,1,len(images));offset=6+16*len(images);data=b''
 for size,png in images:
  ico+=struct.pack('<BBBBHHII',size%256,size%256,0,0,1,32,len(png),offset+len(data));data+=png
 icon=ROOT/'build/windows/icon.ico';icon.write_bytes(ico+data)

# Compile against .NET Framework 4.8's reference assemblies (from the Mono installer that
# build_intel.py unpacks), not Mono's own libraries, which have methods Windows lacks.
api=Path('/tmp/duckfriends/intel/mono-expanded/mono.pkg/Payload/Library/Frameworks/Mono.framework/Versions/6.12.0/lib/mono/4.8-api')
if not api.exists():raise SystemExit('Run build_intel.py first.')
mono=RES/'Runtime'
env={'MONO_CFG_DIR':str(mono/'etc'),'MONO_GAC_PREFIX':str(mono),'PATH':'/usr/bin:/bin'}
print(subprocess.check_output([str(mono/'bin/mono-sgen'),str(mono/'lib/mono/4.5/mcs.exe'),'-platform:x86','-target:winexe','-noconfig','-nostdlib','-lib:'+str(api),'-r:mscorlib.dll','-r:System.dll','-r:System.Core.dll','-win32icon:'+str(icon),
 '-out:'+str(stage/'Duck Game Friends.exe'),'-r:'+str(stage/'DuckGame.exe'),'-r:'+str(stage/'FNA.dll'),
 str(SRC/'FriendsBootstrap.cs'),str(SRC/'NCRelay.cs'),str(SRC/'FriendsMenu.cs')],env=env,stderr=subprocess.STDOUT,text=True,errors='replace'))

out=Path.home()/('Downloads/DuckGameFriends-'+version+'-Windows.zip')
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for p in sorted(stage.rglob('*')):
  if p.is_file():z.write(p,p.relative_to(stage.parent))
(out.parent/(out.name+'.sha256')).write_text(hashlib.sha256(out.read_bytes()).hexdigest()+'  '+out.name+'\n')
print('ZIP_READY',out)
