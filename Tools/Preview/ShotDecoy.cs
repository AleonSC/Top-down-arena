using System;
using System.Linq;
using System.Reflection;
using Arena;

// DECOY=<seconds into the scene>: the Healer Soldier's Decoy Dummy (Core/DecoyDummy.cs, Game/DecoyDummyLook.cs) played through
// the real game: you at (520, 540) with four grunts on the right firing small shots; dummies are thrown at 0.3 s (ahead),
// 1.0 s (lower) and 1.7 s (higher), and each grunt shoots at the nearest dummy up within 240 px of it, or at you. DECOYWEAR=1
// wears the second to 40% once it's up; DECOYPOP=<s> pops the first then (its confetti blast). Use with mode hud; crop about
// (440, 330)-(900, 750).
static partial class Shot {
  static void DecoyScene() {
    string ts = Environment.GetEnvironmentVariable("DECOY");
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
    me.Kit = Kit.Soldier; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Soldier, 0);
    me.MaxHp = me.Def.Hp; me.Hp = me.MaxHp; me.ShieldHp = 0f;
    V2 home = new V2(520f, 540f);
    V2[] throws = { new V2(700f, 520f), new V2(680f, 660f), new V2(660f, 400f) };
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 900f);
    var spots = new[] { new V2(860f, 420f), new V2(890f, 530f), new V2(860f, 650f), new V2(800f, 580f) };
    var mobs = new Enemy[spots.Length];
    for (int i = 0; i < spots.Length; i++) {
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, spots[i], 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 5000f; e.Pos = e.Disp = spots[i]; e.FireT = 99f; mobs[i] = e;
    }
    me.Slots[0].Id = AbilityId.DecoyDummy; me.Slots[0].MaxCharges = 3; me.Slots[0].Charges = 3; me.Slots[0].Timer = 0f;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    int frames = (int)Math.Round(float.Parse(ts, inv) * 60f);
    string ps = Environment.GetEnvironmentVariable("DECOYPOP");
    int popAt = ps != null ? (int)Math.Round(float.Parse(ps, inv) * 60f) : -1;
    bool wear = Environment.GetEnvironmentVariable("DECOYWEAR") != null;
    var rng = new Random(5);
    for (int f = 0; f < frames; f++) {
      for (int k = 0; k < throws.Length; k++)
        if (f == 18 + k * 42) { me.Pos = home; cast.Invoke(gm, new object[] { 1, 0, throws[k], false }); me.Slots[0].Charges = Math.Max(me.Slots[0].Charges, 1); }
      var ds = gm.Zones.Values.Where(z => z.Kind == ZoneKind.DecoyDummy).OrderBy(z => z.Id).ToList();
      foreach (var z in ds) if (popAt < 0 || f < popAt || z != ds[0]) z.BlockHp = Math.Max(z.BlockHp, z.SoakMax * (wear && z == ds.ElementAtOrDefault(1) ? 0.4f : 0.6f));  // (they stand through the scene)
      if (wear && ds.Count > 1 && Game.DecoyUp(ds[1])) { ds[1].BlockHp = Math.Min(ds[1].BlockHp, ds[1].SoakMax * 0.4f); }
      foreach (var z in ds) z.GuardFrac = z.BlockHp / z.SoakMax;
      if (f == popAt && ds.Count > 0) { ds[0].BlockHp = 0.01f; shoot.Invoke(gm, new object[] { Defs.SmallShot, ds[0].Pos + new V2(25f, 0f), new V2(-400f, 0f), 0f, 6f, 5f, 0, false, 0f }); }
      if (f >= 12 && f % 7 == 0) {
        var e = mobs[rng.Next(mobs.Length)];
        var lure = ds.Where(z => Game.DecoyUp(z) && z.Pos.DistanceTo(e.Pos) < Game.DecoyPull).OrderBy(z => z.Pos.DistanceTo(e.Pos)).FirstOrDefault();
        V2 to = (lure != null ? lure.Pos : home) + new V2(0f, (float)(rng.NextDouble() * 2 - 1) * 10f);
        shoot.Invoke(gm, new object[] { Defs.SmallShot, e.Pos, (to - e.Pos).Normalized() * 260f, 0f, 6f, 5f, 0, false, 0f });
      }
      for (int i = 0; i < mobs.Length; i++) if (mobs[i].Hp > 0f) { mobs[i].Pos = spots[i]; mobs[i].FireT = 99f; }
      me.Pos = home; me.InvulnT = 1f; me.Aim = (throws[0] - home).Angle;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = throws[0] }); gm.Update(1f / 60f);
    }
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    Console.WriteLine("decoy: " + string.Join(", ", gm.Zones.Values.Where(z => z.Kind == ZoneKind.DecoyDummy).Select(z => (z.GuardFrac).ToString("0.00"))));
  }
}
