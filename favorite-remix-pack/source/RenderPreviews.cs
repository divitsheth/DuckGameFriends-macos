using System;
using System.IO;
using System.Linq;
using System.Threading;
using DuckGame;
class RenderPreviews {
 static void Main(string[] args) {
  string mapDirectory = Path.GetFullPath(args[0]);
  string previewDirectory = Path.GetFullPath(args[1]);
  Directory.CreateDirectory(previewDirectory);
  new Thread(() => {
   Thread.Sleep(6000);
   foreach (var file in Directory.GetFiles(mapDirectory, "*.lev").OrderBy(p => p)) {
    Tasker.Task(() => {
     var data = DuckFile.LoadLevel(File.ReadAllBytes(file));
     var preview = Content.GeneratePreview(data, true);
     using (var image = new MemoryStream()) {
      preview.preview.SaveAsPng(image, preview.preview.Width, preview.preview.Height);
      File.WriteAllBytes(Path.Combine(previewDirectory, Path.GetFileNameWithoutExtension(file) + ".png"), image.ToArray());
      data.previewData.preview = Convert.ToBase64String(image.ToArray());
     }
     // Serialize with explicit type names, independent of the running mod registry.
     bool mods = MonoMain.moddingEnabled; MonoMain.moddingEnabled = false;
     data.SetExtraHeaderInfo(data.metaData);
     var bytes = data.Serialize();
     using (var stream = File.Create(file)) stream.Write(bytes.buffer, 0, bytes.lengthInBytes);
     MonoMain.moddingEnabled = mods;
     Console.WriteLine("RENDERED " + Path.GetFileName(file));
    }).WaitForComplete();
   }
   Tasker.Task(() => MonoMain.instance.Exit());
  }) { IsBackground = true }.Start();
  Program.Main(new[] { "-nosteam", "-nofullscreen", "-startineditor" });
 }
}
