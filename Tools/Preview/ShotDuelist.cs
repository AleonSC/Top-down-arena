using System;
using System.Reflection;
using Arena;

// The Duelist's body looks (Game/DuelistBodyLook.cs). DUELLOOK=0-3: 0 today's look, 1 Maestro, 2 Masked Duke, 3 Phantom.
// DUELSHEET=<1-8>: one Duelist in the middle in that pose: 1 walking, 2 showing its lunge line, 3 lunging, 4 on guard,
// 5 saluting (To the Death), 6 staggered, 7 a Masquerade decoy, 8 last stand (battle-worn). DUELSHEET=all: all eight.
// e.g. DUELLOOK=1 DUELSHEET=2 ZOOM=2.6 NINJA=0 Tools/Preview/shot.sh x hud 3 1 1
static partial class Shot {
  static void DuelistSheetScene() {
    string sheet = Environment.GetEnvironmentVariable("DUELSHEET");
    ArenaApp.DuelistLookVariant = int.Parse(Environment.GetEnvironmentVariable("DUELLOOK") ?? "0");
    if (sheet == null) return;
    var gm = host.Game; gm.Bot = false;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear();
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
    float face = -0.5f;
    V2 dir = V2.FromAngle(face);
    Action<DuelState, Enemy>[] sets = {
      (d, e) => { },
      (d, e) => { d.Turn = DuelTurn.Lunge; d.Dash = DuelDash.Lunge; d.DashWarn = 0.3f; d.DashFrom = e.Pos; d.DashAng = face; d.DashLen = 260f; },
      (d, e) => { d.Turn = DuelTurn.Lunge; d.Dash = DuelDash.Lunge; d.DashWarn = 0f; d.DashFrom = e.Pos - dir * 170f; d.DashAng = face; d.DashLen = 260f; },
      (d, e) => { d.Turn = DuelTurn.Guard; },
      (d, e) => { d.Stage = DuelStage.ToTheDeath; d.Turn = DuelTurn.Salute; d.SaluteT = 0.35f; },
      (d, e) => { d.Stage = DuelStage.ToTheDeath; d.StaggerT = 1.5f; },
      (d, e) => { },
      (d, e) => { d.Stage = DuelStage.LastStand; },
    };
    int only = 0; int.TryParse(sheet, out only);
    for (int i = 0; i < sets.Length; i++) {
      if (only > 0 && i != only - 1) continue;
      var at = only > 0 ? new V2(Game.Width / 2f, Game.Height / 2f) : new V2(Game.Width / 2f + (i % 4 - 1.5f) * 340f, Game.Height / 2f + (i / 4 - 0.5f) * 300f);
      var e = (Enemy)spawn.Invoke(gm, new object[] { i == 6 ? EnemyType.Decoy : EnemyType.Duelist, at, 0f, 0, false });
      e.Spawning = false; e.SpawnT = 0f; e.Disp = e.Pos = at; e.Facing = face;
      if (i != 6) { e.Duel = new DuelState(); sets[i](e.Duel, e); }
    }
    foreach (var p in gm.Players.Values) { p.Pos = p.Disp = only > 0 ? new V2(Game.Width / 2f + 150f, Game.Height / 2f + 160f) : new V2(Game.Width / 2f, Game.Height / 2f + 380f); p.Aim = MathX.Pi; }
  }
}
