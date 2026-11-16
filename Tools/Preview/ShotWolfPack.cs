using System;
using System.Reflection;
using Arena;

// WOLFPACK=pack|alpha|merge [WOLFT=<s>]: the Wolf Whistle's pack (Core/WolfPack.cs, Game/WolfLook.cs) played through the real
// game: you (DPS Summoner) stand at (420, 450) darting a line of grunts, the pack hunting them.
//   pack:  four wolves (two hurt, so their bars show) and six kills towards the fifth (the pips behind you).
//   alpha: an alpha of three wolves (Alt merged them) and one small wolf beside it, a kill or two towards the next.
//   merge: five wolves, Alt merges them at 0.6 s into an alpha of five.
//   lineup: posed, no fight, in front of you: a wolf trotting, one biting a grunt, a hurt one, an alpha of three and an
//          alpha of five, and the pips behind you. ZOOM=2.8, then crop the right half (your level-up ring is in the middle).
// WOLFT: seconds the fight runs before the shot (default 1.2). Use with mode hud and ZOOM=2.2 for a close look.
static partial class Shot {
  static void WolfPackScene() {
    string variant = Environment.GetEnvironmentVariable("WOLFPACK");
    if (variant == null) return;
    if (variant == "lineup") { WolfLineup(); return; }
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var addWolf = typeof(Game).GetMethod("AddWolf", flags);
    var alt = typeof(Game).GetMethod("HostWeaponDash", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Orbs.Clear(); gm.Minions.Clear();
    me.Kit = Kit.Summoner; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.WeaponOf(WeaponId.WolfWhistle);
    me.MaxHp = me.Def.Hp * Defs.KitHpMult(Kit.Summoner); me.Hp = me.MaxHp; me.ShieldHp = 0f; me.PackStarted = true;
    foreach (var sl in me.Slots) sl.Id = AbilityId.None;
    foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
    V2 home = new V2(420f, 450f);
    int ai = 0;
    foreach (var q in gm.Players.Values) if (q != me) { q.Pos = q.Disp = home + new V2(-80f, ai == 0 ? -170f : 170f); ai++; }
    Func<EnemyType, V2, Enemy> Mob = (type, at) => {
      var e = (Enemy)spawn.Invoke(gm, new object[] { type, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 5000f; e.Pos = e.Disp = at; e.FireT = 99f; return e;
    };
    var grunts = new[] { Mob(EnemyType.Grunt, new V2(545f, 400f)), Mob(EnemyType.Grunt, new V2(560f, 455f)), Mob(EnemyType.Grunt, new V2(540f, 510f)) };
    var spots = Array.ConvertAll(grunts, e => e.Pos);
    int n = variant == "alpha" ? 3 : variant == "merge" ? 5 : 4;
    for (int i = 0; i < n; i++) {
      var w = (Minion)addWolf.Invoke(gm, new object[] { me, (V2?)(home + V2.FromAngle(MathX.Tau * i / n + 0.4f) * 34f) });
      w.Aim = 0f;
    }
    if (variant == "alpha") alt.Invoke(gm, new object[] { me.Id, 31, home });  // three merge (the fourth comes after)
    if (variant == "alpha") { var w = (Minion)addWolf.Invoke(gm, new object[] { me, (V2?)(home + new V2(0f, 40f)) }); w.Aim = 0f; }
    me.PackKills = (byte)(variant == "pack" ? 6 : 3);
    int hurt = 0;
    foreach (var m in gm.Minions.Values) if (variant == "pack" && m.Kind == MinionKind.Wolf && hurt < 2) { m.Hp = m.MaxHp * (hurt == 0 ? 0.6f : 0.25f); hurt++; }
    me.Pos = me.Disp = home; me.Aim = 0f;
    int frames = (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("WOLFT") ?? "1.2", System.Globalization.CultureInfo.InvariantCulture) * 60f);
    bool merged = false;
    for (int f = 0; f < frames; f++) {
      float t = f / 60f;
      if (variant == "merge" && !merged && t >= 0.6f) { merged = true; alt.Invoke(gm, new object[] { me.Id, 31, home }); }
      me.Pos = home; me.InvulnT = 1f;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = grunts[1].Pos, Fire = true }); gm.Update(1f / 60f);
      for (int gi = 0; gi < grunts.Length; gi++) { var e = grunts[gi]; e.Hp = e.MaxHp; e.FireT = 99f; e.Pos = spots[gi]; e.KnockLeft = V2.Zero; }  // they stand still
    }
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    foreach (var m in gm.Minions.Values) m.Disp = m.Pos;
    int wolves = 0; foreach (var m in gm.Minions.Values) if (m.Kind == MinionKind.Wolf) { wolves++; Console.Error.WriteLine("wolfpack: wolf {0} scale {1:0.00} hp {2:0}/{3:0} alpha {4}", m.Id, m.Scale, m.Hp, m.MaxHp, m.Alpha); }
    Console.Error.WriteLine("wolfpack: {0} wolves, kills {1}", wolves, me.PackKills);
  }

