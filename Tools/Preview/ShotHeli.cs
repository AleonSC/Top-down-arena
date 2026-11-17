using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// HELI=<seconds since the cast>: the DPS Soldier's Attack Helicopter (Core/AttackHelicopter.cs, Game/AttackHelicopterLook.cs) played
// through the real game: you at (800, 470) as a DPS Soldier, grunts, runners and a Heavy walking in from every side, the gunship
// called 0.3 s into the scene. HELICAP=1 calls it mastered (twin guns); FXOFF=1 the plain look. Use with mode hud.
static partial class Shot {
  static void HeliScene() {
    string ts = Environment.GetEnvironmentVariable("HELI");
    if (ts == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    var look = typeof(ArenaApp).GetMethod("UpdateHelis", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Soldier; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Soldier, 0);
    V2 home = new V2(800f, 470f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    EnemyType heavy = EnemyType.Grunt;
    foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) { if ((int)et >= Defs.Enemies.Length) continue; var d = Defs.Get(et); if (d != null && d.Heavy && !d.IsBoss) { heavy = et; break; } }
    var rng = new Random(11);
    var mobs = new List<Enemy>();
    for (int i = 0; i < 16; i++) {
      float a = i / 16f * MathX.Tau + (float)rng.NextDouble() * 0.3f, d = 230f + (float)rng.NextDouble() * 330f;
      V2 at = home + new V2((float)Math.Cos(a) * d * 1.3f, (float)Math.Sin(a) * d * 0.75f);
      var e = (Enemy)spawn.Invoke(gm, new object[] { i == 5 ? heavy : EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = i == 5 ? 260f : 70f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e);
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.AttackHelicopter; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    if (Environment.GetEnvironmentVariable("HELICAP") != null) me.MasteryXp[AbilityId.AttackHelicopter] = 99999f;
    int frames = (int)Math.Round((float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture) + 0.3f) * 60f);
    for (int f = 0; f < frames; f++) {
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, home + new V2(100f, 0f), false });
      foreach (var e in mobs) if (e.Hp > 0f) { e.FireT = 99f; if (!e.Pos.CloserThan(home, 90f)) e.Pos += (home - e.Pos).Normalized() * (e.Def.Heavy ? 30f : 50f) / 60f; e.Disp = e.Pos; }
      me.Pos = me.Disp = home; me.InvulnT = 1f; me.Aim = 0f;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(100f, 0f) }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      look.Invoke(app, new object[] { gm.AnimT });
    }
    Console.Error.WriteLine("heli: guns {0} targets {1},{2} rounds {3} time left {4:0.0}", me.HeliGuns, me.HeliTgt, me.HeliTgt2, me.HeliRounds, me.UltT);
  }
}
