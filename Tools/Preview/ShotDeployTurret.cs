using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// DEPLOYTURRET=<seconds since the cast>: the DPS Soldier's Deployable Turret mockup (Game/DeployTurretLook.cs) drawn in the real
// arena: you at (800, 470), grunts, runners and a Heavy walking in from every side and a fresh one every few frames, the turret
// cast 0.3 s into the scene. It drops (0-0.55 s), unfolds (to 1.1), fires for 10 s (spool full about 1.2 s in), overheats and blows
// at 11.9 s. Try 0.3, 0.8, 3, 6, 10, 11.5, 12. FXOFF=1 the plain look. Use with mode hud.
static partial class Shot {
  static void DeployTurretScene() {
    string ts = Environment.GetEnvironmentVariable("DEPLOYTURRET");
    if (ts == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var look = typeof(ArenaApp).GetMethod("UpdateDeployTurret", flags);
    typeof(ArenaApp).GetField("juiceGame", flags).SetValue(app, gm);  // so the first drawn frame doesn't clear what the look saw
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
    var rng = new Random(23);
    var mobs = new List<Enemy>();
    Func<int, Enemy> add = i => {
      float a = (float)rng.NextDouble() * MathX.Tau, d = 300f + (float)rng.NextDouble() * 300f;
      V2 at = home + new V2((float)Math.Cos(a) * d * 1.3f, (float)Math.Sin(a) * d * 0.75f);
      var e = (Enemy)spawn.Invoke(gm, new object[] { i % 9 == 4 ? heavy : EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = i % 9 == 4 ? 260f : 70f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e); return e;
    };
    for (int i = 0; i < 28; i++) add(i);
    var mock = new DeployTurretMock { Pos = home, Aim = -0.6f };
    int frames = (int)Math.Round((float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture) + 0.3f) * 60f), n = 100;
    for (int f = 0; f < frames; f++) {
      if (f == 18) ArenaApp.deployTurretMock = mock;
      mobs.RemoveAll(e => e.Hp <= 0f);
      if (f % 5 == 0 && mobs.Count < 40) add(n++);
      foreach (var e in mobs) { e.FireT = 99f; if (!e.Pos.CloserThan(home, 70f)) e.Pos += (home - e.Pos).Normalized() * (e.Def.Heavy ? 30f : 55f) / 60f; e.Disp = e.Pos; }
      me.Pos = me.Disp = home; me.InvulnT = 1f; me.Aim = mock.Aim;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(100f, 0f) }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      look.Invoke(app, new object[] { gm.AnimT });
    }
    Console.Error.WriteLine("deployturret: t {0:0.0} spool {1:0.00} heat {2:0.00} enemies {3}", mock.T, mock.Spool, mock.Heat, gm.Enemies.Count);
  }
}
