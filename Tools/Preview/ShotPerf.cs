using System; using System.Diagnostics; using System.Linq; using Arena; using UnityEngine;
// PERFRUN=<seconds>[,boss]: Quick Play with that boss's approach (default the Twin Reapers) on an Expanding map with two
// rooms, the bots playing from the wave given; every frame drawn, and every 2 s the enemies, particles, GL vertices
// and draw time printed, with the top vertex sources of one traced frame.
static partial class Shot {
  static void PerfRunSetup() {
    string env = Environment.GetEnvironmentVariable("PERFRUN");
    if (env == null) return;
    var parts = env.Split(',');
    host.QuickMode = true;
    QuickPlay.OrderOverride = new[] { parts.Length > 1 ? (EnemyType)Enum.Parse(typeof(EnemyType), parts[1]) : EnemyType.TwinA, EnemyType.Boss, EnemyType.HiveQueen };
  }
  static void PerfRun() {
    string env = Environment.GetEnvironmentVariable("PERFRUN");
    if (env == null) return;
    int secs = int.Parse(env.Split(',')[0]);
    int idleFor = int.Parse(Environment.GetEnvironmentVariable("PERFIDLE") ?? "0");
    var g = host.Game;
    string why; var rooms = new System.Collections.Generic.List<ExpansionRoom>();
    foreach (var box in new[] { new[] { 1600, 100, 800, 600 }, new[] { -900, 100, 800, 600 } }) { var r = Expansion.Custom(rooms, box[0], box[1], box[2], box[3], out why); if (r.HasValue) rooms.Add(r.Value); }
    foreach (var gg in new[] { g }.Concat(others.Select(o => o.Game))) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Expanded.Clear(); gg.Expanded.AddRange(rooms); }
    var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
    var partN = typeof(ArenaApp).GetField("partN", f2);
    var sw = new Stopwatch(); double ms = 0, worst = 0; long verts = 0; int maxV = 0, maxP = 0, frames = 0, kills0 = 0;
    Console.WriteLine("PERFRUN quick {0} wave {1}", g.Quick, g.Wave);
    for (int i = 0; i < secs * 60; i++) {
      bool idle = i < idleFor * 60;  // PERFIDLE=<s>: the bots stand still that long, so a crowd builds
      g.Bot = !idle; foreach (var c in others) c.Game.Bot = !idle;
      Step(host); foreach (var c in others) Step(c);
      foreach (var p in g.Players.Values) { p.InvulnT = 1f; p.Hp = p.MaxHp; p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PrizePicks = p.PendingMilestones = 0; p.Offer = null; p.LegendaryQueue.Clear(); }
      Time.time += 1f / 60f; Time.unscaledTime += 1f / 60f; Time.deltaTime = Time.unscaledDeltaTime = 1f / 60f; Time.frameCount++;
      bool trace = i % 120 == 119;
      var by = new System.Collections.Generic.Dictionary<string, int>();
      if (trace) GL.Trace = () => { var st = new StackTrace(2, false); string key = ""; for (int fi = 0; fi < Math.Min(6, st.FrameCount); fi++) key += st.GetFrame(fi).GetMethod().Name + " < "; by[key] = by.TryGetValue(key, out int c0) ? c0 + 1 : 1; };
      GL.Tris.Clear(); GUI.Log.Clear(); Event.current = new Event { mousePosition = new Vector2 { x = -100, y = -100 } };
      typeof(ArenaApp).GetField("holdT", f2).SetValue(app, 0f);
      sw.Restart(); draw.Invoke(app, null); sw.Stop(); GL.Trace = null;
      if (!trace) { ms += sw.Elapsed.TotalMilliseconds; worst = Math.Max(worst, sw.Elapsed.TotalMilliseconds); frames++; }
      int v = GL.Tris.Count / 6; verts += v; maxV = Math.Max(maxV, v); maxP = Math.Max(maxP, (int)partN.GetValue(app));
      if (trace) {
        int kills = g.Players.Values.Sum(p => p.Run.Kills);
        Console.WriteLine("t {0,4:0}s wave {1,2} {2,-12} breaches {11} enemies {3,3} bullets {4,4} kills/2s {5,3} | verts avg {6,6} max {7,6} | particles max {8,4} | draw {9,5:0.0} ms avg {10,5:0.0} worst",
          i / 60f, g.Wave, g.State, g.Enemies.Count, g.Bullets.Count, kills - kills0, verts / 120, maxV, maxP, ms / Math.Max(1, frames), worst, g.BreachesOn ? g.ActiveBreaches.Length : 0);
        foreach (var kv in by.OrderByDescending(kv => kv.Value).Take(6)) Console.WriteLine("    {0,6} {1}", kv.Value, kv.Key);
        kills0 = kills; ms = worst = 0; verts = 0; maxV = maxP = frames = 0;
      }
    }
    Environment.Exit(0);
  }
}
