using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Arena;

// BALLISTA=<seconds since the cast>: the DPS Knight's Ballista (Core/Ballista.cs, Game/BallistaLook.cs) played through the real game: you at
// (800, 470) as the DPS Knight, grunts, runners and a Heavy walking in from the right and a fresh one every few frames, the ballista called
// 0.3 s into the scene, 70 px ahead. It falls (0-0.6 s), lands; BALLISTARIDE=<s> (default 0.8) you climb on and hold Fire: a bolt every 0.9 s
// (8, 10 with BALLISTACAP=1) until it breaks 0.6 s after the last. BALLISTARIDE=-1 nobody rides it (the MOUNT prompt shows). Try 0.3, 1, 3.2, 5,
// 8.2. FXOFF=1 the plain look. Use with `shot.sh x hud 1 6 1` (6 s in, so the wave is fighting).
static partial class Shot {
  static void BallistaScene() {
    string ts = Environment.GetEnvironmentVariable("BALLISTA");
    if (ts == null) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var vis = typeof(ArenaApp).GetMethod("BallistaVis", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    typeof(ArenaApp).GetField("juiceGame", flags).SetValue(app, gm);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Knight; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Knight, 2);  // (the Rapier: its glow stays off the bow)
    V2 home = new V2(700f, 470f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    EnemyType heavy = EnemyType.Grunt;
    foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) { if ((int)et >= Defs.Enemies.Length) continue; var d = Defs.Get(et); if (d != null && d.Heavy && !d.IsBoss) { heavy = et; break; } }
    var rng = new Random(31);
    var mobs = new List<Enemy>();
    Func<int, Enemy> add = i => {
      float a = ((float)rng.NextDouble() - 0.5f) * 1.6f, d = 420f + (float)rng.NextDouble() * 380f;
      V2 at = home + new V2((float)Math.Cos(a) * d, (float)Math.Sin(a) * d * 0.7f);
      var e = (Enemy)spawn.Invoke(gm, new object[] { i % 9 == 4 ? heavy : EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = i % 9 == 4 ? 600f : 300f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e); return e;
    };
    for (int i = 0; i < 24; i++) add(i);
    for (int k = 0; k < Upgrades.UltSlot; k++) me.Slots[k].Id = AbilityId.None;
    me.Slots[0].Id = Upgrades.FirstAbility(Role.Dps, Kit.Knight, 0); me.Slots[0].Charges = 1;
    me.Slots[Upgrades.UltSlot].Id = AbilityId.Ballista; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    if (Environment.GetEnvironmentVariable("BALLISTACAP") != null) me.MasteryXp[AbilityId.Ballista] = 99999f;
    float ride = float.Parse(Environment.GetEnvironmentVariable("BALLISTARIDE") ?? "0.8", inv);
    V2 aimAt = home + new V2(200f, 0f);
    int frames = (int)Math.Round((float.Parse(ts, inv) + 0.3f) * 60f), n = 100;
    for (int f = 0; f < frames; f++) {
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, aimAt, false });
      bool riding = gm.OnBallista(me);
      if (ride >= 0f && !riding && f == 18 + (int)(ride * 60f)) gm.UseBallista();
      mobs.RemoveAll(e => e.Hp <= 0f);
      if (f % 6 == 0 && mobs.Count < 34) add(n++);
      foreach (var e in mobs) { e.FireT = 99f; if (!e.Pos.CloserThan(home, 120f)) e.Pos += (home - e.Pos).Normalized() * (e.Def.Heavy ? 30f : 50f) / 60f; e.Disp = e.Pos; }
      if (!riding) me.Pos = me.Disp = home;
      me.Firing = me.FiringSeen = riding;
      var nearest = mobs.Count > 0 ? mobs.OrderBy(e => e.Pos.DistanceSq(me.Pos)).First() : null;
      if (nearest != null) { aimAt = nearest.Pos; me.Aim = (aimAt - me.Pos).Angle; }
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt, Fire = riding }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      foreach (var z in gm.Zones.Values.ToList()) if (z.Kind == ZoneKind.Ballista) vis.Invoke(app, new object[] { z, gm.AnimT });
    }
    Console.Error.WriteLine("ballista: enemies {0} bolts fired {1} riding {2}", gm.Enemies.Count, gm.BallistaBoltsFired, gm.OnBallista(me));
  }
}
