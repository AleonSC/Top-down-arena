using System;
using System.Linq;
using System.Reflection;
using Arena;

// WWARD=<seconds into the scene>: the Healer Elementalist's Water Ward (Core/WaterWard.cs, Game/WaterWardLook.cs, look C of
// Docs/Mockups/water-ward.html) played through the real game: you at (620, 560), cast every 3 s from 0.2 s (2 drops each, up to 10).
// A grunt walks in from the right from WWENEMY s (default 6.5) and another from the left 1 s later (WWENEMY=-1: none); WWSHOOT=1 adds a
// shooter above firing at you every 0.8 s from 7 s. Use with mode hud; ZOOM=2 crops round you.
static partial class Shot {
  static void WaterWardScene() {
    string ts = Environment.GetEnvironmentVariable("WWARD");
    if (ts == null) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags);
    var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Elementalist; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Elementalist, 0);
    me.MaxHp = me.Def.Hp; me.Hp = me.MaxHp; me.ShieldHp = 0f;
    V2 home = new V2(620f, 560f), cursor = new V2(620f, 300f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 880f);
    me.Slots[0].Id = AbilityId.WaterWard; me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f;
    float enemyAt = float.Parse(Environment.GetEnvironmentVariable("WWENEMY") ?? "6.5", inv);
    bool shots = Environment.GetEnvironmentVariable("WWSHOOT") != null;
    int frames = (int)Math.Round(float.Parse(ts, inv) * 60f);
    Enemy[] mobs = new Enemy[2];
    V2[] from = { new V2(900f, 575f), new V2(330f, 540f) };
    V2 gun = new V2(640f, 300f);
    var rng = new Random(3);
    for (int f = 0; f < frames; f++) {
      float t = f / 60f;
      if (f % 180 == 12) { me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f; cast.Invoke(gm, new object[] { me.Id, 0, cursor, false }); }
      for (int i = 0; i < 2; i++) {
        float at = enemyAt + i;
        if (enemyAt >= 0f && mobs[i] == null && t >= at) {
          var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, from[i], 0f, 0, false });
          e.SpawnT = 0f; e.Hp = e.MaxHp = 5000f; e.FireT = 99f; mobs[i] = e;
        }
        var m = mobs[i];
        if (m != null && m.Hp > 0f) { V2 d = (home - from[i]).Normalized(); float go = Math.Min((t - at) * 45f, (home - from[i]).Length - 20f); m.Pos = m.Disp = from[i] + d * go; m.FireT = 99f; }
      }
      if (shots && t >= 7f && f % 48 == 0) {
        V2 to = home + new V2((float)(rng.NextDouble() * 2 - 1) * 30f, 0f);
        shoot.Invoke(gm, new object[] { Defs.SmallShot, gun, (to - gun).Normalized() * 220f, 0f, 6f, 5f, 0, false, 0f });
      }
      me.Pos = home; me.InvulnT = 1f; me.Aim = (cursor - home).Angle;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cursor }); gm.Update(1f / 60f);
    }
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    Console.WriteLine("water ward: {0} drops ({1} falling), splashes {2}, bursts {3}", me.WwOff.Count, me.WwFall.Count(x => x > 0f),
      gm.Effects.Count(x => x.Kind == FxKind.WwSplash), gm.Effects.Count(x => x.Kind == FxKind.WwBurst));
  }
}
