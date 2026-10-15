using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using DuckGame;
// Starts Duck Game for Duck Game Friends.
//   menu        the Friends menu (FriendsMenu.cs); what the app launches
//   host, join  go straight online with DUCK_FRIENDS_ROOM (automated tests)
//   local       the ordinary local lobby
// On macOS, Launcher.swift starts it with Mono; on Windows it is "Duck Game Friends.exe".
class FriendsBootstrap {
 const string DefaultRelay = "duckfriends-relay.fly.dev:7777";
 static string mode, room;
 static bool configured, menuShown;
 static int lobbyTicks;

 // Swaps the LAN transport for a fresh relay connection to the given room.
 static void UseRelay(string code, bool hosting) {
  NCRelay.room=code; NCRelay.hosting=hosting; NCRelay.failure=null;
  var network=Network.activeNetwork;
  typeof(Network).GetField("_lanCore",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(network,new NCRelay(network,network.networkIndex));
 }
 public static void StartOnline(string how, string code) {
  mode=how; room=code; configured=false; lobbyTicks=0;
  Level.current=new TeamSelect2();
 }
 public static void StartLocal() {
  mode="local"; room=null; NCRelay.room=null; configured=true;
  Level.current=new TeamSelect2();
 }
 // Runs once the lobby is up: opens the room, or joins it.
 static void Configure() {
  // Wait until the lobby has been running for half a second; its player boxes are built during the first frames.
  if(configured || Network.isActive)return;
  if(!(Level.activeLevel is TeamSelect2) || !Level.activeLevel.initialized || Level.current!=Level.activeLevel){lobbyTicks=0;return;}
  if(++lobbyTicks<5)return;
  configured=true;
  DGRSettings.MidGameJoining=false;
  TeamSelect2.GetOnlineSetting("maxplayers").value=NCRelay.Slots;
  UseRelay(room,mode=="host");
  if(mode=="host") {
   TeamSelect2.GetOnlineSetting("name").value="Duck Game Friends";
   // The room code is also the lobby password.
   TeamSelect2.GetOnlineSetting("password").value=room;
   TeamSelect2.GetOnlineSetting("type").value=(int)NetworkLobbyType.LAN;
   TeamSelect2.GetOnlineSetting("dedicated").value=false;
   TeamSelect2.FillMatchmakingProfiles();
   DuckNetwork.Host(NCRelay.Slots,NetworkLobbyType.LAN,true);
   ((TeamSelect2)Level.current).PrepareForOnline();
   Console.WriteLine("FRIENDS_HOST_STARTED room="+room);
  } else {
   var join=new JoinServer(NCRelay.SlotEndPoint(0).ToString());
   // The pinned upstream build has no public LAN+password constructor.
   var field=typeof(JoinServer).GetField("password",BindingFlags.Instance|BindingFlags.NonPublic);
   if(field==null)throw new InvalidOperationException("Incompatible game: LAN password field missing.");
   field.SetValue(join,room);
   Level.current=join;
   Console.WriteLine("FRIENDS_JOIN_STARTED");
  }
 }
 // Copies the bundled remix maps into the game's custom level folder, keeping any existing copies.
 static void InstallMaps() {
  try {
   var source=Environment.GetEnvironmentVariable("DUCK_FRIENDS_MAPS")??Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Maps");
   if(!Directory.Exists(source))return;
   var dest=Path.Combine(DuckFile.levelDirectory,"Divits Favorite Remixes");
   Directory.CreateDirectory(dest);
   foreach(var file in Directory.GetFiles(source,"*.lev")) {
    var target=Path.Combine(dest,Path.GetFileName(file));
    if(!File.Exists(target))File.Copy(file,target);
   }
   Console.WriteLine("FRIENDS_MAPS_READY "+dest);
  } catch(Exception e) { Console.WriteLine("FRIENDS_MAPS_FAILED "+e.Message); }
 }
 static void Tick() {
  if(!menuShown && mode=="menu" && Level.current!=null) {
   menuShown=true;
   Program.main.Window.Title="Duck Game Friends";
   InstallMaps();
   MonoMain.RegisterEngineUpdatable(new RoomBanner());
   Level.current=new FriendsMenu();
   return;
  }
  // Leaving a lobby or a lost connection ends on the title screen; come back to the menu instead.
  if(menuShown && Level.current is TitleScreen && !Network.isActive) {
   NCRelay.room=null;
   Level.current=new FriendsMenu();
   return;
  }
  if(mode=="host"||mode=="join")Configure();
 }
 static void Main(string[] args) {
  mode=args.Length>0?args[0]:"menu";
  if(mode!="local") {
   var relay=(Environment.GetEnvironmentVariable("DUCK_FRIENDS_RELAY")??DefaultRelay).Split(':');
   if(relay.Length!=2)throw new ArgumentException("DUCK_FRIENDS_RELAY must be host:port.");
   var ip=Dns.GetHostAddresses(relay[0]).First(a=>a.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork);
   NCRelay.relay=new IPEndPoint(ip,int.Parse(relay[1]));
  }
  if(mode=="host"||mode=="join") {
   room=Environment.GetEnvironmentVariable("DUCK_FRIENDS_ROOM")??"";
   if(room.Length!=8)throw new ArgumentException("DUCK_FRIENDS_ROOM must be an 8-character room code.");
  }
  MonoMain.IgnoreDGRUpdates=true;
  bool test=Environment.GetEnvironmentVariable("DUCK_FRIENDS_TEST")=="1";
  // Test runs also print the first exceptions thrown, including ones the game catches and hides.
  int shown=0;
  if(test)AppDomain.CurrentDomain.FirstChanceException+=(sender,e)=>{ if(++shown<=20)Console.WriteLine("FRIENDS_EXCEPTION "+e.Exception.GetType().Name+": "+e.Exception.Message); };
  new Thread(()=>{
   int readySeconds=0, leaveSeconds=0, ticks=0;
   while(true) {
    Thread.Sleep(100);
    bool report=test && ++ticks%10==0;
    Tasker.Task(()=>{
     if(mode!="local"||menuShown)Tick();
     if(report) {
      int connected=DuckNetwork.profiles.Count(p=>p.connection!=null);
      if(Level.current is ConnectionError)Console.WriteLine("FRIENDS_CONNECTION_ERROR "+typeof(ConnectionError).GetField("_text",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Level.current));
      Console.WriteLine("FRIENDS_STATE mode="+mode+" status="+DuckNetwork.status+" level="+(Level.current==null?"null":Level.current.GetType().Name)+" connected="+connected+" slots="+DuckNetwork.numSlots+" ducks="+(Level.current==null?0:Level.current.things.OfType<Duck>().Count()));
      if(Environment.GetEnvironmentVariable("DUCK_FRIENDS_TEST_LEAVE")=="1" && NCRelay.hosting && Network.isActive && Level.current is TeamSelect2 && ++leaveSeconds==8) {
       Console.WriteLine("FRIENDS_TEST_LEAVING room="+NCRelay.room);
       Level.current=new DisconnectFromGame();
      }
      if(mode=="host" && connected==int.Parse(Environment.GetEnvironmentVariable("DUCK_FRIENDS_TEST_PLAYERS")??"4") && DuckNetwork.status==DuckNetStatus.Connected && Level.current is TeamSelect2 && Environment.GetEnvironmentVariable("DUCK_FRIENDS_TEST_MATCH")=="1" && ++readySeconds==10)
       ((TeamSelect2)Level.current).forcestart=true;
     }
    });
   }
  }){IsBackground=true}.Start();
  Program.Main(test?new[]{"-nosteam","-startinlobby","-nofullscreen","-nomusic"}:new[]{"-nosteam","-startinlobby"});
 }
}
