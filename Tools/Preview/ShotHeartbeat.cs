using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;
using UnityEngine;

// HEARTBEAT=<seconds since the cast>: the Healer Hemomancer's Heartbeat (Core/Heartbeat.cs, Game/HeartbeatLook.cs) played
// through the real game: you stand at (500, 450) in a ring of grunts, some weak enough for a wave to kill (they drop blood
// orbs), the cast 0.3 s after a 2.5 s warm-up; a hurt teammate stands nearby. The world is drawn every frame of the last second
// (and thrown away), so the spray and stains are there too. Use with `ZOOM=1.4 shot.sh x hud 1 6 1`.
static partial class Shot {
  static void HeartbeatScene() {
    string ts = Environment.GetEnvironmentVariable("HEARTBEAT");
    if (ts == null) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    var drawWorld = typeof(ArenaApp).GetMethod("DrawWorld", flags);
    typeof(ArenaApp).GetField("juiceGame", flags).SetValue(app, gm);
    var glMat = typeof(ArenaApp).GetField("glMaterial", flags);
    if (glMat.GetValue(app) == null) glMat.SetValue(app, new Material(null));
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Hemomancer; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Hemomancer, 0);
    float tt = float.Parse(ts, inv);
    V2 home = new V2(500f, 450f);
    Player mate = null;
    foreach (var q in gm.Players.Values) if (q != me) { q.Pos = q.Disp = home + new V2(-250f, -140f); q.Hp = q.MaxHp * 0.4f; mate = q; }
    var rng = new System.Random(7);
    var mobs = new List<Enemy>();
    var spots = new Dictionary<Enemy, V2>();
    for (int tries = 0; mobs.Count < 16 && tries < 3000; tries++) {
      float a = (float)rng.NextDouble() * MathX.Tau, r = 90f + (float)rng.NextDouble() * 330f;
      V2 at = home + new V2((float)Math.Cos(a) * r * 1.3f, (float)Math.Sin(a) * r * 0.8f);
      if (mobs.Exists(o => o.Pos.CloserThan(at, 55f)) || (mate != null && mate.Pos.CloserThan(at, 60f))) continue;
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Pos = e.Disp = at; e.FireT = 99f;
      e.Hp = e.MaxHp = mobs.Count % 2 == 0 ? 30f : 1e5f;
      mobs.Add(e); spots[e] = at;
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.Heartbeat; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    me.Hp = me.MaxHp;
    const int warm = 150;  // (the landing intro's camera settles first)
    int frames = warm + (int)Math.Round((tt + 0.3f) * 60f);
    var shown = (Game)typeof(ArenaApp).GetField("game", flags).GetValue(app);  // (the screen's game can trail the host's: step until it shows `tt`)
    Func<float> shownT = () => { Player sp; return shown != null && shown.Players.TryGetValue(me.Id, out sp) ? sp.HbT : me.HbT; };
    for (int f = 0; f < frames + 120; f++) {
      if (f > warm + 18 && shownT() >= tt) break;
      if (f == warm + 18) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, home, false });
      me.Aim = 0f; me.AimAt = home + new V2(200f, 0f);
      foreach (var q in gm.Players.Values) q.Xp = 0f;  // (no level-up card over the scene)
      Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;  // (the kills' hit-pause runs on this clock)
      foreach (var e in mobs) if (gm.Enemies.ContainsKey(e.Id)) { e.FireT = 99f; e.Pos = e.Disp = spots[e]; }
      me.Pos = me.Disp = home;
      if (mate != null) mate.Pos = mate.Disp = home + new V2(-250f, -140f);
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = me.AimAt }); gm.Update(1f / 60f);
      gm.Bullets.Clear();
      me.Pos = me.Disp = home;
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      foreach (var o in gm.Orbs.Values) o.Disp = o.Pos;
      if (f >= frames - 60) { int keep = GL.Tris.Count; drawWorld.Invoke(app, null); GL.Tris.RemoveRange(keep, GL.Tris.Count - keep); }
    }
    int orbs = 0; foreach (var o in gm.Orbs.Values) if (o.Blood) orbs++;
    Console.Error.WriteLine("heartbeat: shown t {5:0.00}, host t {0:0.00} beats {1} hits {2} blood orbs {3} hp {4:0}%", me.HbT, me.HbBeatCount, me.HbHits, orbs, 100f * me.Hp / me.MaxHp, shownT());
  }
}
