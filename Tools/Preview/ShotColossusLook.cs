using System;
using System.Reflection;
using Arena;

// COLSHEET=1: four Colossi in a row, one per phase (Awakening, Crumbling, Molten Core, Last Stand; ColossusBodyLook.cs).
// COLSHEET=2: its poses in a row: a stomp winding up, the slam, bracing for a charge, charging, a throw, dazed
// (COLSTAGE=<0-3>: in that phase, default 0). Mode hud, e.g. COLSHEET=1 ZOOM=1.8 CAMAT=800,450 shot.sh x hud 3 1 1
// (COLSHEET=2: ZOOM=1.1).
static partial class Shot {
  static void ColossusSheetScene() {
    string sheet = Environment.GetEnvironmentVariable("COLSHEET");
    if (sheet == null) return;
    var gm = host.Game; gm.Bot = false;
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    foreach (var q in gm.Players.Values) q.Pos = q.Disp = new V2(-400f, -400f);
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
    bool poses = sheet == "2";
    int n = poses ? 6 : 4;
    var hp = new[] { 0.9f, 0.5f, 0.2f, 0.05f };
    int stage = int.Parse(Environment.GetEnvironmentVariable("COLSTAGE") ?? "0");
    for (int i = 0; i < n; i++) {
      var at = poses ? new V2(800f + (i - 2.5f) * 220f, 450f) : new V2(485f + i * 210f, 450f);
      var en = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Colossus, at, 0f, 0, false });
      en.SpawnT = 0f; en.Spawning = false; en.Pos = en.Disp = at; en.Facing = MathX.Pi / 2f;
      var c = en.Colossus = new ColossusState { Stage = (ColStage)(poses ? stage : i), TurnT = 999f, ChargeDir = MathX.Pi / 2f };
      en.Hp = en.MaxHp * hp[(int)c.Stage];
      if (!poses) continue;
      switch (i) {
        case 0: c.Rings.Add(new StompRing { Center = at, Radius = en.Radius + 6f, Warn = Game.RingWarn * 0.15f, Gap1 = 1f }); break;  // fists up
        case 1: c.Rings.Add(new StompRing { Center = at, Radius = en.Radius + 16f, Warn = 0f, Gap1 = 1f }); break;  // the slam
        case 2: c.ChargeWarn = Game.ColChargeWarn * 0.2f; break;
        case 3: c.ChargeLeft = 400f; break;
        case 4: c.Drops.Add(new ColDrop { Kind = ColDrop.Boulder, From = at, Pos = at + new V2(260f, 160f), T = 0.05f, Dur = 1f, Radius = 40f }); break;
        case 5: c.DazeT = 1f; break;
      }
    }
  }
}
