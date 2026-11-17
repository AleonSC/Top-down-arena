using System;
using System.Linq;
using System.Reflection;
using Arena;

// COVER=<seconds into the scene>: the Tank Soldier's Deployable Cover (Core/DeployableCover.cs, Game/DeployableCoverLook.cs) played
// through the real game like the mockup (Docs/Mockups/deployable-cover.html, look A Stake Drop): you at (520, 540) facing the four
// grunts on the right, which fire small shots at you from 0.4 s; covers are thrown at 0.5 s (from there), 1.5 s (from higher up)
// and 2.5 s (from lower down), and you step back behind the first at 3 s. COVERWEAR=1 wears the second to 60% and the third to 20% once they're up. COVERBREAK=<s> breaks the first
// at that time. Use with mode hud; crop about (440, 330)-(900, 750).
static partial class Shot {
  static void CoverScene() {
    string ts = Environment.GetEnvironmentVariable("COVER");
    if (ts == null) return;
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
    me.Kit = Kit.Soldier; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Weapon = Defs.Weapon(Role.Tank, Kit.Soldier, 0);
    me.MaxHp = me.Def.Hp; me.Hp = me.MaxHp; me.ShieldHp = 0f;
    V2 home = new V2(520f, 540f);
    V2 foes = new V2(880f, 540f);
    V2[] stands = { home, new V2(530f, 410f), new V2(515f, 675f) };  // where you stand for each throw (back home after the last)
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 900f);
    var spots = new[] { new V2(840f, 420f), new V2(880f, 520f), new V2(850f, 640f), new V2(780f, 560f) };
    var mobs = new Enemy[spots.Length];
    for (int i = 0; i < spots.Length; i++) {
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, spots[i], 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 5000f; e.Pos = e.Disp = spots[i]; e.FireT = 99f; mobs[i] = e;
    }
    me.Slots[0].Id = AbilityId.DeployableCover; me.Slots[0].MaxCharges = 2; me.Slots[0].Charges = 2; me.Slots[0].Timer = 0f;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    int frames = (int)Math.Round(float.Parse(ts, inv) * 60f);
    string bs = Environment.GetEnvironmentVariable("COVERBREAK");
    int breakAt = bs != null ? (int)Math.Round(float.Parse(bs, inv) * 60f) : -1;
    bool wear = Environment.GetEnvironmentVariable("COVERWEAR") != null;
    var rng = new Random(5);
    V2 cursor = foes, at = home;
    for (int f = 0; f < frames; f++) {
      for (int k = 0; k < 3; k++)
      {
        if (f == 6 + k * 60) at = stands[k];
        if (f == 30 + k * 60) { me.Pos = at; me.Slots[0].Charges = 2; cast.Invoke(gm, new object[] { 1, 0, cursor, false }); }
      }
      if (f == 180) at = home;
      var covers = gm.Zones.Values.Where(z => z.Kind == ZoneKind.Cover).OrderBy(z => z.Id).ToList();
      if (wear && f == 200 && covers.Count == 3) { covers[1].BlockHp = covers[1].SoakMax * 0.6f; covers[2].BlockHp = covers[2].SoakMax * 0.2f; foreach (var z in covers) z.GuardFrac = z.BlockHp / z.SoakMax; }
      if (f == breakAt && covers.Count > 0) { covers[0].BlockHp = 0.01f; shoot.Invoke(gm, new object[] { Defs.SmallShot, covers[0].Pos + V2.FromAngle(covers[0].Aim) * 30f, V2.FromAngle(covers[0].Aim) * -400f, 0f, 6f, 5f, 0, false, 0f }); }
      if (f >= 24 && f % 6 == 0) {
        var e = mobs[rng.Next(mobs.Length)];
        V2 to = at + new V2(0f, (float)(rng.NextDouble() * 2 - 1) * 40f);
        shoot.Invoke(gm, new object[] { Defs.SmallShot, e.Pos, (to - e.Pos).Normalized() * 260f, 0f, 6f, 5f, 0, false, 0f });
      }
      for (int i = 0; i < mobs.Length; i++) if (mobs[i].Hp > 0f) { mobs[i].Pos = spots[i]; mobs[i].FireT = 99f; }
      me.Pos = at; me.InvulnT = 1f; me.Aim = (cursor - home).Angle;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cursor }); gm.Update(1f / 60f);
    }
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    Console.WriteLine("cover: " + string.Join(", ", gm.Zones.Values.Where(z => z.Kind == ZoneKind.Cover).Select(z => (z.BlockHp).ToString("0"))));
  }
}
