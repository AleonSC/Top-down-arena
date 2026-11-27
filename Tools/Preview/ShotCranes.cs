using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;
using UnityEngine;

// CRANES=<seconds since the cast>: the DPS Ninja's Hundred Paper Cranes (Core/HundredPaperCranes.cs,
// Game/HundredPaperCranesLook.cs) played through the real game: you stand at (520, 450) with a scatter of grunts to the
// right (one big one, the rest small; they stand still) and the cranes swirl round you, then peel off and home in.
// Use with `ZOOM=1.1 shot.sh x hud`.
static partial class Shot {
  static void CranesScene() {
    string ts = Environment.GetEnvironmentVariable("CRANES");
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
    me.Kit = Kit.Ninja; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Ninja, 0);
    float tt = float.Parse(ts, inv);
    V2 home = new V2(520f, 450f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = home + new V2(-200f, 1400f);
    var rng = new System.Random(5);
    var mobs = new List<Enemy>();
    var spots = new Dictionary<Enemy, V2>();
    for (int tries = 0; mobs.Count < 12 && tries < 3000; tries++) {
      V2 at = home + new V2(260f + (float)rng.NextDouble() * 380f, -260f + (float)rng.NextDouble() * 520f);
      if (mobs.Exists(o => o.Pos.CloserThan(at, 70f))) continue;
      var e = (Enemy)spawn.Invoke(gm, new object[] { mobs.Count == 0 ? EnemyType.Brute : EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Pos = e.Disp = at; e.FireT = 99f;
      e.Hp = e.MaxHp = mobs.Count == 0 ? 1e5f : 1e4f;
      mobs.Add(e); spots[e] = at;
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.HundredPaperCranes; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    me.Hp = me.MaxHp;
    const int warm = 150;  // (the landing intro's camera settles first)
    int frames = warm + (int)Math.Round((tt + 0.3f) * 60f);
    var shown = (Game)typeof(ArenaApp).GetField("game", flags).GetValue(app);  // (the screen's game can trail the host's: step until it shows `tt`)
    Func<float> shownT = () => { Player sp; return shown != null && shown.Players.TryGetValue(me.Id, out sp) ? sp.HpcT : me.HpcT; };
    bool cast0 = false;
    for (int f = 0; f < frames + 120; f++) {
      if (cast0 && shownT() >= tt) break;
      if (f == warm + 18) { cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, home + new V2(300f, 0f), false }); cast0 = true; }
      me.Aim = 0f; me.AimAt = home + new V2(300f, 0f);
      foreach (var q in gm.Players.Values) q.Xp = 0f;  // (no level-up card over the scene)
      Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
      foreach (var e in mobs) if (gm.Enemies.ContainsKey(e.Id)) { e.FireT = 99f; e.Pos = e.Disp = spots[e]; }
      me.Pos = me.Disp = home;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = me.AimAt }); gm.Update(1f / 60f);
      foreach (var id in new List<int>(gm.Bullets.Keys)) if (gm.Bullets[id].Kind != Defs.PaperCrane) gm.Bullets.Remove(id);
      me.Pos = me.Disp = home;
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      if (f >= frames - 60) { int keep = GL.Tris.Count; drawWorld.Invoke(app, null); GL.Tris.RemoveRange(keep, GL.Tris.Count - keep); }
    }
    int flying = 0; foreach (var b in gm.Bullets.Values) if (b.Kind == Defs.PaperCrane) flying++;
    Console.Error.WriteLine("cranes: shown t {0:0.00}, host t {1:0.00}, ring {2}, flying {3}, hits {4}", shownT(), me.HpcT, me.HpcN, flying, me.HpcHits);
  }
}
