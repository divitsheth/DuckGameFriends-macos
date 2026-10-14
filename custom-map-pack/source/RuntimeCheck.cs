using System;
using System.Linq;
using System.Threading;
using DuckGame;
class RuntimeCheck {
 static void Main(string[] args) {
  var timer = new Thread(() => {
   Thread.Sleep(12000);
   try {
    var level = Level.current;
    var objects = level.things.ToArray();
    var ducks = objects.OfType<Duck>().ToArray();
    Console.WriteLine("RUNTIME_CHECK level="+level.GetType().Name+" objects="+objects.Length+" ducks="+ducks.Length);
    foreach(var d in ducks) Console.WriteLine("DUCK x="+d.x+" y="+d.y+" dead="+d.dead);
    bool pass = level is DuckGameTestArea && ducks.Length == 4 && ducks.All(d => !d.dead);
    Console.WriteLine(pass ? "RUNTIME_PASS" : "RUNTIME_FAIL");
    Environment.Exit(pass ? 0 : 1);
   } catch(Exception e) { Console.WriteLine(e); Environment.Exit(2); }
  });
  timer.IsBackground=true; timer.Start();
  Program.Main(args);
 }
}
