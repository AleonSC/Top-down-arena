using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// LANCERAIN=<seconds since the cast>: the Healer Knight's Holy Lance Rain (Core/HolyLanceRain.cs, Game/HolyLanceRainLook.cs)
// played through the real game: you at (700, 480) as a Healer Knight walking right, an ally beside you, a ring of grunts round
// you; the rain cast 0.3 s into the scene. LRCAP=1 casts it mastered; FXOFF=1 the plain look. Use with mode hud.
static partial class Shot {
  static void LanceRainScene() {
    string ts = Environment.GetEnvironmentVariable("LANCERAIN");
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
    me.Kit = Kit.Knight; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Knight, 0);
    V2 home = new V2(700f, 480f);
    Player ally = null;
    foreach (var q in gm.Players.Values) if (q != me) { if (ally == null) ally = q; q.Pos = q.Disp = new V2(150f, 820f); }
    if (ally != null) { ally.Pos = ally.Disp = home + new V2(90f, 70f); ally.Hp = ally.MaxHp * 0.5f; }
    var rng = new Random(7);
    var mobs = new List<Enemy>();
    for (int i = 0; i < 16; i++) {
      float a = (float)rng.NextDouble() * MathX.Tau, d = 90f + (float)rng.NextDouble() * 260f;
      V2 at = home + new V2((float)Math.Cos(a) * d, (float)Math.Sin(a) * d * 0.8f);
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 90f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e);
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.HolyLanceRain; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    if (Environment.GetEnvironmentVariable("LRCAP") != null) me.MasteryXp[AbilityId.HolyLanceRain] = 99999f;
    me.Hp = me.MaxHp * 0.6f;
    int frames = (int)Math.Round((float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture) + 0.3f) * 60f);
    V2 pos = home;
    for (int f = 0; f < frames; f++) {
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, home + new V2(200f, 0f), false });
      if (f > 18 && f < 18 + 180) pos += new V2(40f / 60f, 0f);
      foreach (var e in mobs) if (e.Hp > 0f) { e.FireT = 99f; e.Disp = e.Pos; }
      me.Pos = me.Disp = pos; me.InvulnT = 1f; me.Aim = 0f;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = pos + new V2(200f, 0f) }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    }
    int alive = 0; foreach (var e in mobs) if (e.Hp > 0f && gm.Enemies.ContainsKey(e.Id)) alive++;
    Console.Error.WriteLine("lancerain: {0} of {1} enemies left, hp {2:0}/{3:0}, ally {4:0}", alive, mobs.Count, me.Hp, me.MaxHp, ally != null ? ally.Hp : 0f);
  }
}
