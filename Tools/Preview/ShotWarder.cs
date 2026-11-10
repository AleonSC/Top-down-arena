using System;
using System.Reflection;
using Arena;

// WARDER=<seconds into the scene>: the Warder and its twelve-cell bubbles played through the real game like the mockups
// (Docs/Mockups/warder-rework.html, shield-tells.html). A Warder at (600, 350), three grunts round it, a Shielded elite
// off to the side and you far away. It charges from 0.2 s and wards at 1.6 s; then you shoot grunt 0 from the lower left
// (3.4 s), grunt 1 from the east (4.2 s, 4.4 s) and the elite from two sides (3.0 s, 4.6 s); grunt 0 takes more at 5.2 s.
// FXOFF=1 for the plain looks. Use with mode hud; crop about (380, 200)-(980, 560).
static partial class Shot {
  static void WarderScene() {
    string ts = Environment.GetEnvironmentVariable("WARDER");
    if (ts == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    foreach (var q in gm.Players.Values) q.Pos = q.Disp = new V2(150f, 900f);
    var spots = new[] { new V2(500f, 300f), new V2(700f, 290f), new V2(615f, 440f), new V2(470f, 480f) };
    var types = new[] { EnemyType.Grunt, EnemyType.Grunt, EnemyType.Grunt, EnemyType.Grunt };
    var mobs = new Enemy[4];
    for (int i = 0; i < 4; i++) {
      var e = (Enemy)spawn.Invoke(gm, new object[] { types[i], spots[i], 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 600f; e.Pos = e.Disp = spots[i]; e.FireT = 99f; mobs[i] = e;
    }
    mobs[3].Traits = (byte)EliteTrait.Shielded; mobs[3].BubbleHp = mobs[3].BubbleMax = mobs[3].MaxHp * Game.ShieldedShare;
    var w = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Warder, new V2(600f, 350f), 0f, 0, false });
    w.SpawnT = 0f; w.Hp = w.MaxHp = 5000f; w.Pos = w.Disp = new V2(600f, 350f); w.FireT = 99f; w.WardT = 1.6f;
    int frames = (int)Math.Round(float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture) * 60f);
    Action<int, V2, float> shoot = (who, from, dmg) => { me.Pos = from; hurt.Invoke(gm, new object[] { mobs[who], dmg, me.Id }); };
    for (int f = 0; f < frames; f++) {
      double sec = f / 60.0;
      if (f == 180) shoot(3, new V2(470f, 700f), 40f);
      if (f == 204) shoot(0, new V2(300f, 500f), 30f);
      if (f == 252) shoot(1, new V2(950f, 290f), 30f);
      if (f == 264) shoot(1, new V2(950f, 290f), 30f);
      if (f == 276) shoot(3, new V2(700f, 480f), 60f);
      if (f == 312) { shoot(0, new V2(300f, 500f), 45f); shoot(0, new V2(300f, 200f), 45f); }
      for (int i = 0; i < 4; i++) { mobs[i].Pos = spots[i]; mobs[i].FireT = 99f; }
      w.Pos = new V2(600f, 350f); w.FireT = 99f; w.Hp = w.MaxHp;
      me.Pos = new V2(150f, 900f); me.InvulnT = 1f;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = new V2(600f, 350f) }); gm.Update(1f / 60f);
    }
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    Console.Error.WriteLine(string.Format("phase {0}, cells {1:X} / {2:X} / {3:X} / elite {4:X}", w.Phase, Game.CellMask(mobs[0]), Game.CellMask(mobs[1]), Game.CellMask(mobs[2]), Game.CellMask(mobs[3])));
  }
}
