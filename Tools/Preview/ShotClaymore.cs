using System;
using System.Linq;
using System.Reflection;
using Arena;

// CLAYMORE=<seconds into the scene>: the DPS Soldier's Claymore (Core/Claymore.cs, Game/ClaymoreLook.cs) played through the real
// game: you at (520, 540) facing right; claymores go down at 0.2 s (ahead), 0.5 s (up-right) and 0.8 s (down-right), each arming
// for 0.6 s; grunts stand beside the cones. CLAYTRIP=<s> walks a grunt into the first claymore's cone then (the fuse, then the
// blast 0.15 s later). CLAYCAP=1: mastered (a spare blast: it re-arms). Use with mode hud; crop about (440, 330)-(960, 750).
static partial class Shot {
  static void ClaymoreScene() {
    string ts = Environment.GetEnvironmentVariable("CLAYMORE");
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
    if (Environment.GetEnvironmentVariable("CLAYCAP") != null) me.MasteryXp[AbilityId.Claymore] = 1e6f;
    V2 home = new V2(520f, 540f);
    V2[] plants = { new V2(680f, 540f), new V2(660f, 420f), new V2(660f, 660f) };
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 900f);
    var spots = new[] { new V2(930f, 420f), new V2(900f, 690f), new V2(590f, 330f) };
    var mobs = new Enemy[spots.Length];
    for (int i = 0; i < spots.Length; i++) {
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, spots[i], 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 5000f; e.Pos = e.Disp = spots[i]; e.FireT = 99f; mobs[i] = e;
    }
    me.Slots[0].Id = AbilityId.Claymore; me.Slots[0].MaxCharges = 2; me.Slots[0].Charges = 2; me.Slots[0].Timer = 0f;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    int frames = (int)Math.Round(float.Parse(ts, inv) * 60f);
    string ps = Environment.GetEnvironmentVariable("CLAYTRIP");
    int tripAt = !string.IsNullOrEmpty(ps) ? (int)Math.Round(float.Parse(ps, inv) * 60f) : -1;
    for (int f = 0; f < frames; f++) {
      for (int k = 0; k < plants.Length; k++)
        if (f == 12 + k * 18) { me.Pos = home; me.Slots[0].Charges = 1; cast.Invoke(gm, new object[] { 1, 0, plants[k], false }); }
      if (tripAt >= 0 && f >= tripAt) spots[0] = new V2(820f, 560f);  // the first grunt steps into the first claymore's cone
      for (int i = 0; i < mobs.Length; i++) if (mobs[i].Hp > 0f && (i != 0 || f < tripAt + 9 || tripAt < 0)) { mobs[i].Pos = spots[i]; mobs[i].FireT = 99f; }
      me.Pos = home; me.InvulnT = 1f; me.Aim = 0f;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(300f, 0f) }); gm.Update(1f / 60f);
    }
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    Console.WriteLine("claymore: " + string.Join(", ", gm.Zones.Values.Where(z => z.Kind == ZoneKind.Claymore).Select(z => z.GuardFrac.ToString("0.00"))));
  }
}
