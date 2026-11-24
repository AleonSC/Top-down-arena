using System;
using System.Reflection;
using Arena;

// COLSHEET=1 [COLLOOK=0-3]: four Colossi in a row, one per phase (Awakening, Crumbling, Molten Core, Last Stand), in the
// look COLLOOK picks (ColossusBodyLook.cs; 0 = today's). Mode hud, e.g. COLSHEET=1 COLLOOK=1 shot.sh x hud 3 1 1.
static partial class Shot {
  static void ColossusSheetScene() {
    if (Environment.GetEnvironmentVariable("COLSHEET") == null) return;
    ArenaApp.ColLook = int.Parse(Environment.GetEnvironmentVariable("COLLOOK") ?? "0");
    var gm = host.Game; gm.Bot = false;
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    foreach (var q in gm.Players.Values) q.Pos = q.Disp = new V2(-400f, -400f);
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
    var hp = new[] { 0.9f, 0.5f, 0.2f, 0.05f };
    for (int i = 0; i < 4; i++) {
      var at = new V2(485f + i * 210f, 450f);
      var en = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Colossus, at, 0f, 0, false });
      en.SpawnT = 0f; en.Spawning = false; en.Pos = en.Disp = at; en.Facing = MathX.Pi / 2f; en.Hp = en.MaxHp * hp[i];
      en.Colossus = new ColossusState { Stage = (ColStage)i, TurnT = 999f };
    }
  }
}
