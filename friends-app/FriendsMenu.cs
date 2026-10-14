using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using DuckGame;
// The first screen of Duck Game Friends: host a room, join one with a code, or
// play locally. Drawn with the game's own menu box, font, logo and duck, and
// driven by keyboard or controller like the rest of Duck Game.
class FriendsMenu : Level {
 enum Screen { Menu, Join, Busy }
 enum RoomStatus { Open, Missing, Full, Unreachable }
 // Room codes avoid look-alikes (no 0/O, 1/I).
 public const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
 static readonly string[] Items = { "HOST A ROOM", "JOIN A ROOM", "PLAY LOCALLY", "HOW TO PLAY", "QUIT" };
 static readonly Color Gold = new Color(242,201,76), Dim = new Color(141,153,174), Bad = new Color(235,87,87);

 Screen screen = Screen.Menu;
 int selected;
 string code = "", message = "", messageLine2 = "";
 Color messageColor = Dim;
 Task<RoomStatus> pending;
 Action<RoomStatus> then;
 float blink;
 Sprite background, title;
 SpriteMap box, duck;

 public FriendsMenu() { _centeredView = true; }

 public override void Initialize() {
  background = new Sprite("mainBackground");
  title = new Sprite("duckGameTitle");
  box = new SpriteMap("uiBox", 10, 10);
  duck = new SpriteMap("duck", 32, 32);
  Graphics.fade = 1f;
  ShowMenu("");
  Console.WriteLine("FRIENDS_MENU_SHOWN");
  base.Initialize();
 }

 void Hints(string left, string right) {
  HUD.CloseAllCorners();
  HUD.AddCornerMessage(HUDCorner.BottomLeft, left);
  if (right != null) HUD.AddCornerMessage(HUDCorner.BottomRight, right);
 }
 void ShowMenu(string error) {
  screen = Screen.Menu; Input._imeAllowed = false;
  Say(error, "", Bad);
  Hints("@SELECT@SELECT", null);
 }
 void ShowJoin(string error) {
  screen = Screen.Join;
  // A code copied from a message is filled in for you.
  if (code == "") { var clip = Normalize(Clipboard.Read()); if (clip.Length == 8) code = clip; }
  Keyboard.KeyString = code;
  Say(error == "" ? "TYPE THE CODE YOUR" : error, error == "" ? "FRIEND SENT YOU" : "", error == "" ? Dim : Bad);
  Hints("@ENTERKEY@JOIN", "@ESCAPEKEY@BACK");
 }
 void Say(string a, string b, Color c) { message = a; messageLine2 = b; messageColor = c; }
 void Check(string room, string busyText, Action<RoomStatus> next) {
  screen = Screen.Busy; Input._imeAllowed = false;
  Say(busyText, "", Dim); HUD.CloseAllCorners();
  pending = Task.Run(() => Query(room)); then = next;
 }

 public static string Normalize(string s) => new string((s ?? "").ToUpperInvariant().Where(c => Alphabet.IndexOf(c) >= 0).Take(8).ToArray());

 // DUCK_FRIENDS_TEST_MENU=host|join presses through the menu for automated runs.
 // With DUCK_FRIENDS_TEST_LEAVE=1 the host leaves its lobby and hosts once more.
 static int scriptRuns;
 string script = scriptRuns++ < (Environment.GetEnvironmentVariable("DUCK_FRIENDS_TEST_LEAVE") == "1" ? 2 : 1) ? Environment.GetEnvironmentVariable("DUCK_FRIENDS_TEST_MENU") : null;
 int frames;
 public override void Update() {
  blink += 0.03f;
  if (script != null && ++frames == 180) {
   Console.WriteLine("FRIENDS_MENU_SCRIPT " + script);
   if (script == "host") { selected = 0; Choose(); }
   else if (script == "join") { selected = 1; Choose(); var room = code; Check(room, "CHECKING THE CODE...", r => Joined(room, r)); }
   script = null;
  }
  if (pending != null) {
   if (pending.IsCompleted) { var r = pending.Result; pending = null; then(r); }
  } else if (screen == Screen.Menu) {
   if (Input.Pressed(Triggers.MenuUp) || Input.Pressed(Triggers.Up)) { selected = (selected + Items.Length - 1) % Items.Length; SFX.Play("textLetter", 0.7f); }
   if (Input.Pressed(Triggers.MenuDown) || Input.Pressed(Triggers.Down)) { selected = (selected + 1) % Items.Length; SFX.Play("textLetter", 0.7f); }
   if (Input.Pressed(Triggers.Select) || Keyboard.Pressed(Keys.Enter)) { SFX.Play("rockHitGround", 0.8f); Choose(); }
  } else if (screen == Screen.Join) {
   Input._imeAllowed = true; Keyboard.repeat = true;
   bool command = Keyboard.Down(Keys.LeftWindows) || Keyboard.Down(Keys.RightWindows) || Keyboard.control;
   if (command && Keyboard.Pressed(Keys.V)) Keyboard.KeyString = Clipboard.Read();
   var typed = Normalize(Keyboard.KeyString);
   if (typed != code) { code = typed; SFX.Play("textLetter", 0.7f); }
   Keyboard.KeyString = code;
   // E and P are cancel on the keyboard layouts but are also code letters.
   bool back = Keyboard.Pressed(Keys.Escape) || Input.Pressed(Triggers.Cancel) && !Keyboard.Down(Keys.E) && !Keyboard.Down(Keys.P);
   if (back) { Keyboard.repeat = false; code = ""; ShowMenu(""); }
   else if (Keyboard.Pressed(Keys.Enter) || Input.Pressed(Triggers.Select)) {
    if (code.Length < 8) Say("THE CODE HAS 8 LETTERS", "", Bad);
    else { Keyboard.repeat = false; var room = code; Check(room, "CHECKING THE CODE...", r => Joined(room, r)); }
   }
  }
  base.Update();
 }

