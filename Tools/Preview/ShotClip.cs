using System; using System.IO; using System.Threading; using Arena; using UnityEngine;
// CLIP=<frames>: after the posed frame, the match keeps running and every CLIPSTEP fixed steps (default 3, 20 fps)
// another frame is drawn, as <name>.f001.tris/.ui ... (clip.sh turns them into a GIF). Frame 0 is the normal shot.
static partial class Shot {
  static void ClipFrames() {
    string n = Environment.GetEnvironmentVariable("CLIP");
    if (n == null) return;
    int frames = int.Parse(n), step = int.Parse(Environment.GetEnvironmentVariable("CLIPSTEP") ?? "3");
    bool hold = Environment.GetEnvironmentVariable("CLIPHURT") == null;  // as the setup: nobody gets hurt (CLIPHURT=1: they do)
    var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2); var edges = typeof(ArenaApp).GetMethod("DrawCabEdges", f2);
    for (int i = 1; i < frames; i++) {
      for (int k = 0; k < step; k++) {
        Step(host); foreach (var c in others) Step(c);
        if (hold) foreach (var p in host.Game.Players.Values) p.InvulnT = 1f;
        Time.time += 1f / 60f; Time.unscaledTime += 1f / 60f;
      }
      Time.deltaTime = Time.unscaledDeltaTime = step / 60f; Time.frameCount++;
      GL.Tris.Clear(); GUI.Log.Clear();
      Event.current = new Event { mousePosition = Event.current.mousePosition };
      draw.Invoke(app, null);
      DrawMode();
      Event.current.type = EventType.Repaint; edges.Invoke(app, null);
      string f = args[0] + ".f" + i.ToString("000");
      using (var w = new BinaryWriter(File.Create(f + ".tris"))) foreach (var v in GL.Tris) w.Write(v);
      File.WriteAllLines(f + ".ui", GUI.Log.ToArray());
    }
  }
}
