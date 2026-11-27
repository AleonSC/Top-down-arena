using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// GLOVES=<seconds since the cast>: the DPS Elementalist's Rock Gloves ultimate (Core/RockGloves.cs, Game/RockGlovesLook.cs) played
// through the real game: you at (800, 470) facing right with grunts in front, the cast 0.3 s into the scene, Fire held from the
// end of the gather (0.6 s). Try 0.3 (gather), 0.75 (first jabs), 1.0 (mid-flurry), 1.25 (the Haymaker), 10.2 (shedding chips).
// GLOVESIDLE=1 holds Fire off. Use with `ZOOM=1.3 shot.sh x hud 1 6 1`.
static partial class Shot {
  static void RockGlovesScene() {
    string ts = Environment.GetEnvironmentVariable("GLOVES");
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
    me.Kit = Kit.Elementalist; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Elementalist, 0);
    V2 home = new V2(800f, 470f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    var mobs = new List<Enemy>();
    var spots = new Dictionary<Enemy, V2>();
    V2[] at = { new V2(905f, 465f), new V2(915f, 500f), new V2(925f, 430f), new V2(1000f, 480f), new V2(890f, 560f), new V2(700f, 380f), new V2(650f, 560f), new V2(1060f, 380f) };
    foreach (var a in at) {
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, a, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 4000f; e.Pos = e.Disp = a; e.FireT = 99f; mobs.Add(e); spots[e] = a;
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.RockGloves; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    V2 aimAt = home + new V2(160f, 0f);
    me.Aim = 0f;
    bool idle = Environment.GetEnvironmentVariable("GLOVESIDLE") != null;
    int frames = (int)Math.Round((float.Parse(ts, inv) + 0.3f) * 60f);
    for (int f = 0; f < frames; f++) {
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, aimAt, false });
      foreach (var e in mobs) { e.FireT = 99f; e.Pos = e.Disp = spots[e]; }  // (held in place, so the flurry keeps landing)
      me.Pos = me.Disp = home;
      bool fire = !idle && f >= 18 + 36;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt, Fire = fire }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    }
    Console.Error.WriteLine("gloves: punch {0:0.00} t {1:0.00} jabs {2} hays {3} hits {4}", me.GlvPunch, me.GlvT, me.GlvJabs, me.GlvHays, me.GlvHits);
  }
}
