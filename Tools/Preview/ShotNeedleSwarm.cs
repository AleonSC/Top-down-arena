using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// NEEDLESWARM=<seconds held>: the DPS Hemomancer's Needle Swarm (Core/NeedleSwarm.cs, Game/NeedleSwarmLook.cs) played through the
// real game, the look flown every step: you at (800, 470) holding Fire with the cursor on a grunt 200 px to the right, a second grunt
// below and a Heavy up left in reach, enemy shots coming in from the left onto the ring. The first tendril reaches its grunt at about
// 1.1 s and wraps it; the next starts then. NSALT=<s> also throws the Alt's fan that far in. Try 0.6, 1.5, 3. Use with
// `shot.sh x hud 1 6 1` (6 s in, so the wave is fighting); ZOOM=2 CAMAT=800,470 for a close look.
static partial class Shot {
  static void NeedleSwarmScene() {
    string ts = Environment.GetEnvironmentVariable("NEEDLESWARM");
    if (ts == null) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
    var look = typeof(ArenaApp).GetMethod("NsUpdate", flags);
    typeof(ArenaApp).GetField("juiceGame", flags).SetValue(app, gm);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Hemomancer; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Hemomancer, 0);
    me.Hp = me.MaxHp;
    V2 home = new V2(800f, 470f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    EnemyType heavy = EnemyType.Grunt;
    foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) { if ((int)et >= Defs.Enemies.Length) continue; var d = Defs.Get(et); if (d != null && d.Heavy && !d.IsBoss) { heavy = et; break; } }
    var spots = new Dictionary<Enemy, V2>();
    Func<EnemyType, V2, float, Enemy> put = (type, at, hp) => {
      var e = (Enemy)spawn.Invoke(gm, new object[] { type, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = hp; e.Pos = e.Disp = at; e.FireT = 99f; spots[e] = at; return e;
    };
    var first = put(EnemyType.Grunt, home + new V2(200f, -10f), 5000f);
    put(EnemyType.Grunt, home + new V2(60f, 190f), 5000f);
    put(heavy, home + new V2(-170f, -150f), 9000f);
    put(EnemyType.Grunt, home + new V2(420f, 120f), 5000f);
    V2 aimAt = first.Pos;
    me.Aim = (aimAt - home).Angle;
    float held = float.Parse(ts, inv);
    string altS = Environment.GetEnvironmentVariable("NSALT");
    int altAt = altS == null ? -1 : (int)Math.Round(float.Parse(altS, inv) * 60f);
    int frames = (int)Math.Round(held * 60f);
    for (int f = 0; f < frames; f++) {
      if (f % 9 == 0)  // a steady stream of shots from the left, onto the ring
        shoot.Invoke(gm, new object[] { Defs.SmallShot, home + new V2(-420f, -80f + (f * 37 % 160)), (home - (home + new V2(-420f, -80f + (f * 37 % 160)))).Normalized() * 320f, 0f, 3f, 6f, 0, false, 0f });
      foreach (var e in spots.Keys) { e.FireT = 99f; e.Pos = e.Disp = spots[e]; }
      me.Pos = me.Disp = home; me.Hp = me.MaxHp;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt, Fire = true, WeaponAction = f == altAt || f == altAt + 1 }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      look.Invoke(app, null);
    }
    Console.Error.WriteLine("needleswarm: needles {0} tendrils {1},{2},{3} bound {4},{5},{6}", me.NsCount, me.NsTen[0], me.NsTen[1], me.NsTen[2], me.NsBound[0], me.NsBound[1], me.NsBound[2]);
  }
}
