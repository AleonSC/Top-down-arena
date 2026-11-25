using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;
using UnityEngine;

// FORBIDDEN=<seconds since the cast>: the DPS Ninja's Forbidden Path (Core/ForbiddenPath.cs, Game/ForbiddenPathLook.cs) played
// through the real game: you at (380, 450) with a tough grunt (the lock) at (640, 450) in a ring of grunts and three more further out,
// the cast 0.3 s into the scene and Fire held from then on, so the combo runs back to back: the shed to 0.6 s, the Pounce
// to 1.15, the Rake to 1.85, the Spiral to 3.25, the Rend to 4.25, the Gate to 5.65 (slam at 4.9). The world is drawn every
// frame of the last second, so the fading crescents, claw marks and black sun are there. Try 0.3, 0.95, 1.2, 2.6, 3.7, 4.6, 5.0.
// FPNEXT=<1-5> starts the combo at that stage (each stage then starts 0.6 s in). Use with `ZOOM=2.4 CAMAT=630,460 shot.sh x hud 1 6 1`.
static partial class Shot {
  static void ForbiddenPathScene() {
    string ts = Environment.GetEnvironmentVariable("FORBIDDEN");
    if (ts == null) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    var drawWorld = typeof(ArenaApp).GetMethod("DrawWorld", flags);
    typeof(ArenaApp).GetField("juiceGame", flags).SetValue(app, gm);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Ninja; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Ninja, 0);
    V2 home = new V2(380f, 450f), lockAt = new V2(640f, 450f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    EnemyType heavy = EnemyType.Grunt;
    foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) { if ((int)et >= Defs.Enemies.Length) continue; var d = Defs.Get(et); if (d != null && d.Heavy && !d.IsBoss) { heavy = et; break; } }
    var mobs = new List<Enemy>();
    var spots = new Dictionary<Enemy, V2>();
    V2[] at = { lockAt, lockAt + new V2(-60f, -60f), lockAt + new V2(60f, -50f), lockAt + new V2(-45f, 70f), lockAt + new V2(70f, 60f),
                lockAt + new V2(180f, -150f), lockAt + new V2(210f, 150f), lockAt + new V2(-170f, 190f) };
    for (int i = 0; i < at.Length; i++) {
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, at[i], 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = i == 0 ? 2e5f : 1e5f; e.Pos = e.Disp = at[i]; e.FireT = 99f; mobs.Add(e); spots[e] = at[i];
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.ForbiddenPath; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    me.Pos = me.Disp = home;
    int startAt = Environment.GetEnvironmentVariable("FPNEXT") != null ? int.Parse(Environment.GetEnvironmentVariable("FPNEXT")) : 1;
    int frames = (int)Math.Round((float.Parse(ts, inv) + 0.3f) * 60f);
    for (int f = 0; f < frames; f++) {
      V2 aimAt = lockAt;
      me.Aim = (aimAt - me.Pos).Angle; me.AimAt = aimAt;
      if (f == 18) { cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, aimAt, false }); me.FpNext = (byte)startAt; }
      foreach (var e in mobs) { e.FireT = 99f; e.Hp = e.MaxHp; if (me.FpStage == 3) spots[e] = e.Pos; else e.Pos = e.Disp = spots[e]; }
      me.Hp = me.MaxHp;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt, Fire = f > 18 }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      if (f >= frames - 60 && f < frames - 1) { int keep = GL.Tris.Count; drawWorld.Invoke(app, null); GL.Tris.RemoveRange(keep, GL.Tris.Count - keep); }
    }
    foreach (var e in mobs) Console.Error.WriteLine("  mob {0} {1} at {2:0},{3:0}", e.Id, e.Type, e.Pos.X, e.Pos.Y);
    Console.Error.WriteLine("  me at {0:0},{1:0} FpAt {2:0},{3:0}", me.Pos.X, me.Pos.Y, me.FpAt.X, me.FpAt.Y);
    Console.Error.WriteLine("forbidden: T {0:0.00} age {1:0.00} stage {2} st {3:0.00} target {4} hits {5} stages {6}", me.FpT, me.FpAge, me.FpStage, me.FpStageT, me.FpTarget, me.FpHits, me.FpStages);
  }
}
