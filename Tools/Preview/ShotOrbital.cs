using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// ORBITAL=<seconds since the cast>: the DPS Soldier's Orbital Laser (Core/OrbitalLaser.cs, Game/OrbitalLaserLook.cs) played
// through the real game: you at (430, 620) as a DPS Soldier, a pack of grunts and a Heavy round (1000, 420), the strike called
// on them 0.3 s into the scene. ORBCAP=1 calls it mastered (wider, the beam ploughing on); FXOFF=1 the plain look. Use with mode hud.
static partial class Shot {
  static void OrbitalScene() {
    string ts = Environment.GetEnvironmentVariable("ORBITAL");
    if (ts == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Soldier; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Soldier, 0);
    V2 home = new V2(430f, 620f), target = new V2(1000f, 420f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    EnemyType heavy = EnemyType.Grunt;
    foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) { if ((int)et >= Defs.Enemies.Length) continue; var d = Defs.Get(et); if (d != null && d.Heavy && !d.IsBoss) { heavy = et; break; } }
    var rng = new Random(7);
    var mobs = new List<Enemy>();
    for (int i = 0; i < 26; i++) {
      float a = (float)rng.NextDouble() * MathX.Tau, d = i < 18 ? 40f + (float)rng.NextDouble() * 230f : 330f + (float)rng.NextDouble() * 200f;
      V2 at = target + new V2((float)Math.Cos(a) * d * 1.15f, (float)Math.Sin(a) * d * 0.85f);
      var e = (Enemy)spawn.Invoke(gm, new object[] { i == 3 ? heavy : EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = i == 3 ? 400f : 80f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e);
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.OrbitalLaser; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    if (Environment.GetEnvironmentVariable("ORBCAP") != null) me.MasteryXp[AbilityId.OrbitalLaser] = 99999f;
    int frames = (int)Math.Round((float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture) + 0.3f) * 60f);
    for (int f = 0; f < frames; f++) {
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, target, false });
      foreach (var e in mobs) if (e.Hp > 0f) { e.FireT = 99f; if (!e.Pos.CloserThan(home, 120f)) e.Pos += (home - e.Pos).Normalized() * (e.Def.Heavy ? 14f : 24f) / 60f; e.Disp = e.Pos; }
      me.Pos = me.Disp = home; me.InvulnT = 1f; me.Aim = (float)Math.Atan2(target.Y - home.Y, target.X - home.X);
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = target }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    }
    int alive = 0; foreach (var e in mobs) if (e.Hp > 0f && gm.Enemies.ContainsKey(e.Id)) alive++;
    Console.Error.WriteLine("orbital: {0} of {1} enemies left, ult cooldown {2:0.0}", alive, mobs.Count, me.Slots[Upgrades.UltSlot].Timer);
  }
}
