using System; using System.Linq; using System.Reflection; using System.Collections.Generic; using Arena; using UnityEngine;
// Drives the visual effects (Game/Juice.cs) through real frames without Unity: hits, kills, a boss's death, a hit-pause,
// a level up and being hurt, and checks each one starts what it should. Run by Tools/RenderCheck/juice.sh.
class JuiceCheck {
  static int fails;
  static void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
  static void Main() {
    UnityEngine.Screen.width = 1280; UnityEngine.Screen.height = 720;
    UnityEngine.Time.unscaledDeltaTime = 1f / 60f;
    var f2 = BindingFlags.NonPublic | BindingFlags.Instance;
    var s = new Session { StartWave = 4 };
    s.HostOffline(Role.Tank, Kit.Soldier, 0, 0);
    var g = s.Game;
    typeof(Game).GetField("rng", f2).SetValue(g, new System.Random(4));
    g.Bot = true; g.HostStart(); g.DevSetGod(1, true);
    for (int i = 0; i < 60 * 6; i++) { g.FixedStep(1f / 60f, new PlayerInput()); g.Update(1f / 60f); g.Sounds.Clear(); }
    var app = new ArenaApp();
    Action<string, object> set = (n, v) => typeof(ArenaApp).GetField(n, f2).SetValue(app, v);
    Func<string, object> get = n => typeof(ArenaApp).GetField(n, f2).GetValue(app);
    set("session", s); set("game", g); set("scale", 1f); set("drawScale", 1280f / Game.Width);
    var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
    Func<int> frame = () => { GL.Tris.Clear(); draw.Invoke(app, null); return GL.Tris.Count / 18; };
    Action tick = () => { g.FixedStep(1f / 60f, new PlayerInput()); g.Update(1f / 60f); g.Sounds.Clear(); };

    set("fxOn", false);
    int plain = frame();
    set("fxOn", true);
    frame(); tick();
    int juicy = frame();
    Console.WriteLine("  triangles: {0} plain, {1} with effects", plain, juicy);
    Check(juicy > plain, "with effects on, the arena draws more (glow, vignette)");

    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", f2);
    var a = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(500f, 300f), 0f, 0, false });
    a.SpawnT = 0f;
    frame(); tick();
    a.Hp -= 20f;
    frame();
    Check((bool)typeof(ArenaApp).GetMethod("JuiceFlash", f2).Invoke(app, new object[] { a }) && (float)typeof(ArenaApp).GetMethod("JuiceScale", f2).Invoke(app, new object[] { a }) >= 1f,
      "a hit enemy flashes white (and bulges)");
    for (int i = 0; i < 12; i++) { tick(); frame(); }
    var nums = (System.Collections.IList)get("numbers");
    Check(nums.Count >= 1, "damage to an enemy rises off it as a number");

    a.Hp = 0f; g.Enemies.Remove(a.Id);
    frame();
    int parts = (int)get("partN");
    var scorches = (System.Collections.IList)get("scorches");
    Console.WriteLine("  a grunt died: {0} particles, {1} scorch marks", parts, scorches.Count);
    Check(parts >= 3 + 10 && scorches.Count >= 1, "a killed enemy shatters into shards, sparks and a scorch mark");

    // One shot from full health: the killing blow still shows (the game plays a death where it was).
    var c = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Sniper, new V2(300f, 600f), 0f, 0, false });
    c.SpawnT = 0f;
    tick(); frame();
    nums.Clear();
    float full = c.Hp;
    g.Enemies.Remove(c.Id);
    g.Effects.Add(new Effect { Kind = FxKind.Boom, Pos = c.Pos, Color = c.Def.Color, Radius = c.Radius * 2.2f, Dur = 0.4f });
    frame();
    var last = nums.Count > 0 ? nums[nums.Count - 1] : null;
    int shown = last == null ? -1 : (int)last.GetType().GetField("Value").GetValue(last);
    Check(shown == (int)Math.Round(full), "a one-shot kill shows its killing blow as a number (" + shown + ")");

    // A big enemy's death holds the picture: the next frame replays the last one exactly.
    var j = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Juggernaut, new V2(900f, 500f), 0f, 0, false });
    j.SpawnT = 0f;
    tick(); frame(); tick(); frame();
    set("holdGap", 0f);
    j.Hp = 0f; g.Enemies.Remove(j.Id);
    frame();
    var before = new List<float>(GL.Tris);
    tick();
    frame();
    float hold = (float)get("holdT");
    Check(before.Count > 0 && GL.Tris.SequenceEqual(before), "a heavy enemy's death holds the picture: the next frame is the same one again");
    for (int i = 0; i < 10; i++) { tick(); frame(); }
    Check((float)get("holdT") <= 0f, "and the picture moves on after a moment");

    // Shake: a blast near you offsets the arena.
    float t0 = (float)get("trauma");
    g.Effects.Add(new Effect { Kind = FxKind.Boom, Pos = g.Me.Pos + new V2(60f, 0f), Color = new Rgba(1f, 0.6f, 0.3f), Radius = 140f, Dur = 0.4f });
    frame();
    Check((float)get("trauma") > t0 && ((System.Collections.IList)get("ripples")).Count >= 1, "a big blast near you shakes the screen and sends a ripple through the grid");
    set("fxShake", 0); set("trauma", 0f);
    g.Effects.Add(new Effect { Kind = FxKind.Boom, Pos = g.Me.Pos, Color = new Rgba(1f, 0.6f, 0.3f), Radius = 140f, Dur = 0.4f });
    frame();
    Check((float)get("trauma") == 0f, "with Screen shake off, nothing shakes");
    set("fxShake", 2);

    // Hurt: the red corners; a level up: the gold burst.
    g.Me.Hp -= 30f; frame();
    Check((float)get("hurtTint") > 0.9f, "taking damage tints the corners red");
    int p0 = (int)get("partN");
    g.Me.Level++; frame();
    Check((int)get("partN") >= p0 + 30, "a level up bursts with gold sparks");

    // A boss: the entrance card, then its death in waves.
    var boss = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Boss, new V2(800f, 300f), 0f, 0, false });
    boss.SpawnT = 0f;
    frame();
    Check((float)get("introT") > 0f && (string)get("introName") == "THE WARDEN", "a boss's arrival brings its entrance card");
    set("holdGap", 0f);
    boss.Hp = 0f; g.Enemies.Remove(boss.Id); frame();
    Check(((System.Collections.IList)get("bursts")).Count == 4 && (float)get("trauma") > 0.5f, "a boss's death shakes hard and bursts in waves");

    // Off again: exactly the plain drawing.
    set("fxOn", false);
    tick(); set("fxOn", false);
    int off1 = frame();
    Check(off1 > 0 && (float)typeof(ArenaApp).GetMethod("JuiceScale", f2).Invoke(app, new object[] { g.Enemies.Values.First() }) == 1f, "turned off, nothing extra is drawn");
    Console.WriteLine(fails == 0 ? "ALL PASSED" : fails + " FAILED");
    Environment.Exit(fails == 0 ? 0 : 1);
  }
}
