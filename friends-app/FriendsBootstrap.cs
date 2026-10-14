using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using DuckGame;
class FriendsBootstrap {
 static string mode, address, password;
 static bool started;
 static void Configure() {
  if(started || !(Level.current is TeamSelect2))return;
  started=true;
  DGRSettings.MidGameJoining=false;
  TeamSelect2.GetOnlineSetting("maxplayers").value=4;
  if(mode=="host") {
   TeamSelect2.GetOnlineSetting("name").value="Duck Game Friends";
   TeamSelect2.GetOnlineSetting("password").value=password;
   TeamSelect2.GetOnlineSetting("type").value=(int)NetworkLobbyType.LAN;
   TeamSelect2.GetOnlineSetting("dedicated").value=false;
   TeamSelect2.FillMatchmakingProfiles();
   DuckNetwork.Host(4,NetworkLobbyType.LAN,true);
   ((TeamSelect2)Level.current).PrepareForOnline();
   Console.WriteLine("FRIENDS_HOST_STARTED slots=4 port=1337");
  } else if(mode=="join") {
   var join=new JoinServer(address);
   // The pinned upstream build has no public LAN+password constructor.
   var field=typeof(JoinServer).GetField("password",BindingFlags.Instance|BindingFlags.NonPublic);
   if(field==null)throw new InvalidOperationException("Incompatible game: LAN password field missing.");
   field.SetValue(join,password);
   Level.current=join;
   Console.WriteLine("FRIENDS_JOIN_STARTED");
  }
 }
 static void Main(string[] args) {
  mode=args.Length>0?args[0]:"local";
  address=Environment.GetEnvironmentVariable("DUCK_FRIENDS_ADDRESS")??"127.0.0.1:1337";
  password=Environment.GetEnvironmentVariable("DUCK_FRIENDS_PASSWORD")??"";
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
