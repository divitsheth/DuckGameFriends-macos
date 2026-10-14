using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using DuckGame;
class FriendsBootstrap {
 static string mode, room;
 static bool started;
 static void Configure() {
  if(started || !(Level.current is TeamSelect2))return;
  started=true;
  DGRSettings.MidGameJoining=false;
  TeamSelect2.GetOnlineSetting("maxplayers").value=4;
  // Replace the LAN transport with the relay before any socket is opened.
  var network=Network.activeNetwork;
  typeof(Network).GetField("_lanCore",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(network,new NCRelay(network,network.networkIndex));
  NCRelay.room=room;
  NCRelay.hosting=mode=="host";
  if(mode=="host") {
   TeamSelect2.GetOnlineSetting("name").value="Duck Game Friends";
   TeamSelect2.GetOnlineSetting("password").value=room;
   TeamSelect2.GetOnlineSetting("type").value=(int)NetworkLobbyType.LAN;
   TeamSelect2.GetOnlineSetting("dedicated").value=false;
   TeamSelect2.FillMatchmakingProfiles();
   DuckNetwork.Host(4,NetworkLobbyType.LAN,true);
   ((TeamSelect2)Level.current).PrepareForOnline();
   Console.WriteLine("FRIENDS_HOST_STARTED room="+room);
  } else if(mode=="join") {
   var join=new JoinServer(NCRelay.SlotEndPoint(0).ToString());
   // The pinned upstream build has no public LAN+password constructor.
   var field=typeof(JoinServer).GetField("password",BindingFlags.Instance|BindingFlags.NonPublic);
   if(field==null)throw new InvalidOperationException("Incompatible game: LAN password field missing.");
   field.SetValue(join,room);
   Level.current=join;
   Console.WriteLine("FRIENDS_JOIN_STARTED");
  }
 }
 static void Main(string[] args) {
  mode=args.Length>0?args[0]:"local";
  if(mode!="local") {
   // The room code is both the relay room and the game's lobby password.
   room=Environment.GetEnvironmentVariable("DUCK_FRIENDS_ROOM")??"";
   if(room.Length!=8)throw new ArgumentException("DUCK_FRIENDS_ROOM must be an 8-character room code.");
   var relay=(Environment.GetEnvironmentVariable("DUCK_FRIENDS_RELAY")??"").Split(':');
   if(relay.Length!=2)throw new ArgumentException("DUCK_FRIENDS_RELAY must be host:port.");
   var ip=Dns.GetHostAddresses(relay[0]).First(a=>a.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork);
   NCRelay.relay=new IPEndPoint(ip,int.Parse(relay[1]));
  }
  MonoMain.IgnoreDGRUpdates=true;
  bool test=Environment.GetEnvironmentVariable("DUCK_FRIENDS_TEST")=="1";
  new Thread(()=>{
   int readySeconds=0;
   for(int i=0;i<180;i++) {
    Thread.Sleep(1000);
    Tasker.Task(()=>{
     if(mode!="local")Configure();
     if(test) {
      int connected=DuckNetwork.profiles.Count(p=>p.connection!=null);
      Console.WriteLine("FRIENDS_STATE mode="+mode+" status="+DuckNetwork.status+" level="+(Level.current==null?"null":Level.current.GetType().Name)+" connected="+connected+" slots="+DuckNetwork.numSlots+" ducks="+(Level.current==null?0:Level.current.things.OfType<Duck>().Count()));
      if(mode=="host" && connected==4 && DuckNetwork.status==DuckNetStatus.Connected && Level.current is TeamSelect2 && Environment.GetEnvironmentVariable("DUCK_FRIENDS_TEST_MATCH")=="1" && ++readySeconds==10)
       ((TeamSelect2)Level.current).forcestart=true;
     }
    });
    if(!test && started)break;
   }
  }){IsBackground=true}.Start();
  Program.Main(test?new[]{"-nosteam","-startinlobby","-nofullscreen","-nomusic"}:new[]{"-nosteam","-startinlobby"});
 }
}
