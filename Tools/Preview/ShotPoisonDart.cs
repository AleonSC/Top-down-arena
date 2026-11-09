using System;
using System.Reflection;
using Arena;

// DART=<seconds into the scene>: the Soldier DPS's Poison Dart (Core/PoisonDart.cs, Game/PoisonDartLook.cs) played through
// the real game like the mockup's scene (Docs/Mockups/poison-dart.html): you at (420, 450), seven grunts standing about,
// the dart flies at 0.5 s to the one nearest the cursor, it dies at 2.2 s and the poison spreads, one of those dies at
// 3.9 s and it spreads on. DARTCAP=1 casts it mastered; FXOFF=1 the plain looks. Use with mode hud; crop about
// (380, 300)-(880, 560) to set beside the mockup (its canvas is (360, 240)-(840, 580) of the arena).
static partial class Shot {
  static void PoisonDartScene() {
    string ts = Environment.GetEnvironmentVariable("DART");
    if (ts == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Soldier; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Soldier, 0);
    me.MaxHp = me.Def.Hp; me.Hp = me.MaxHp; me.ShieldHp = 0f;
    V2 off = new V2(360f, 240f), home = new V2(60f, 210f) + off, cursor = new V2(236f, 132f) + off;
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 800f);
    var spots = new[] { new V2(250f, 150f), new V2(318f, 214f), new V2(196f, 246f), new V2(352f, 128f), new V2(420f, 262f), new V2(448f, 176f), new V2(140f, 120f) };
    var mobs = new Enemy[spots.Length];
    for (int i = 0; i < spots.Length; i++) {
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, spots[i] + off, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 5000f; e.Pos = e.Disp = spots[i] + off; e.FireT = 99f; mobs[i] = e;
    }
    me.Slots[0].Id = AbilityId.PoisonDart; me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f;
    if (Environment.GetEnvironmentVariable("DARTCAP") != null) me.MasteryXp[AbilityId.PoisonDart] = 99999f;
    int frames = (int)Math.Round(float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture) * 60f);
    for (int f = 0; f < frames; f++) {
      if (f == 30) cast.Invoke(gm, new object[] { 1, 0, cursor, false });
      if (f == 132) mobs[0].Hp = 0f;
      if (f == 234) { var s = Array.Find(mobs, e => e.Hp > 0f && e.VenomT > 0f && e == mobs[1]) ?? Array.Find(mobs, e => e.Hp > 0f && e.VenomT > 0f); if (s != null) s.Hp = 0f; }
      for (int i = 0; i < mobs.Length; i++) if (mobs[i].Hp > 0f) { mobs[i].Pos = spots[i] + off; mobs[i].FireT = 99f; }
      me.Pos = home; me.InvulnT = 1f; me.Aim = (cursor - home).Angle;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cursor }); gm.Update(1f / 60f);
    }
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    int n = 0; foreach (var e in gm.Enemies.Values) if (e.VenomT > 0f) n++;
    Console.WriteLine("poison dart: {0} enemies poisoned, {1} left", n, gm.Enemies.Count);
  }
}
