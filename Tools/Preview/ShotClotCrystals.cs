using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// CLOTCRYSTALS=<seconds since the cast>: the Healer Hemomancer's Clot Crystals (Core/ClotCrystals.cs, Game/ClotCrystalsLook.cs)
// played through the real game (CRYCAP=1 mastered, four crystals): you at (800, 470) in a ring of grunts 260 px out firing at
// you, the cast 0.3 s into the scene. The crystals rise over 0.45 s, then jerk at the shots that come in. CRYHURT=<0-1> sets
// their health left at the cast (cracks and the arc). Try 0.2, 1, 2.5, 6. Use with `ZOOM=1.8 shot.sh x hud 1 6 1`.
static partial class Shot {
  static void ClotCrystalsScene() {
    string ts = Environment.GetEnvironmentVariable("CLOTCRYSTALS");
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
    me.Kit = Kit.Hemomancer; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Hemomancer, 0);
    me.MaxHp = me.Hp = 400f;
    V2 home = new V2(800f, 470f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    var mobs = new List<Enemy>();
    for (int i = 0; i < 7; i++) {
      V2 at = home + V2.FromAngle(-2.6f + i * 0.86f) * 260f;
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 9000f; e.Pos = e.Disp = at; e.FireT = 0.2f + 0.15f * i; mobs.Add(e);
    }
    me.Slots[0].Id = AbilityId.ClotCrystals; me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f;
    if (Environment.GetEnvironmentVariable("CRYCAP") != null) me.MasteryXp[AbilityId.ClotCrystals] = 99999f;
    string hurt = Environment.GetEnvironmentVariable("CRYHURT");
    int frames = (int)Math.Round((float.Parse(ts, inv) + 0.3f) * 60f);
    for (int f = 0; f < frames; f++) {
      if (f == 18) {
        cast.Invoke(gm, new object[] { me.Id, 0, home + new V2(100f, 0f), false });
        if (hurt != null) foreach (var m in gm.Minions.Values) if (m.Kind == MinionKind.ClotCrystal) m.BlockHp = m.MaxHp * float.Parse(hurt, inv);
      }
      foreach (var e in mobs) e.Pos = e.Disp = home + (e.Pos - home).Normalized() * 260f;
      me.Pos = me.Disp = home; me.Hp = me.MaxHp;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(100f, 0f) }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    }
    foreach (var m in gm.Minions.Values) Console.Error.WriteLine("crystal {0}: health {1:0}/{2:0}, state {3}", m.Id, m.BlockHp, m.MaxHp, m.Stacks);
  }
}
