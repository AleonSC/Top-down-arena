using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// SHOCK=<seconds since the cast>: the Shockwave ability (Core/Shockwave.cs, Game/ShockwaveLook.cs) played through the real
// game: you at (600, 450) in a ring of grunts and one Heavy, enemy shots raining in, the wave cast at 0.5 s into the scene.
// SHOCKCAP=1 casts it mastered (300 px); FXOFF=1 the plain ring. Use with mode hud.
static partial class Shot {
  static void ShockwaveScene() {
    string ts = Environment.GetEnvironmentVariable("SHOCK");
    if (ts == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
    var look = typeof(ArenaApp).GetMethod("UpdateShockwaveLook", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 home = new V2(600f, 450f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    EnemyType heavy = EnemyType.Grunt;
    foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) { if ((int)et >= Defs.Enemies.Length) continue; var d = Defs.Get(et); if (d != null && d.Heavy && !d.IsBoss) { heavy = et; break; } }
    var rng = new Random(7);
    var mobs = new List<Enemy>();
    for (int i = 0; i < 12; i++) {
      float a = i / 12f * MathX.Tau + (float)rng.NextDouble() * 0.4f, d = 110f + (float)rng.NextDouble() * 190f;
      var e = (Enemy)spawn.Invoke(gm, new object[] { i == 7 ? heavy : EnemyType.Grunt, home + V2.FromAngle(a) * d, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 5000f; e.Pos = e.Disp = home + V2.FromAngle(a) * d; e.FireT = 99f; mobs.Add(e);
    }
    Func<bool, Bullet> rain = init => {
      float a = (float)rng.NextDouble() * MathX.Tau, d = init ? 40f + (float)rng.NextDouble() * 300f : 330f + (float)rng.NextDouble() * 40f;
      V2 at = home + V2.FromAngle(a) * d;
      float dir = (home - at).Angle + ((float)rng.NextDouble() - 0.5f) * 0.9f;
      return shoot.Invoke(gm, new object[] { (byte)10, at, V2.FromAngle(dir) * (70f + 80f * (float)rng.NextDouble()), 0f, 9f, 8f, -1, false, 0f }) as Bullet;
    };
    for (int i = 0; i < 90; i++) rain(true);
    me.Slots[0].Id = AbilityId.Shockwave; me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f;
    if (Environment.GetEnvironmentVariable("SHOCKCAP") != null) me.MasteryXp[AbilityId.Shockwave] = 99999f;
    int frames = (int)Math.Round((float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture) + 0.5f) * 60f);
    for (int f = 0; f < frames; f++) {
      if (f == 30) cast.Invoke(gm, new object[] { me.Id, 0, home + new V2(100f, 0f), false });
      foreach (var e in mobs) e.FireT = 99f;
      int live = 0; foreach (var b in gm.Bullets.Values) if (Defs.IsEnemyShot(b.Kind)) live++;
      if (f < 30) for (; live < 90; live++) rain(false);
      me.Pos = home; me.InvulnT = 1f; me.Aim = 0f;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(100f, 0f) }); gm.Update(1f / 60f);
      look.Invoke(app, new object[] { gm.AnimT });
    }
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
  }
}
