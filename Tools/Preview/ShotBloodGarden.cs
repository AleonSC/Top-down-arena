using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;
using UnityEngine;

// GARDEN=<seconds since the cast> [GARDENCAP=1]: the Healer Hemomancer's Blood Garden (Core/BloodGarden.cs,
// Game/BloodGardenLook.cs) played through the real game: you stand at (560, 440) among grunts held still, the seeds fly out
// and sprout, the saplings slow and siphon the grunts near them, and the full bulbs burst into blood orbs (you are kept hurt,
// so they drift to you). Use with `ZOOM=1.3 shot.sh x hud 1 6 1` (1 s: seeds flying; 3-5 s: siphoning and bursts).
static partial class Shot {
  static void BloodGardenScene() {
    string ts = Environment.GetEnvironmentVariable("GARDEN");
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
    V2 home = new V2(560f, 440f);
    var rng = new System.Random(11);
    var mobs = new List<Enemy>();
    var spots = new Dictionary<Enemy, V2>();
    for (int tries = 0; mobs.Count < 18 && tries < 3000; tries++) {
      float a = (float)rng.NextDouble() * MathX.Tau, r = 110f + (float)rng.NextDouble() * 220f;
      V2 at = home + new V2((float)Math.Cos(a) * r * 1.25f, (float)Math.Sin(a) * r * 0.85f);
      if (mobs.Exists(o => o.Pos.CloserThan(at, 50f))) continue;
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Pos = e.Disp = at; e.FireT = 99f;
      e.Hp = e.MaxHp = 1e5f;
      mobs.Add(e); spots[e] = at;
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.BloodGarden; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    if (Environment.GetEnvironmentVariable("GARDENCAP") != null) me.MasteryXp[AbilityId.BloodGarden] = 99999f;
    me.Hp = me.MaxHp;
    const int warm = 150;  // (the landing intro's camera settles first)
    int castAt = warm + 18, frames = castAt + (int)Math.Round(tt * 60f);
    for (int f = 0; f < frames; f++) {
      if (f == castAt) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, home + new V2(200f, 0f), false });
      if (f > castAt) me.Hp = Math.Min(me.Hp, me.MaxHp * 0.55f);  // (kept hurt, so the orbs come to you)
      me.Aim = 0f; me.AimAt = home + new V2(200f, 0f);
      foreach (var q in gm.Players.Values) q.Xp = 0f;  // (no level-up card over the scene)
      Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
      foreach (var e in mobs) if (gm.Enemies.ContainsKey(e.Id)) { e.FireT = 99f; e.Pos = e.Disp = spots[e]; }
      me.Pos = me.Disp = home;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = me.AimAt }); gm.Update(1f / 60f);
      gm.Bullets.Clear();
      me.Pos = me.Disp = home;
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      foreach (var o in gm.Orbs.Values) o.Disp = o.Pos;
      if (f >= frames - 60) { int keep = GL.Tris.Count; drawWorld.Invoke(app, null); GL.Tris.RemoveRange(keep, GL.Tris.Count - keep); }
    }
    int saplings = 0; foreach (var z in gm.Zones.Values) if (z.Kind == ZoneKind.BloodSapling) saplings++;
    Console.Error.WriteLine("garden: saplings {0} bursts {1} orbs {2} hp {3:0}", saplings, me.BgBursts, gm.Orbs.Count, me.Hp);
  }
}
