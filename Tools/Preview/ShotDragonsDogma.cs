using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// DOGMA=<s since the cast>: MOCKUP of Dragon's Dogma, a proposed Tank Ninja ultimate (Game/DragonsDogmaLook.cs). You (a Tank
// Ninja, NINJA=0) stand in the middle; eight enemies round you; the dragon pours out of you, eats them one by one along
// its path and coils back in at 4.6 s. Each enemy is removed the moment it's eaten. Use with mode hud;
// a GIF: NINJA=0 Tools/Preview/clip.sh dogma hud VAR=DOGMA=0:5.2:0.1
static partial class Shot {
  static void DragonsDogmaScene() {
    string ts = Environment.GetEnvironmentVariable("DOGMA");
    if (ts == null) return;
    float T = float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture);
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 home = new V2(800f, 450f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    me.Pos = me.Disp = home; me.Aim = -MathX.Pi / 2f;
    // The victims, in the order it eats them, and when.
    var foes = new (EnemyType type, V2 at, float eat)[] {
      (EnemyType.Grunt,   home + new V2(-60f, -230f), 0.85f),
      (EnemyType.Charger, home + new V2(-300f, -140f), 1.35f),
      (EnemyType.Grunt,   home + new V2(-360f, 110f), 1.85f),
      (EnemyType.Shielder,home + new V2(-120f, 250f), 2.35f),
      (EnemyType.Grunt,   home + new V2(190f, 210f), 2.85f),
      (EnemyType.Sniper,  home + new V2(380f, 60f), 3.35f),
      (EnemyType.Splitter,home + new V2(300f, -200f), 3.85f),
      (EnemyType.Grunt,   home + new V2(90f, -170f), 4.25f),
    };
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var knots = new List<V2> { home, home + new V2(30f, -90f) };
    var kt = new List<float> { 0f, 0.4f };
    var victims = new List<V2>(); var eats = new List<float>(); var cols = new List<Rgba>();
    foreach (var f in foes) {
      knots.Add(f.at); kt.Add(f.eat); victims.Add(f.at); eats.Add(f.eat);
      var e = (Enemy)spawn.Invoke(gm, new object[] { f.type, f.at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 1e5f; e.CageT = 999f; e.Pos = e.Disp = f.at;
      cols.Add(e.Def.Color);
      if (T >= f.eat) gm.Enemies.Remove(e.Id);
    }
    knots.Add(home + new V2(-70f, -60f)); kt.Add(4.55f);
    knots.Add(home); kt.Add(4.85f);
    ArenaApp.DogmaKnots = knots.ToArray(); ArenaApp.DogmaKnotT = kt.ToArray();
    ArenaApp.DogmaVictims = victims.ToArray(); ArenaApp.DogmaEatT = eats.ToArray(); ArenaApp.DogmaVictimCol = cols.ToArray();
    ArenaApp.DogmaT = T;
    for (int i = 0; i < 2; i++) { me.Pos = home; me.InvulnT = 1f; host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(0f, -100f) }); gm.Update(1f / 60f); }
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
  }
}
