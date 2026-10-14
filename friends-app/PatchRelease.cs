using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
// Apply the matching NCBasic.cs fix to the pinned macOS release assembly.
// Fail closed if the expected release IL changes; never modify the installed app.
class PatchRelease {
 static void Main(string[] a) {
  var resolver=new DefaultAssemblyResolver();
  resolver.AddSearchDirectory(System.IO.Path.GetDirectoryName(a[0]));
  var asm=AssemblyDefinition.ReadAssembly(a[0],new ReaderParameters{AssemblyResolver=resolver});
  var m=asm.MainModule.GetType("DuckGame.NCBasic").Methods.Single(x=>x.Name=="OnJoinServer");
  var call=m.Body.Instructions.Single(x=>x.Operand is MethodReference && ((MethodReference)x.Operand).Name=="GetActiveUdpListeners");
  var port=m.Body.Instructions.Single(x=>x.OpCode==OpCodes.Ldc_I4 && (int)x.Operand==1336);
  var endpoint=((ArrayType)((MethodReference)call.Operand).ReturnType).ElementType;
  var il=m.Body.GetILProcessor();
  il.InsertBefore(call,il.Create(OpCodes.Pop));
  il.InsertBefore(call,il.Create(OpCodes.Ldc_I4_0));
  call.OpCode=OpCodes.Newarr;call.Operand=endpoint;
  port.OpCode=OpCodes.Ldc_I4_0;port.Operand=null;
  var mid=asm.MainModule.GetType("DuckGame.DuckNetwork").Methods.Single(x=>x.Name=="MidGameJoiningLogic");
  var first=mid.Body.Instructions[0];var mi=mid.Body.GetILProcessor();
  mi.InsertBefore(first,mi.Create(OpCodes.Ldsfld,asm.MainModule.GetType("DuckGame.Network").Fields.Single(f=>f.Name=="lanMode")));
  mi.InsertBefore(first,mi.Create(OpCodes.Brfalse,first));
  mi.InsertBefore(first,mi.Create(OpCodes.Ret));
  asm.Write(a[1]);
  Console.WriteLine("Patched LAN client: OS-assigned UDP port; no unsupported listener enumeration.");
 }
}
