using System; using UnityEngine;
using System.Reflection;
using Arena;

// LIGHTING=1: dynamic lighting and shadows (Game/LightingLook.cs) on the Pillar Ring: a Spinner in the middle throwing a
// ring of shots out past the eight pillars (their shadows fan out), Grunts round you, your shots, a crate and a barrel,
// a blast going off by a pillar and a Flame Wall in the corner. LIGHTING=2: the same moment a little later. LIGHTING=3: the effects' lights instead of the shots (a Railgun beam, a
// lightning chain, a frost burst, a muzzle flash). LIGHTS=0 the
// lighting off. Use with mode hud, e.g. LIGHTING=1 shot.sh x hud 3 1 1.
static partial class Shot {
  static void LightingScene() {
    string ls = Environment.GetEnvironmentVariable("LIGHTING");
    if (ls == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var addZone = typeof(Game).GetMethod("AddZone", flags);
    var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    gm.Enemies.Clear(); gm.Props.Clear();
    gm.RoomsUp = false; gm.DevSetLayout(LayoutId.PillarRing); gm.LayoutAge = 99f;
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 home = new V2(420f, 450f), mid = new V2(800f, 450f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(330f, 300f);
    me.Pos = me.Disp = home; me.Aim = 0f;
    spawn.Invoke(gm, new object[] { EnemyType.Spinner, mid, 0f, 0, false });
    foreach (var at in new[] { new V2(560f, 330f), new V2(590f, 560f), new V2(1060f, 250f), new V2(1120f, 640f) })
      spawn.Invoke(gm, new object[] { EnemyType.Grunt, at, 0f, 0, false });
    foreach (var e in gm.Enemies.Values) { e.Spawning = false; e.SpawnT = 0f; e.Disp = e.Pos; }
    gm.Props[9001] = new Prop { Id = 9001, Kind = PropKind.Crate, Pos = new V2(390f, 250f) };
    gm.Props[9002] = new Prop { Id = 9002, Kind = PropKind.Barrel, Pos = new V2(1250f, 420f) };
    gm.Props[9003] = new Prop { Id = 9003, Kind = PropKind.Chest, Pos = new V2(1300f, 220f) };
    int ring = ls == "2" ? 2 : 1;
    for (int r = 0; r < ring; r++)
      for (int k = 0; k < 16; k++) {  // the Spinner's ring, out past the pillars
        float a = MathX.Tau * (k + 0.5f * r) / 16f, d = 70f + 150f * (ring - r);
        shoot.Invoke(gm, new object[] { Defs.BigShot, mid + V2.FromAngle(a) * d, V2.FromAngle(a) * 200f, 0f, 9f, 8f, -1, false, 0f });
      }
    for (int k = 0; k < 5; k++)  // your stream at the nearest Grunt
      shoot.Invoke(gm, new object[] { Defs.DpsShot, home + new V2(40f + 34f * k, -16f * k), new V2(700f, -320f), 0f, 2f, 10f, me.Id, false, 0f });
    gm.Effects.Add(new Effect { Kind = FxKind.Boom, Pos = new V2(1150f, 330f), Radius = 70f, Color = new Rgba(1f, 0.6f, 0.2f), T = 0.08f, Dur = 0.6f });
    addZone.Invoke(gm, new object[] { ZoneKind.FlameWall, me, new V2(250f, 200f), 80f, 5f });
    foreach (var pr in gm.Props.Values) pr.Age = 5f;
    if (ls == "3") {  // the effects' lights: a Railgun beam, a lightning chain, a frost burst, a muzzle flash
      gm.Bullets.Clear();
      gm.Effects.Add(new Effect { Kind = FxKind.RailBeam, Pos = home + new V2(30f, 0f), Pos2 = new V2(1500f, 300f), Radius = 8f, Color = new Rgba(1f, 0.8f, 0.3f, 1f), T = 0.05f, Dur = 0.5f });
      gm.Effects.Add(new Effect { Kind = FxKind.ArcLink, Pos = new V2(560f, 330f), Pos2 = new V2(590f, 560f), T = 0.05f, Dur = 0.4f });
      gm.Effects.Add(new Effect { Kind = FxKind.FrostBurst, Pos = new V2(1120f, 640f), T = 0.05f, Dur = 0.6f });
      typeof(ArenaApp).GetMethod("NoteMuzzleLight", flags).Invoke(app, new object[] { home + new V2(30f, 0f), new Rgba(1f, 0.8f, 0.3f) });
    }
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
  }
}

// LIGHTPERF=<enemies>,<shots>[,<blasts>]: the lighting's draw cost in a crowd: that many Grunts packed round you, enemy
// shots among them and blasts going off, the frame drawn 60 times with the lighting off, hard, soft and soft with the
// floor dimmed; prints each one's draw time and vertices, then quits. E.g. LIGHTPERF=300,400,6 shot.sh x hud 3 1 1.
static partial class Shot {
  static void LightPerf() {
    string lp = Environment.GetEnvironmentVariable("LIGHTPERF");
    if (lp == null) return;
    var p = lp.Split(','); int n = int.Parse(p[0]), m = p.Length > 1 ? int.Parse(p[1]) : 0, booms = p.Length > 2 ? int.Parse(p[2]) : 4;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear();
    var rng = new System.Random(5);
    V2 mid = new V2(800f, 450f);
    for (int i = 0; i < n; i++) {
      float a = (float)rng.NextDouble() * MathX.Tau, d = 60f + (float)Math.Sqrt(rng.NextDouble()) * 420f;
      spawn.Invoke(gm, new object[] { EnemyType.Grunt, mid + V2.FromAngle(a) * d, 0f, 0, false });
    }
    foreach (var e in gm.Enemies.Values) { e.Spawning = false; e.SpawnT = 0f; e.Disp = e.Pos; }
    for (int i = 0; i < m; i++) {
      float a = (float)rng.NextDouble() * MathX.Tau, d = (float)Math.Sqrt(rng.NextDouble()) * 600f;
      shoot.Invoke(gm, new object[] { Defs.SmallShot, mid + V2.FromAngle(a) * d, V2.FromAngle(a) * 200f, 0f, 9f, 8f, -1, false, 0f });
    }
    for (int i = 0; i < booms; i++)
      gm.Effects.Add(new Effect { Kind = FxKind.Boom, Pos = mid + V2.FromAngle(i * 1.7f) * (100f + 50f * i), Radius = 70f, Color = new Rgba(1f, 0.6f, 0.2f), T = 0.08f, Dur = 30f });
    var draw = typeof(ArenaApp).GetMethod("DrawWorld", flags);
    var light = typeof(ArenaApp).GetMethod("DrawLighting", flags);
    var rays = typeof(ArenaApp).GetField("lightRays", flags);
    var fans = typeof(ArenaApp).GetField("lightFanN", flags);
    Console.WriteLine("LIGHTPERF enemies {0} shots {1} blasts {2}", gm.Enemies.Count, gm.Bullets.Count, booms);
    foreach (var cfg in new[] { "off", "hard", "soft", "soft+dim" }) {
      set("lightsOn", cfg != "off"); set("lightsSoft", cfg.StartsWith("soft")); set("floorDim", cfg.EndsWith("dim") ? 2 : 0);
      var sw = new System.Diagnostics.Stopwatch(); double ms = 0, worst = 0, lms = 0; long verts = 0;
      for (int i = 0; i < 70; i++) {
        GL.Tris.Clear(); GUI.Log.Clear();
        typeof(ArenaApp).GetField("holdT", flags).SetValue(app, 0f);
        sw.Restart(); draw.Invoke(app, null); sw.Stop();
        if (i < 10) continue;  // (warm up)
        ms += sw.Elapsed.TotalMilliseconds; worst = Math.Max(worst, sw.Elapsed.TotalMilliseconds); verts += GL.Tris.Count / 6;
        sw.Restart(); light.Invoke(app, new object[] { 0f }); sw.Stop(); lms += sw.Elapsed.TotalMilliseconds;
      }
      Console.WriteLine("  {0,-9} frame {1,6:0.00} ms (worst {2,6:0.00}) | lighting alone {3,5:0.00} ms | verts {4,6} | lights {5,2} rays {6,5}",
        cfg, ms / 60, worst, lms / 60, verts / 60, fans.GetValue(app), rays.GetValue(app));
    }
    Environment.Exit(0);
  }
}
