using System;
using System.Reflection;
using Arena;

// The Duelist's body, the Maestro (Game/DuelistBodyLook.cs), posed.
// DUELSHEET=<1-8>: one Duelist in the middle in that pose: 1 walking, 2 showing its lunge line, 3 lunging, 4 on guard,
// 5 saluting (To the Death), 6 staggered, 7 a Masquerade decoy, 8 last stand (battle-worn). DUELSHEET=all: all eight.
// DUELTRANS=<1 Masquerade|2 To the Death>,<s into the 2 s change>: the phase change's set piece (one Duelist in the middle).
// DUELLAST=<s>: the last stand that long after it began (its entrance runs 1.4 s), one Duelist in the middle in its circle.
// DUELDECOY=<s>: the Masquerade that long after its decoys appeared (their entrance runs 0.6 s): the Duelist and three
// decoys sliding out from it to their places round you.
// e.g. DUELSHEET=2 ZOOM=3 KNIGHT=1 CAMAT=800,450 Tools/Preview/shot.sh x hud 3 1 1
static partial class Shot {
  static void DuelistSheetScene() {
    string sheet = Environment.GetEnvironmentVariable("DUELSHEET"), trans = Environment.GetEnvironmentVariable("DUELTRANS");
    if (trans != null) {
      var tp = trans.Split(',');
      var to = (DuelStage)int.Parse(tp[0]);
      float into = float.Parse(tp[1], System.Globalization.CultureInfo.InvariantCulture);
      host.Game.Enemies.Clear(); host.Game.Bullets.Clear(); host.Game.Effects.Clear(); host.Game.Bot = false;
      var sp = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var at = new V2(Game.Width / 2f, Game.Height / 2f);
      var de = (Enemy)sp.Invoke(host.Game, new object[] { EnemyType.Duelist, at, 0f, 0, false });
      de.Spawning = false; de.SpawnT = 0f; de.Disp = de.Pos = at; de.Facing = -0.5f;
      de.Hp = de.MaxHp * (to == DuelStage.Masquerade ? 0.64f : 0.29f);
      de.Duel = new DuelState { Stage = to - 1, TransTo = to, TransT = Math.Max(0.001f, 2f - into) };
      foreach (var p in host.Game.Players.Values) { p.Pos = p.Disp = at + new V2(150f, 160f); p.Aim = MathX.Pi; }
      return;
    }
    string last = Environment.GetEnvironmentVariable("DUELLAST");
    if (last != null) {
      float since = float.Parse(last, System.Globalization.CultureInfo.InvariantCulture);
      host.Game.Enemies.Clear(); host.Game.Bullets.Clear(); host.Game.Effects.Clear(); host.Game.Bot = false;
      var sp = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var at = new V2(Game.Width / 2f, Game.Height / 2f);
      var de = (Enemy)sp.Invoke(host.Game, new object[] { EnemyType.Duelist, at, 0f, 0, false });
      de.Spawning = false; de.SpawnT = 0f; de.Disp = de.Pos = at; de.Facing = -0.5f; de.Hp = de.MaxHp * 0.08f;
      de.Duel = new DuelState { Stage = DuelStage.LastStand, Ring = 280f };
      var fl = BindingFlags.NonPublic | BindingFlags.Instance;
      var drawn = (Game)typeof(ArenaApp).GetField("game", fl | BindingFlags.Public).GetValue(app);
      var seen = (System.Collections.Generic.Dictionary<int, float>)typeof(ArenaApp).GetField("duelLastAt", fl).GetValue(app);
      seen[de.Id] = drawn.AnimT - since;
      foreach (var p in host.Game.Players.Values) { p.Pos = p.Disp = at + new V2(150f, 160f); p.Aim = MathX.Pi; }
      return;
    }
    string dec = Environment.GetEnvironmentVariable("DUELDECOY");
    if (dec != null) {
      float since = float.Parse(dec, System.Globalization.CultureInfo.InvariantCulture);
      host.Game.Enemies.Clear(); host.Game.Bullets.Clear(); host.Game.Effects.Clear(); host.Game.Bot = false;
      var sp = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var you = new V2(Game.Width / 2f, Game.Height / 2f + 60f);
      var fl = BindingFlags.NonPublic | BindingFlags.Instance;
      var drawn = (Game)typeof(ArenaApp).GetField("game", fl | BindingFlags.Public).GetValue(app);
      var seen = (System.Collections.Generic.Dictionary<int, float>)typeof(ArenaApp).GetField("decoySeenAt", fl).GetValue(app);
      float out1 = Math.Min(1f, since / 0.8f), ring = 180f;
      V2 home = you + V2.FromAngle(-MathX.Pi / 2f) * ring;
      for (int k = 0; k < 4; k++) {
        V2 slot = you + V2.FromAngle(-MathX.Pi / 2f + MathX.Tau * k / 4f) * ring, at = k == 0 ? home : home + (slot - home) * out1;
        var de = (Enemy)sp.Invoke(host.Game, new object[] { k == 0 ? EnemyType.Duelist : EnemyType.Decoy, at, 0f, 0, false });
        de.Spawning = false; de.SpawnT = 0f; de.Disp = de.Pos = at; de.Facing = (you - at).Angle;
        if (k == 0) { de.Hp = de.MaxHp * 0.6f; de.Duel = new DuelState { Stage = DuelStage.Masquerade, Turn = DuelTurn.Masquerade }; }
        else { de.Spawning = since < 0.4f; seen[de.Id] = drawn.AnimT - since; }
      }
      foreach (var p in host.Game.Players.Values) { p.Pos = p.Disp = you; p.Aim = MathX.Pi / 2f; }  // (facing away: no hits knocking it on into To the Death)
      return;
    }
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
      else ((System.Collections.Generic.Dictionary<int, float>)typeof(ArenaApp).GetField("decoySeenAt", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(app))[e.Id] = -99f;  // long since in
    }
    foreach (var p in gm.Players.Values) { p.Pos = p.Disp = only > 0 ? new V2(Game.Width / 2f + 150f, Game.Height / 2f + 160f) : new V2(Game.Width / 2f, Game.Height / 2f + 380f); p.Aim = MathX.Pi; }
  }
}