 void Choose() {
  switch (selected) {
   case 0: Host(3); break;
   case 1: code = ""; ShowJoin(""); break;
   case 2: FriendsBootstrap.StartLocal(); break;
   case 3: var guide = Environment.GetEnvironmentVariable("DUCK_FRIENDS_GUIDE"); if (guide != null) Process.Start("open", "\"" + guide + "\""); break;
   case 4: MonoMain.exit = true; break;
  }
 }
 void Host(int tries) {
  var bytes = new byte[8];
  using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create()) rng.GetBytes(bytes);
  var room = new string(bytes.Select(b => Alphabet[b % Alphabet.Length]).ToArray());
  Check(room, "CREATING YOUR ROOM...", r => {
   if (r == RoomStatus.Missing) { Clipboard.Write(Pretty(room)); FriendsBootstrap.StartOnline("host", room); }
   else if (r == RoomStatus.Unreachable) ShowMenu("CAN'T REACH THE SERVER");
   else if (tries > 1) Host(tries - 1);
   else ShowMenu("PLEASE TRY AGAIN");
  });
 }
 void Joined(string room, RoomStatus r) {
  if (r == RoomStatus.Open) FriendsBootstrap.StartOnline("join", room);
  else ShowJoin(r == RoomStatus.Missing ? "NO ROOM WITH THAT CODE" : r == RoomStatus.Full ? "THAT ROOM IS FULL" : "CAN'T REACH THE SERVER");
 }
 public static string Pretty(string room) => room.Substring(0, 4) + "-" + room.Substring(4);

 // Asks the relay whether a room exists and has a free slot. Protocol: relay/relay.js.
 static RoomStatus Query(string room) {
  try {
   using (var udp = new UdpClient()) {
    udp.Client.ReceiveTimeout = 1000;
    var q = new byte[9]; q[0] = 0x51; System.Text.Encoding.ASCII.GetBytes(room, 0, 8, q, 1);
    for (int i = 0; i < 3; i++) {
     udp.Send(q, q.Length, NCRelay.relay);
     try {
      var from = new IPEndPoint(IPAddress.Any, 0);
      var r = udp.Receive(ref from);
      if (r.Length == 10 && r[0] == 0x52 && r.Skip(2).SequenceEqual(q.Skip(1)))
       return r[1] == 0 ? RoomStatus.Open : r[1] == 1 ? RoomStatus.Missing : RoomStatus.Full;
     } catch (SocketException) { }
    }
   }
  } catch (Exception e) { Console.WriteLine("FRIENDS_QUERY_FAILED " + e.Message); }
  return RoomStatus.Unreachable;
 }

 // Drawing, in the 320x180 HUD space.
 public static void Box(SpriteMap box, float x, float y, float w, float h, Depth depth) {
  box.alpha = 1f;
  Action<int,float,float,float,float> Part = (frame, px, py, sx, sy) => { box.frame = frame; box.xscale = sx; box.yscale = sy; Graphics.Draw(box, px, py, depth); };
  float iw = (w - 20) / 10f, ih = (h - 20) / 10f;
  Part(0, x, y, 1, 1); Part(1, x + 10, y, iw, 1); Part(2, x + w - 10, y, 1, 1);
  Part(3, x, y + 10, 1, ih); Part(4, x + 10, y + 10, iw, ih); Part(5, x + w - 10, y + 10, 1, ih);
  Part(6, x, y + h - 10, 1, 1); Part(7, x + 10, y + h - 10, iw, 1); Part(8, x + w - 10, y + h - 10, 1, 1);
  box.xscale = box.yscale = 1f;
 }
 static void Centered(string s, float cx, float y, Color c, float scale = 1f) =>
  Graphics.DrawString(s, new Vec2(cx - Graphics.GetStringWidth(s) * scale / 2f, y), c, (Depth)0.9f, null, scale);

 public override void Draw() {
  Graphics.Draw(background, 0f, 0f, (Depth)0.1f);
  if (screen == Screen.Menu) {
   Graphics.Draw(title, 4f, 6f, (Depth)0.3f);
   Centered("FRIENDS", 95f, 100f, Gold);
   duck.frame = 0;
   duck.flipH = false; Graphics.Draw(duck, 40f, 118f, (Depth)0.3f);
   duck.flipH = true; Graphics.Draw(duck, 172f, 118f, (Depth)0.3f);
   duck.flipH = false;
   Box(box, 184f, 22f, 128f, 122f, (Depth)0.5f);
   Centered("PLAY", 248f, 32f, Gold);
   for (int i = 0; i < Items.Length; i++) {
    float y = 50f + i * 16f;
    if (i == selected) Graphics.DrawString(">", new Vec2(194f, y), Color.White, (Depth)0.9f);
    Graphics.DrawString(Items[i], new Vec2(206f, y), i == selected ? Color.White : Dim, (Depth)0.9f);
   }
   if (message != "") Centered(message, 160f, 152f, messageColor);
   return;
  }
  Box(box, 40f, 26f, 240f, 112f, (Depth)0.5f);
  if (screen == Screen.Busy) { Centered(message, 160f, 78f, Gold); return; }
  Centered("ENTER ROOM CODE", 160f, 40f, Gold);
  float x = 160f - 9 * 16f / 2f;
  for (int i = 0; i < 8; i++) {
   if (i == 4) { Graphics.DrawString("-", new Vec2(x, 62f), Dim, (Depth)0.9f, null, 2f); x += 16f; }
   if (i < code.Length) Graphics.DrawString(code[i].ToString(), new Vec2(x, 62f), Color.White, (Depth)0.9f, null, 2f);
   else if (i != code.Length || blink % 1f < 0.6f) Graphics.DrawRect(new Vec2(x + 1f, 78f), new Vec2(x + 15f, 80f), i == code.Length ? Gold : new Color(74,85,104), (Depth)0.9f);
   x += 16f;
  }
  Centered(message, 160f, 98f, messageColor);
  if (messageLine2 != "") Centered(messageLine2, 160f, 110f, messageColor);
 }
}

