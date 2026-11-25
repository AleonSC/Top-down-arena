using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;
using UnityEngine;

// RAVAGER=<seconds since the cast>: the DPS Hemomancer's Blood Ravager (Core/BloodRavager.cs, Game/BloodRavagerLook.cs) played
// through the real game: you walk right from (380, 450) through a loose crowd of grunts and a Heavy, the cast 0.3 s into the
// scene, then (RAVAGERCLICK=1, the default) a click every 0.1 s over the last 0.4 s, so legs are lashing. The world is drawn
// every frame of the last second (and thrown away), so the walk, stains and drops are there too. RAVAGERSTILL=1 stands still.
// Try 0.15 (the burst), 1.2, 3. Use with `ZOOM=1.6 shot.sh x hud 1 6 1`.
static partial class Shot {
  static void BloodRavagerScene() {
    string ts = Environment.GetEnvironmentVariable("RAVAGER");
    if (ts == null) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    var drawWorld = typeof(ArenaApp).GetMethod("DrawWorld", flags);
    typeof(ArenaApp).GetField("juiceGame", flags).SetValue(app, gm);
    var glMat = typeof(ArenaApp).GetField("glMaterial", flags);
    if (glMat.GetValue(app) == null) glMat.SetValue(app, new Material(null));  // (else the warm-up frames leave the stub's blend additive)
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Hemomancer; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Hemomancer, 0);
    bool still = Environment.GetEnvironmentVariable("RAVAGERSTILL") == "1";
    bool clicks = Environment.GetEnvironmentVariable("RAVAGERCLICK") != "0";
    float tt = float.Parse(ts, inv);
    V2 home = new V2(380f, 450f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    EnemyType heavy = EnemyType.Grunt;
    foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) { if ((int)et >= Defs.Enemies.Length) continue; var d = Defs.Get(et); if (d != null && d.Heavy && !d.IsBoss) { heavy = et; break; } }
    float speed = me.Def.Speed * Game.RvSpeed;
    V2 endAt = still ? home : home + new V2(speed * Math.Max(0f, tt), 0f);
    var rng = new System.Random(3);
    var mobs = new List<Enemy>();
    var spots = new Dictionary<Enemy, V2>();
    for (int tries = 0; mobs.Count < 10 && tries < 3000; tries++) {
      V2 at = endAt + new V2(((float)rng.NextDouble() - 0.35f) * 520f, ((float)rng.NextDouble() - 0.5f) * 360f);
      if (at.CloserThan(endAt, 60f) || mobs.Exists(o => o.Pos.CloserThan(at, 50f))) continue;
      var e = (Enemy)spawn.Invoke(gm, new object[] { mobs.Count == 3 ? heavy : EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 1e5f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e); spots[e] = at;
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.BloodRavager; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    int frames = (int)Math.Round((tt + 0.3f) * 60f);
    for (int f = 0; f < frames; f++) {
      float since = f / 60f - 0.3f;
      V2 pos = still || since < 0f ? home : home + new V2(speed * since, (float)Math.Sin(since * 2.2f) * 30f);
      V2 aimAt = pos + new V2(220f, -60f);
      me.Aim = (aimAt - pos).Angle; me.AimAt = aimAt;
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, aimAt, false });
      foreach (var e in mobs) { e.FireT = 99f; e.Hp = e.MaxHp; e.Pos = e.Disp = spots[e]; }
      me.Pos = me.Disp = pos;
      bool fire = clicks && f >= frames - 24 && (frames - f) % 6 >= 3;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt, Fire = fire }); gm.Update(1f / 60f);
      me.Pos = me.Disp = pos;
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      if (f >= frames - 60 && f < frames - 1) { int keep = GL.Tris.Count; drawWorld.Invoke(app, null); GL.Tris.RemoveRange(keep, GL.Tris.Count - keep); }
    }
    Console.Error.WriteLine("ravager: t {0:0.00} lashes {1} hits {2} stomps {3} trampled {4}", me.RvT, me.RvLashCount, me.RvLashHits, me.RvStomps, me.RvTrampled);
  }
}