  // lineup: (x, y) from you, aim, scale (alpha size), run, stride phase, biting cycle (-1 none), health
  static readonly float[][] lineup = {
    new[] { 75f, -48f, 0.25f, 1f, 1f, 0.6f, -1f, 1f },
    new[] { 160f, -52f, 0f, 1f, 0f, 0f, 0.58f, 1f },
    new[] { 62f, 42f, -0.15f, 1f, 1f, 2.4f, -1f, 0.3f },
    new[] { 118f, 52f, 0f, 3f, 1f, 0.2f, -1f, 0.75f },
    new[] { 185f, 48f, 0f, 5f, 0f, 0f, -1f, 1f },
  };

  static void WolfLineup() {
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Orbs.Clear(); gm.Minions.Clear(); gm.Enemies.Clear();
    me.Kit = Kit.Summoner; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.WeaponOf(WeaponId.WolfWhistle);
    me.MaxHp = me.Def.Hp * Defs.KitHpMult(Kit.Summoner); me.Hp = me.MaxHp; me.ShieldHp = 0f; me.PackStarted = true; me.PackKills = 7; me.LevelFlash = 0f;
    foreach (var sl in me.Slots) sl.Id = AbilityId.None;
    V2 home = new V2(640f, 450f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 150f + 100f * q.Id);
    me.Pos = me.Disp = home; me.Aim = 0f;
    int id = 9100;
    foreach (var L in lineup) {
      float spread = float.Parse(Environment.GetEnvironmentVariable("WOLFSPREAD") ?? "1", System.Globalization.CultureInfo.InvariantCulture);  // WOLFSPREAD=0.45 ZOOM=6: a close look
      V2 at = home + new V2(L[0], L[1]) * spread;
      var m = new Minion { Id = id++, Owner = me.Id, Kind = MinionKind.Wolf, Pos = at, Disp = at, Aim = L[2], T = -1f, Companion = true };
      int n = (int)L[3];
      m.Alpha = n > 1 ? n : 0; m.Scale = 1f + Game.AlphaScalePer * (n - 1);
      m.MaxHp = 20f * n; m.Hp = m.MaxHp * L[7];
      gm.Minions[m.Id] = m;
      if (L[6] >= 0f) {
        var d = Defs.Get(EnemyType.Grunt); V2 ga = at + V2.FromAngle(L[2]) * (d.Radius + 24f);
        var g = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, ga, 0f, 0, false });
        g.SpawnT = 0f; g.Pos = g.Disp = ga; g.FireT = 99f; g.Facing = MathX.Pi;
      }
    }
  }

  /// After the app is made: give each lineup wolf its stride and bite (BearViewOf keeps them: its last look is long ago).
  static void WolfPoses() {
    if (Environment.GetEnvironmentVariable("WOLFPACK") != "lineup") return;
    var bf = BindingFlags.NonPublic | BindingFlags.Instance;
    var views = typeof(ArenaApp).GetField("bearViews", bf).GetValue(app) as System.Collections.IDictionary;
    var vt = typeof(ArenaApp).GetNestedType("BearView", BindingFlags.NonPublic);
    if (views == null || vt == null) return;  // (an older look without the views)
    int id = 9100;
    foreach (var L in lineup) {
      var v = Activator.CreateInstance(vt);
      Action<string, object> put = (n, val) => vt.GetField(n).SetValue(v, val);
      put("Has", true); put("LastT", -1000f); put("Last", host.Game.Minions[id].Pos);
      put("Run", L[4]); put("Phase", L[5]); put("Biting", L[6] >= 0f); put("Cycle", Math.Max(0f, L[6]));
      views[id++] = v;
    }
  }
}
