using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;
using UnityEngine;

// RODENT=<seconds since the cast>: the DPS Summoner's Rodent Minigun (Core/RodentMinigun.cs, Game/RodentMinigunLook.cs) played
// through the real game: you at (480, 450) aiming right at a crowd of grunts and a Heavy, the cast 0.3 s into the scene
// (RODENTAIM=<deg> swings the aim that far over the last 0.6 s, to show the barrel dragging). The world is drawn every frame of
// the last second (and thrown away), so the rodents scampering off and the cheese crumbs are there too. The portal opens to
// 0.4 s, spins up to 1.4 s, fires flat out to 6.4 s, winds down to 7.0 s and shuts at 7.4 s. Try 0.25, 1.0, 3.0, 6.7.
// Use with `ZOOM=1.6 shot.sh x hud 1 6 1`.
static partial class Shot {
  static void RodentMinigunScene() {
    string ts = Environment.GetEnvironmentVariable("RODENT");
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
    me.Kit = Kit.Summoner; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Summoner, 0);
    V2 home = new V2(480f, 450f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    EnemyType heavy = EnemyType.Grunt;
    foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) { if ((int)et >= Defs.Enemies.Length) continue; var d = Defs.Get(et); if (d != null && d.Heavy && !d.IsBoss) { heavy = et; break; } }
    var rng = new System.Random(5);
    var mobs = new List<Enemy>();
    var spots = new Dictionary<Enemy, V2>();
    for (int tries = 0; mobs.Count < 9 && tries < 2000; tries++) {
      V2 at = home + new V2(230f + (float)rng.NextDouble() * 220f, ((float)rng.NextDouble() - 0.5f) * 260f);
      if (mobs.Exists(o => o.Pos.CloserThan(at, 46f))) continue;
      var e = (Enemy)spawn.Invoke(gm, new object[] { mobs.Count == 2 ? heavy : EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 1e5f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e); spots[e] = at;
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.RodentMinigun; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    float swing = Environment.GetEnvironmentVariable("RODENTAIM") != null ? float.Parse(Environment.GetEnvironmentVariable("RODENTAIM"), inv) * MathX.Pi / 180f : 0f;
    int frames = (int)Math.Round((float.Parse(ts, inv) + 0.3f) * 60f);
    for (int f = 0; f < frames; f++) {
      float k = swing == 0f ? 0f : Math.Max(0f, Math.Min(1f, (f - (frames - 36)) / 36f));
      V2 aimAt = home + V2.FromAngle(-0.08f + swing * k) * 300f;
      me.Aim = (aimAt - home).Angle; me.AimAt = aimAt;
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, aimAt, false });
      foreach (var e in mobs) { e.FireT = 99f; e.Hp = e.MaxHp; e.Pos = e.Disp = spots[e]; }
      me.Pos = me.Disp = home;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt, Fire = true }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      if (f >= frames - 60 && f < frames - 1) { int keep = GL.Tris.Count; drawWorld.Invoke(app, null); GL.Tris.RemoveRange(keep, GL.Tris.Count - keep); }
    }
    Console.Error.WriteLine("rodent: t {0:0.00} fired {1} bullets {2} ang {3:0}", me.RmT, me.RodentsFired, gm.Bullets.Count, me.RmAng * 180f / MathX.Pi);
  }
}
