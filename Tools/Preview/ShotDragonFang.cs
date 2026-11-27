using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// FANG=<bites> [FANGAFTER=<s>]: the DPS Ninja's Dragon Fang Kunai (Core/DragonFang.cs, Game/DragonFangLook.cs) played through the
// real game: you at (300, 450), 13 grunts in a loose grid round (760, 450); the kunai is thrown at the first and the scene stops FANGAFTER
// (default 0.08) seconds after its <bites>-th bite, so the dragon is as fierce as that many bites make it. Try 1, 4, 8, 12.
// FXOFF=1 the plain look. Use with `ZOOM=1.25 CAMAT=760,450 shot.sh x hud 1 6 1`.
static partial class Shot {
  static void DragonFangScene() {
    string ss = Environment.GetEnvironmentVariable("FANG");
    if (ss == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var track = typeof(ArenaApp).GetMethod("TrackFang", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Ninja; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Ninja, 2); me.Stats.FangBites = 11;  // (three Dragon's Hunger cards: all 12 bites)
    V2 mine = new V2(300f, 450f);
    int n = 0;
    foreach (var q in gm.Players.Values) if (q != me) { q.Pos = q.Disp = new V2(200f, 300f + 300f * n++); }
    var home = new Dictionary<int, V2>();
    for (int i = 0; i < 13; i++) {
      int row = i / 4, col = i % 4;  // a loose hex grid round (760, 450)
      V2 at = new V2(560f + 140f * col + (row % 2) * 70f, 255f + 130f * row);
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 5000f; e.Pos = e.Disp = at; e.FireT = 99f; home[e.Id] = at;
    }
    int want = int.Parse(ss);
    float after = float.Parse(Environment.GetEnvironmentVariable("FANGAFTER") ?? "0.08", System.Globalization.CultureInfo.InvariantCulture);
    V2 aim = new V2(560f, 255f);
    UnityEngine.Time.deltaTime = 1f / 60f;
    int bites = 0; float since = -1f;
    for (int f = 0; f < 600; f++) {
      foreach (var q in gm.Players.Values) q.Hp = q.MaxHp;
      foreach (var e in gm.Enemies.Values) if (home.ContainsKey(e.Id)) { e.Pos = e.Disp = home[e.Id]; e.FireT = 99f; }
      me.Pos = mine; me.Aim = (aim - mine).Angle; me.AimAt = aim;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aim, Fire = f == 2 }); gm.Update(1f / 60f);
      me.Pos = me.Disp = mine;
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      foreach (var b in gm.Bullets.Values) if (b.Kind == Defs.DragonFang) track.Invoke(app, new object[] { b, f / 60f });
      foreach (var fx in gm.Effects) if (fx.Kind == FxKind.DragonFangBite) bites = Math.Max(bites, (int)fx.Pos2.Y);
      if (bites >= want && since < 0f) since = 0f;
      if (since >= 0f && (since += 1f / 60f) >= after) break;
    }
    Console.Error.WriteLine("dragonfang: {0} bites", bites);
  }
}
