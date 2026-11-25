using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;
using UnityEngine;

// MAMMOTH=<seconds since the cast>: the DPS Summoner's Mammoth Fall (Core/MammothFall.cs, Game/MammothFallLook.cs) played through
// the real game: you at (420, 470) aiming at a crowd of grunts and a Heavy round (830, 420), the cast 0.3 s into the scene. The
// mammoth is drawn where the host has it (no easing: one frame is drawn). It forms to 1.6 s,
// dives to 2.1 s, lands, rampages from 2.45 s to 5.45 s and runs off by 6.25 s. Try 0.5, 1.4, 1.95, 2.2, 3.5, 5.9.
// Use with `ZOOM=1.3 CAMAT=640,450 shot.sh x hud 1 6 1`.

static partial class Shot {
  static void MammothFallScene() {
    string ts = Environment.GetEnvironmentVariable("MAMMOTH");
    if (ts == null) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    typeof(ArenaApp).GetField("juiceGame", flags).SetValue(app, gm);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Summoner; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Summoner, 0);
    V2 home = new V2(420f, 470f), aimAt = new V2(830f, 420f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    EnemyType heavy = EnemyType.Grunt;
    foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) { if ((int)et >= Defs.Enemies.Length) continue; var d = Defs.Get(et); if (d != null && d.Heavy && !d.IsBoss) { heavy = et; break; } }
    var rng = new System.Random(7);
    var mobs = new List<Enemy>();
    var spots = new Dictionary<Enemy, V2>();
    for (int tries = 0; mobs.Count < 12 && tries < 3000; tries++) {
      float a = (float)rng.NextDouble() * MathX.Tau, r = 30f + (float)rng.NextDouble() * 250f;
      V2 at = aimAt + new V2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.8f);
      if (mobs.Exists(o => o.Pos.CloserThan(at, 48f)) || at.CloserThan(home, 160f)) continue;
      var e = (Enemy)spawn.Invoke(gm, new object[] { mobs.Count == 3 ? heavy : EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 1e5f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e); spots[e] = at;
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.MammothFall; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    int frames = (int)Math.Round((float.Parse(ts, inv) + 0.3f) * 60f);
    Time.unscaledDeltaTime = 1f / 60f;
    for (int f = 0; f < frames; f++) {
      me.Aim = (aimAt - home).Angle; me.AimAt = aimAt;
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, aimAt, false });
      bool landed = f >= 18 + (int)(Game.MfImpactAt * 60f);
      foreach (var e in mobs) { e.FireT = 99f; e.Hp = e.MaxHp; if (!landed) e.Pos = e.Disp = spots[e]; }
      me.Pos = me.Disp = home;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    }
    Zone mz = null; foreach (var z in gm.Zones.Values) if (z.Kind == ZoneKind.Mammoth) mz = z;
    Console.Error.WriteLine("mammoth: age {0:0.00} pos {1} prey {2} tramples {3}", mz != null ? Game.MfAge(mz) : -1f, mz != null ? mz.Pos.ToString() : "-", mz != null ? mz.Side : 0, me.MammothTramples);
  }
}
