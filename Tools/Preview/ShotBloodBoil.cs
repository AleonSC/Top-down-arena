using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;
using UnityEngine;

// BOIL=<seconds since the cast>: the DPS Hemomancer's Blood Boil (Core/BloodBoil.cs, Game/BloodBoilLook.cs) played through the
// real game: you circle (500, 450) firing at a loose crowd of grunts, the cast 0.3 s into the scene. BOILHP=<0-1> eases your
// health down to that share by the end, so the boil shows that hard (left out: the real drain). BOILEND=1 presses it again
// 0.4 s before the end, for the steam burst. The world is drawn every frame of the last second (and thrown away), so the
// puddles, bubbles and steam are there too. Use with `ZOOM=1.8 shot.sh x hud 1 6 1`.
static partial class Shot {
  static void BloodBoilScene() {
    string ts = Environment.GetEnvironmentVariable("BOIL");
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
    me.Kit = Kit.Hemomancer; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Hemomancer, 1);
    string hs = Environment.GetEnvironmentVariable("BOILHP");
    bool end = Environment.GetEnvironmentVariable("BOILEND") == "1";
    float tt = float.Parse(ts, inv), hpTo = hs == null ? -1f : float.Parse(hs, inv);
    V2 home = new V2(500f, 450f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    var rng = new System.Random(5);
    var mobs = new List<Enemy>();
    var spots = new Dictionary<Enemy, V2>();
    for (int tries = 0; mobs.Count < 9 && tries < 3000; tries++) {
      V2 at = home + new V2(((float)rng.NextDouble() - 0.5f) * 640f, ((float)rng.NextDouble() - 0.5f) * 380f);
      if (at.CloserThan(home, 150f) || mobs.Exists(o => o.Pos.CloserThan(at, 60f))) continue;
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 1e5f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e); spots[e] = at;
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.BloodBoil; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    me.Hp = me.MaxHp;
    int frames = (int)Math.Round((tt + 0.3f) * 60f);
    float ang = 0f;
    for (int f = 0; f < frames; f++) {
      float since = f / 60f - 0.3f;
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, home, false });
      if (end && f == frames - 24) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, home, false });
      if (hpTo >= 0f && since > 0f && Game.BoilOn(me)) me.Hp = me.MaxHp * (1f - (1f - hpTo) * Math.Min(1f, since / Math.Max(0.1f, tt - 0.5f)));
      ang += me.Speed / 90f / 60f;
      V2 pos = home + new V2((float)Math.Cos(ang) * 90f, (float)Math.Sin(ang) * 60f);
      Enemy near = null;
      foreach (var e in mobs) if (near == null || e.Pos.DistanceTo(pos) < near.Pos.DistanceTo(pos)) near = e;
      V2 aimAt = near != null ? near.Pos : pos + new V2(200f, 0f);
      me.Aim = (aimAt - pos).Angle; me.AimAt = aimAt;
      foreach (var e in mobs) { e.FireT = 99f; e.Hp = e.MaxHp; e.Pos = e.Disp = spots[e]; }
      me.Pos = me.Disp = pos;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt, Fire = true }); gm.Update(1f / 60f);
      me.Pos = me.Disp = pos;
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      if (f >= frames - 60 && f < frames - 1) { int keep = GL.Tris.Count; drawWorld.Invoke(app, null); GL.Tris.RemoveRange(keep, GL.Tris.Count - keep); }
    }
    Console.Error.WriteLine("boil: t {0:0.00} hp {1:0}% boil {2:0.00} speed {3:0}", me.BoilT, 100f * me.Hp / me.MaxHp, Game.BoilLevel(me), me.Speed);
  }
}
