using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// TOTEM=<seconds since the last plant>: the Tank Hemomancer's Blood Totem (Core/BloodTotem.cs, Game/BloodTotemLook.cs) played
// through the real game (TOTEMCAP=1 mastered): you at (800, 470), three totems planted 0.2 s apart round you (left, upper right,
// lower right), with grunts, a Heavy and enemies outside the reach walking in. A totem rises in 0.25 s and its veins grow out
// over 0.25 s more. Try 0.1, 0.4, 1.5, 4. Use with `ZOOM=1.3 shot.sh x hud 1 6 1` (6 s in, so the wave is fighting).
static partial class Shot {
  static void BloodTotemScene() {
    string ts = Environment.GetEnvironmentVariable("TOTEM");
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
    me.Kit = Kit.Hemomancer; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Weapon = Defs.Weapon(Role.Tank, Kit.Hemomancer, 0);
    me.MaxHp = me.Hp = 400f;
    V2 home = new V2(800f, 470f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    EnemyType heavy = EnemyType.Grunt;
    foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) { if ((int)et >= Defs.Enemies.Length) continue; var d = Defs.Get(et); if (d != null && d.Heavy && !d.IsBoss) { heavy = et; break; } }
    V2[] spotsT = { home + new V2(-300f, -20f), home + new V2(230f, -170f), home + new V2(260f, 170f) };
    var rng = new Random(11);
    var mobs = new List<Enemy>();
    for (int i = 0, tries = 0; mobs.Count < 18 && tries < 3000; tries++) {
      V2 c = spotsT[i % 3];
      float a = (float)rng.NextDouble() * MathX.Tau, d = i < 13 ? 50f + (float)rng.NextDouble() * 120f : 220f + (float)rng.NextDouble() * 60f;
      V2 at = c + V2.FromAngle(a) * d;
      if (mobs.Exists(o => o.Pos.CloserThan(at, 40f)) || at.CloserThan(home, 90f)) continue;
      var e = (Enemy)spawn.Invoke(gm, new object[] { i == 4 ? heavy : EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 900f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e); i++;
    }
    me.Slots[0].Id = AbilityId.BloodTotem; me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f;
    if (Environment.GetEnvironmentVariable("TOTEMCAP") != null) me.MasteryXp[AbilityId.BloodTotem] = 99999f;
    int frames = (int)Math.Round((float.Parse(ts, inv) + 0.7f) * 60f);
    for (int f = 0; f < frames; f++) {
      for (int k = 0; k < 3; k++)
        if (f == 18 + k * 12) { me.Slots[0].Timer = 0f; me.Slots[0].Charges = 1; cast.Invoke(gm, new object[] { me.Id, 0, spotsT[k], false }); }
      foreach (var e in mobs) e.FireT = 99f;
      me.Pos = me.Disp = home; me.Hp = me.MaxHp;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = spotsT[0] }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    }
    Console.Error.WriteLine("totem: enemies {0} zones {1}", gm.Enemies.Count, gm.Zones.Count);
  }
}
