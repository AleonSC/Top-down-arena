using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;
using UnityEngine;

// ONI=<seconds since the cast>[,<kills>]: the DPS Ninja's Oni Mask (Core/OniMask.cs, Game/OniMaskLook.cs) played through the
// real game: you stand at (520, 450) aiming right at a few grunts (they stand still); `kills` of them die to you just before
// the shot, so the ring shows the time they put back on. ONITRAIL=1 walks you left to right for the afterimages;
// ONILEFT=<s> sets the time left at the shot (the last 2 s: cracked mask, sputtering flames).
// Use with `ZOOM=2 shot.sh x hud`.
static partial class Shot {
  static void OniMaskScene() {
    string ts = Environment.GetEnvironmentVariable("ONI");
    if (ts == null) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    var parts = ts.Split(',');
    float tt = float.Parse(parts[0], inv);
    int kills = parts.Length > 1 ? int.Parse(parts[1], inv) : 0;
    bool walk = Environment.GetEnvironmentVariable("ONITRAIL") == "1";
    string leftS = Environment.GetEnvironmentVariable("ONILEFT");  // (the time left set just after the cast: the last 2 s without a long run)
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
    var drawWorld = typeof(ArenaApp).GetMethod("DrawWorld", flags);
    typeof(ArenaApp).GetField("juiceGame", flags).SetValue(app, gm);
    var glMat = typeof(ArenaApp).GetField("glMaterial", flags);
    if (glMat.GetValue(app) == null) glMat.SetValue(app, new Material(null));
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Ninja; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Ninja, 0);
    V2 home = new V2(520f, 450f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = home + new V2(-200f, 1400f);
    var mobs = new List<Enemy>();
    var spots = new Dictionary<Enemy, V2>();
    for (int i = 0; i < 6; i++) {
      V2 at = home + new V2(150f + 60f * (i % 3), -90f + 90f * (i / 3) + 20f * (i % 2));
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Pos = e.Disp = at; e.FireT = 99f; e.Hp = e.MaxHp = 1e4f;
      mobs.Add(e); spots[e] = at;
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.OniMask; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    me.Hp = me.MaxHp;
    const int warm = 150;
    int castAt = warm + 18, frames = castAt + (int)Math.Round(tt * 60f);
    for (int f = 0; f < frames; f++) {
      if (f == castAt) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, home + new V2(300f, 0f), false });
      if (f == castAt + 1 && leftS != null) me.OmT = float.Parse(leftS, inv) + tt;
      if (f > castAt && f >= frames - 6 * kills && (frames - f) % 6 == 0) {  // the kills, just before the shot
        int n = (frames - f) / 6 - 1;
        if (n < mobs.Count && gm.Enemies.ContainsKey(mobs[n].Id)) { mobs[n].Hp = 1f; hurt.Invoke(gm, new object[] { mobs[n], 50f, me.Id }); }
      }
      V2 pos = walk ? home + new V2(-120f + 200f * Mathf.Clamp01((f - castAt) / 60f / Mathf.Max(0.1f, tt)), 0f) : home;
      me.Aim = 0f; me.AimAt = home + new V2(300f, 0f);
      foreach (var q in gm.Players.Values) q.Xp = 0f;
      Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f; Time.deltaTime = 1f / 60f;
      foreach (var e in mobs) if (gm.Enemies.ContainsKey(e.Id)) { e.FireT = 99f; e.Pos = e.Disp = spots[e]; }
      me.Pos = me.Disp = pos;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = me.AimAt }); gm.Update(1f / 60f);
      gm.Bullets.Clear();
      me.Pos = me.Disp = pos;
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      if (f >= frames - 60) { int keep = GL.Tris.Count; drawWorld.Invoke(app, null); GL.Tris.RemoveRange(keep, GL.Tris.Count - keep); }
    }
    Console.Error.WriteLine("oni: left {0:0.00} of {1:0.00}, kills {2}, hp {3:0}/{4:0}, speed {5:0}", me.OmT, me.OmLen, me.OmKills, me.Hp, me.MaxHp, me.Speed);
  }
}
