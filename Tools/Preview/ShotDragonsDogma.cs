using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// DOGMA=<s since the cast>: Dragon's Dogma, the Tank Ninja's second ultimate (Core/DragonsDogma.cs, Game/DragonsDogmaLook.cs),
// cast and played through the real game: you (NINJA=0) in the middle, eight enemies round you (a Juggernaut among them, heavy, so only
// bitten); the dragon hunts them down and coils back in. DOGMACAP=1 mastered (7 s). Use with mode hud; a GIF:
//   NINJA=0 Tools/Preview/clip.sh dogma hud 3 1 1 DOGMA=0:5.4:0.05
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
    var foes = new (EnemyType type, V2 at)[] {
      (EnemyType.Grunt,   home + new V2(-60f, -230f)),
      (EnemyType.Charger, home + new V2(-300f, -140f)),
      (EnemyType.Grunt,   home + new V2(-360f, 110f)),
      (EnemyType.Shielder,home + new V2(-120f, 250f)),
      (EnemyType.Juggernaut, home + new V2(190f, 210f)),
      (EnemyType.Sniper,  home + new V2(380f, 60f)),
      (EnemyType.Splitter,home + new V2(300f, -200f)),
      (EnemyType.Grunt,   home + new V2(90f, -170f)),
    };
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var spots = new Dictionary<int, V2>();
    foreach (var f in foes) {
      var e = (Enemy)spawn.Invoke(gm, new object[] { f.type, f.at, 0f, 0, false });
      e.SpawnT = 0f; e.Pos = e.Disp = f.at;
      if (f.type == EnemyType.Juggernaut) e.Hp = e.MaxHp = 1e5f;
      spots[e.Id] = f.at;
    }
    var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(gm);
    sq.Clear(); for (int i = 0; i < 50; i++) sq.Add(EnemyType.Grunt);  // (no new ones spawn in while it hunts)
    if (Environment.GetEnvironmentVariable("DOGMACAP") != null) me.MasteryXp[AbilityId.DragonsDogma] = 1e6f;
    gm.State = MatchState.Fighting;
    var slot = me.Slots[Upgrades.UltSlot]; slot.Charges = 1; slot.Timer = 0f;
    typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null).Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, home, false });
    int frames = (int)Math.Round(T * 60f);
    for (int f = 0; f < frames; f++) {
      foreach (var id in new List<int>(gm.Enemies.Keys)) {
        if (!spots.ContainsKey(id)) { gm.Enemies.Remove(id); continue; }
        var e = gm.Enemies[id]; e.Pos = spots[id];
      }
      gm.Bullets.Clear();
      gm.State = MatchState.Fighting; me.Pos = home; me.InvulnT = 1f; me.Aim = -MathX.Pi / 2f;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(0f, -100f) }); gm.Update(1f / 60f);
    }
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    Console.Error.WriteLine("dogma: on {0}, eaten {1}, bites {2}, enemies left {3}", me.DogmaOn, me.DogmaEaten, me.DogmaBites, gm.Enemies.Count);
  }
}
