using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// EXCALIBUR=<seconds since the cast>: the DPS Knight's Excalibur ultimate (Core/Excalibur.cs, Game/ExcaliburLook.cs) played
// through the real game: you at (700, 470) facing right with grunts in front, the cast 0.3 s into the scene, Fire held from the
// end of the draw (0.5 s). Try 0.25 (the draw), 0.62 (mid-swing, first wave out), 0.9 (two waves), 1.75 (the Judgement's chop),
// 1.95 (its wave). EXCIDLE=1 holds Fire off; EXCEND=1 shows the burst at the end. Use with `ZOOM=1.1 shot.sh x hud 1 6 1`.
static partial class Shot {
  static void ExcaliburScene() {
    string ts = Environment.GetEnvironmentVariable("EXCALIBUR");
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
    me.Kit = Kit.Knight; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Knight, 0);
    V2 home = new V2(700f, 470f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    var mobs = new List<Enemy>();
    var spots = new Dictionary<Enemy, V2>();
    V2[] at = { new V2(860f, 440f), new V2(880f, 520f), new V2(1010f, 380f), new V2(1060f, 500f), new V2(1180f, 430f), new V2(1240f, 560f), new V2(1300f, 360f), new V2(960f, 600f) };
    foreach (var a in at) {
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, a, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 4000f; e.Pos = e.Disp = a; e.FireT = 99f; mobs.Add(e); spots[e] = a;
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.Excalibur; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    V2 aimAt = home + new V2(300f, 0f);
    me.Aim = 0f;
    bool idle = Environment.GetEnvironmentVariable("EXCIDLE") != null, end = Environment.GetEnvironmentVariable("EXCEND") != null;
    int frames = (int)Math.Round((float.Parse(ts, inv) + 0.3f) * 60f);
    for (int f = 0; f < frames; f++) {
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, aimAt, false });
      if (end && f == 19) me.ExcT = Game.ExcDraw + 0.02f;
      foreach (var e in mobs) { e.FireT = 99f; e.Pos = e.Disp = spots[e]; }  // (held in place)
      me.Pos = me.Disp = home;
      bool fire = !idle && !end && f >= 18 + 30;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt, Fire = fire }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    }
    Console.Error.WriteLine("excalibur: swing {0:0.00} t {1:0.00} swings {2} judges {3} hits {4}", me.ExcSwing, me.ExcT, me.ExcSwings, me.ExcJudges, me.ExcHits);
  }
}
