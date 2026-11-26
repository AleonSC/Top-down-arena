using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// CHAKRA=<seconds since the cast>: the Healer Ninja's Chakra Reserves (Core/ChakraReserves.cs, Game/ChakraReservesLook.cs)
// played through the real game (CHAKRACAP=1 mastered): you at (640, 480), a hurt teammate at (860, 430) with their
// abilities on cooldown, and grunts off to the right; the cast 0.3 s into the scene. The thread reaches them by 0.22 s, the
// lotus blooms and fades by 0.95 s, the Surge runs to 5 s. Try 0.1, 0.3, 0.6, 2.0. Use with `ZOOM=2 shot.sh x hud 1 6 2`.
static partial class Shot {
  static void ChakraScene() {
    string ts = Environment.GetEnvironmentVariable("CHAKRA");
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
    me.Kit = Kit.Ninja; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Ninja, 0);
    V2 home = new V2(640f, 480f), allyAt = new V2(860f, 430f);
    Player ally = null;
    foreach (var q in gm.Players.Values) if (q != me) { if (ally == null) ally = q; else q.Pos = q.Disp = new V2(150f, 820f); }
    var mobs = new List<Enemy>();
    var spots = new Dictionary<Enemy, V2>();
    V2[] at = { new V2(1120f, 330f), new V2(1160f, 470f), new V2(1090f, 590f), new V2(1220f, 400f) };
    foreach (var p in at) {
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, p, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 9999f; e.Pos = e.Disp = p; e.FireT = 99f; mobs.Add(e); spots[e] = p;
    }
    me.Slots[0].Id = AbilityId.ChakraReserves; me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f;
    if (Environment.GetEnvironmentVariable("CHAKRACAP") != null) me.MasteryXp[AbilityId.ChakraReserves] = 99999f;
    if (ally != null) {
      ally.Hp = ally.MaxHp * 0.3f;
      ally.Slots[0].Charges = 0; ally.Slots[0].Timer = 8f;
      ally.Aim = (at[1] - allyAt).Angle;
    }
    me.Aim = (allyAt - home).Angle;
    int frames = (int)Math.Round((float.Parse(ts, inv) + 0.3f) * 60f);
    for (int f = 0; f < frames; f++) {
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, 0, allyAt, false });
      foreach (var e in mobs) { e.FireT = 99f; e.Pos = e.Disp = spots[e]; }
      me.Pos = me.Disp = home;
      if (ally != null) { ally.Pos = ally.Disp = allyAt + new V2(0f, 18f * (float)Math.Sin(f / 20f)); }
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = allyAt }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    }
    Console.Error.WriteLine("chakra: ally hp {0:0} surge {1:0.00} effects {2}", ally == null ? 0f : ally.Hp, ally == null ? 0f : ally.CrSurgeT, gm.Effects.Count);
  }
}