// The room code, pinned to the top of the lobby so it can be read out on a call.
// Drawn through the engine hook rather than as a level object, which online play would try to sync.
class RoomBanner : IEngineUpdatable {
 SpriteMap box;
 public void PreUpdate() { }
 public void Update() { }
 public void PostUpdate() { }
 public void OnDrawLayer(Layer layer) {
  if (layer != Layer.HUD || NCRelay.room == null || !Network.isActive || !(Level.current is TeamSelect2)) return;
  box = box ?? new SpriteMap("uiBox", 10, 10);
  var text = "ROOM " + FriendsMenu.Pretty(NCRelay.room);
  float w = Graphics.GetStringWidth(text) + 24f;
  float x = Layer.HUD.camera.width / 2f - w / 2f;
  FriendsMenu.Box(box, x, 2f, w, 20f, (Depth)0.95f);
  Graphics.DrawString(text, new Vec2(x + 12f, 8f), Color.White, (Depth)0.99f);
 }
}

static class Clipboard {
 static string Run(string tool, string input) {
  try {
   var p = Process.Start(new ProcessStartInfo("/usr/bin/" + tool) { UseShellExecute = false, RedirectStandardInput = input != null, RedirectStandardOutput = input == null });
   if (input != null) { p.StandardInput.Write(input); p.StandardInput.Close(); p.WaitForExit(2000); return ""; }
   var s = p.StandardOutput.ReadToEnd(); p.WaitForExit(2000); return s;
  } catch { return ""; }
 }
 public static string Read() => Run("pbpaste", null);
 public static void Write(string s) => Run("pbcopy", s);
}
