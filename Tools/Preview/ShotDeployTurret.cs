using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Arena;

// DEPLOYTURRET=<seconds since the cast>: the DPS Soldier's Deployable Turret mockup (Core/DeployableTurret.cs, Game/DeployTurretLook.cs) played through the real
// game (DEPLOYCAP=1 mastered, DEPLOYHIT=<s after the cast> a 150 hit on it): you at (800, 470), grunts, runners and a Heavy walking in from every side and a fresh one every few frames, the turret
// cast 0.3 s into the scene. It drops (0-0.55 s), unfolds (to 1.1), fires for 10 s (spool full about 1.2 s in), overheats and blows
// at 11.9 s. Try 0.3, 0.8, 3, 6, 10, 11.5, 12. FXOFF=1 the plain look. Use with `shot.sh x hud 1 6 1` (6 s in, so the wave is fighting).
static partial class Shot {
  static void DeployTurretScene() {
    string ts = Environment.GetEnvironmentVariable("DEPLOYTURRET");
    if (ts == null) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var look = typeof(ArenaApp).GetMethod("UpdateDeployTurrets", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
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
    int us = Upgrades.UltSlot; me.Slots[us].Id = AbilityId.DeployableTurret; me.Slots[us].Charges = 1; me.Slots[us].Timer = 0f;
    if (Environment.GetEnvironmentVariable("DEPLOYCAP") != null) me.MasteryXp[AbilityId.DeployableTurret] = 99999f;
    V2 aimAt = home + new V2(100f, 0f);
    int frames = (int)Math.Round((float.Parse(ts, inv) + 0.3f) * 60f), n = 100;
    for (int f = 0; f < frames; f++) {
      if (f == 18) { cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, aimAt, false }); }
      mobs.RemoveAll(e => e.Hp <= 0f);
      if (f % 5 == 0 && mobs.Count < 40) add(n++);
      foreach (var e in mobs) { e.FireT = 99f; if (!e.Pos.CloserThan(home, 70f)) e.Pos += (home - e.Pos).Normalized() * (e.Def.Heavy ? 30f : 55f) / 60f; e.Disp = e.Pos; }
      me.Pos = me.Disp = home; me.Firing = me.FiringSeen = f > 18;  // holding Fire once it's cast
      var nearest = mobs.Count > 0 ? mobs.OrderBy(e => e.Pos.DistanceSq(home)).First() : null;
      if (nearest != null) { aimAt = nearest.Pos; me.Aim = (aimAt - home).Angle; }
      if (f == 18 + (int)(float.Parse(Environment.GetEnvironmentVariable("DEPLOYHIT") ?? "-1", inv) * 60f) && Environment.GetEnvironmentVariable("DEPLOYHIT") != null) { me.DevGod = false; typeof(Game).GetMethod("Damage", flags).Invoke(gm, new object[] { me, 150f, "Test" }); me.DevGod = true; }  // DEPLOYHIT=<s after the cast>: a 150 hit on it
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt, Fire = f > 18 }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      look.Invoke(app, new object[] { gm.AnimT });
    }
    Console.Error.WriteLine("deployturret: enemies {0} zones {1}", gm.Enemies.Count, gm.Zones.Count);
  }
}
