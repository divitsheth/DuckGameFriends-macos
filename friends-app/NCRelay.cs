using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using DuckGame;
// Routes the original LAN transport through the Duck Game Friends relay.
//
// The game still believes it is on a LAN, but each player is addressed by a
// virtual endpoint for their room slot (10.77.0.1 is the host, .2-.8 guests).
// Every packet goes to the relay, which forwards it to the player in that slot,
// so nobody needs a VPN, port forwarding, or each other's real address.
// Protocol: relay/relay.js.
class NCRelay : NCBasic {
 public static IPEndPoint relay;
 public static string room;
 public static bool hosting;
 public static string failure;
 public const int Slots=8;
 public static IPEndPoint SlotEndPoint(int slot) => new IPEndPoint(new IPAddress(new byte[]{10,77,0,(byte)(slot+1)}),1337);
 static int SlotOf(IPEndPoint e) {
  var b=e.Address.GetAddressBytes();
  return b.Length==4 && b[0]==10 && b[1]==77 && b[2]==0 && b[3]>=1 && b[3]<=Slots ? b[3]-1 : -1;
 }
 readonly byte[] nonce=new byte[8];
 byte[] token;
 int slot=-1;
 DateTime lastJoin=DateTime.MinValue, lastKeepalive=DateTime.MinValue, lastFrame=DateTime.MinValue;
 double worstFrameMs; // the longest wait between network updates since the last report
 public NCRelay(Network c,int networkIndex):base(c,networkIndex) {
  using(var rng=RandomNumberGenerator.Create())rng.GetBytes(nonce);
 }
 public override NCError OnSendPacket(byte[] data,int length,object connection) {
  if(token==null||_socket==null)return null; // heartbeats are resent until the relay seats us
  int to=SlotOf((IPEndPoint)connection);
  if(to<0)return null;
  var lan=new byte[8+length]; // the exact datagram NCBasic would have sent
  var bits=new BitBuffer(lan,false);
  bits.Write(kLanMessageHeader);
  if(data!=null)bits.Write(data,length:length);
  var buffer=new byte[10+lan.Length];
  buffer[0]=0x44;
  Buffer.BlockCopy(token,0,buffer,1,8);
  buffer[9]=(byte)to;
  Buffer.BlockCopy(lan,0,buffer,10,lan.Length);
  _socket.Send(buffer,buffer.Length,relay);
  bytesThisFrame+=length+8;
  return null;
 }
 protected override void ReceivePackets(Queue<NCBasicPacket> packets) {
  if(_socket==null)return;
  var now=DateTime.UtcNow;
  if(lastFrame!=DateTime.MinValue)worstFrameMs=Math.Max(worstFrameMs,(now-lastFrame).TotalMilliseconds);
  lastFrame=now;
  try {
   if(token==null && failure==null && (now-lastJoin).TotalMilliseconds>500) {
    lastJoin=now;
    var join=new byte[19]; // the last byte is the room size; guests' is ignored
    join[0]=0x4A; join[1]=(byte)(hosting?0:1);
    System.Text.Encoding.ASCII.GetBytes(room,0,8,join,2);
    Buffer.BlockCopy(nonce,0,join,10,8);
    join[18]=Slots;
    _socket.Send(join,join.Length,relay);
   }
   if(token!=null && (now-lastKeepalive).TotalSeconds>5) {
    lastKeepalive=now;
    var keep=new byte[9]; keep[0]=0x4B; Buffer.BlockCopy(token,0,keep,1,8);
    _socket.Send(keep,keep.Length,relay);
   }
   IPEndPoint from=null;
   while(_socket.Available>0) {
    var msg=_socket.Receive(ref from);
    if(msg==null||msg.Length<2||!from.Equals(relay))continue; // ignore LAN broadcasts and strangers
    if(msg[0]==0x44 && msg[1]<Slots) {
     var data=new byte[msg.Length-2];
     Buffer.BlockCopy(msg,2,data,0,data.Length);
     packets.Enqueue(new NCBasicPacket{data=data,sender=SlotEndPoint(msg[1])});
    } else if(msg[0]==0x50 && msg.Length==5 && token!=null) {
     var report=System.Text.Encoding.UTF8.GetBytes(Report());
     var pong=new byte[13+report.Length];
     pong[0]=0x4F; Buffer.BlockCopy(token,0,pong,1,8); Buffer.BlockCopy(msg,1,pong,9,4);
     Buffer.BlockCopy(report,0,pong,13,report.Length);
     _socket.Send(pong,pong.Length,relay);
    } else if(msg[0]==0x41 && msg.Length==18 && token==null && msg.Skip(10).SequenceEqual(nonce)) {
     slot=msg[1]; token=msg.Skip(2).Take(8).ToArray();
     Console.WriteLine("FRIENDS_RELAY_SEATED room="+room+" slot="+slot);
    } else if(msg[0]==0x45 && msg.Length==10 && token==null && msg.Skip(2).SequenceEqual(nonce)) {
     failure=msg[1]==1?"no-room":msg[1]==2?"room-full":"code-taken";
     Console.WriteLine("FRIENDS_RELAY_FAILED "+failure);
    }
   }
  } catch(SocketException e) {
   Console.WriteLine("FRIENDS_RELAY_SOCKET "+e.SocketErrorCode);
  }
 }
 // How this game sees the match, sent with each answer to the relay's ping (see relay/relay.js):
 // its name, its updates per second and longest stall, and the game's own ping (averaged over its last 32) and lost packets per player.
 string Report() {
  try {
   var conns=string.Join(",",Network.activeNetwork.core.connections.Select(c=>{
    int s=c.data is IPEndPoint e?SlotOf(e):-1; var m=c.manager;
    return "{\"slot\":"+s+",\"ping\":"+(int)(m.ping*1000)+",\"peak\":"+(int)(m.pingPeak*1000)+",\"lost\":"+m.losses+",\"sent\":"+m.sent+"}";
   }));
   var name=new string((DuckNetwork.localProfile?.name??"").Where(ch=>ch>=' '&&ch!='"'&&ch!='\\').Take(32).ToArray());
   var os=Environment.OSVersion.Platform==PlatformID.Win32NT?"windows":"mac";
   var r="{\"name\":\""+name+"\",\"os\":\""+os+"\",\"slot\":"+slot+",\"fps\":"+Graphics.fps+",\"worstFrame\":"+(int)worstFrameMs+",\"conns\":["+conns+"]}";
   worstFrameMs=0;
   return r;
  } catch(Exception e) { return "{\"error\":\""+e.GetType().Name+"\"}"; }
 }
}
