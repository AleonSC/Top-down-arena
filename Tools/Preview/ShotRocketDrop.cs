using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// ROCKETDROP=<seconds since the cast> [ROCKETCAP=1]: the DPS Soldier's Rocket Drop (Core/RocketDrop.cs, Game/RocketDropLook.cs)
// played through the real game: you as a DPS Soldier at (600, 560), the pod called 0.3 s into the scene onto (720, 470). It lands
// at 1.1 s and opens by 1.5 s; you walk over and take the launcher (about 2.0 s), then fire at a pack of grunts round (1000, 420)
// from 2.2 s. Try 0.5, 0.9, 1.25, 1.7, 2.45 (a rocket in flight), 2.62 (the blast), 3.4 (fire pools). ROCKETCAP=1 mastered
// (bomblets). Use with `ZOOM=1.1 CAMAT=780,480 shot.sh x hud 1 6 1`.
static partial class Shot {
  static void RocketDropScene() {
    string ss = Environment.GetEnvironmentVariable("ROCKETDROP");
    if (ss == null) return;
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
    V2 drop = new V2(720f, 470f), mine = new V2(600f, 560f), pack = new V2(1000f, 420f);
    int n = 0;
    foreach (var q in gm.Players.Values) if (q != me) { q.Pos = q.Disp = new V2(380f, 300f + 140f * n++); }
    var rng = new Random(7);
    var mobs = new List<Enemy>();
    var home = new Dictionary<int, V2>();
    for (int i = 0; i < 12; i++) {
      float a = i / 12f * MathX.Tau + (float)rng.NextDouble() * 0.4f, d = 20f + (float)rng.NextDouble() * 120f;
      V2 at = pack + new V2((float)Math.Cos(a) * d, (float)Math.Sin(a) * d * 0.9f);
      var e = (Enemy)spawn.Invoke(gm, new object[] { i % 5 == 4 ? EnemyType.Brute : EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 900f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e); home[e.Id] = at;
    }
    for (int i = 0; i < 3; i++) {  // a few under the pod (the slam kills them)
      V2 at = drop + V2.FromAngle(i * 2.1f) * 45f;
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 100f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e); home[e.Id] = at;
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.RocketDrop; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    if (Environment.GetEnvironmentVariable("ROCKETCAP") != null) me.MasteryXp[AbilityId.RocketDrop] = 99999f;
    int frames = (int)Math.Round((float.Parse(ss, System.Globalization.CultureInfo.InvariantCulture) + 0.3f) * 60f);
    UnityEngine.Time.deltaTime = 1f / 60f;
    for (int f = 0; f < frames; f++) {
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, drop, false });
      Zone pod = null;
      foreach (var z in gm.Zones.Values) if (z.Kind == ZoneKind.RocketDrop) pod = z;
      bool armed = Game.RocketOn(me);
      if (!armed && pod != null && Game.RocketReady(pod)) mine += (pod.Pos - mine).LimitLength(260f / 60f);
      V2 aim = armed ? pack : drop;
      foreach (var q in gm.Players.Values) q.Hp = q.MaxHp;  // (enemies hold their fire anyway)
      foreach (var e in mobs) if (e.Hp > 0f) { e.FireT = 99f; e.Pos = home[e.Id]; e.Disp = e.Pos; }  // (held in place)
      me.Pos = mine; me.Aim = (aim - mine).Angle; me.AimAt = aim;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aim, Fire = armed }); gm.Update(1f / 60f);
      me.Pos = me.Disp = mine; me.Aim = (aim - mine).Angle; me.AimAt = aim;
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    }
    int fires = 0; foreach (var z in gm.Zones.Values) if (z.Kind == ZoneKind.RocketFire) fires++;
    int rockets = 0; foreach (var b in gm.Bullets.Values) if (b.Kind == Defs.RocketShell) rockets++;
    Console.Error.WriteLine("rocketdrop: launcher {0:0.0}s shots {1} rockets {2} fires {3} zones {4}", me.RocketT, me.RocketShots, rockets, fires, gm.Zones.Count);
  }
}
