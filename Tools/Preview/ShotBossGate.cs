using System;
using Arena;

// GATE=<fill 0-1>: the boss gate (Core/BossGate.cs, Game/BossGateLook.cs) in the hub in the break after wave 10, the
// arena empty. GATEIN=<n>: that many players standing in it (default 1). GATEAGE=<s>: seconds since it opened (default 5).
// Use with mode hud and 3 players. HUD: the wave box's gate bar; WCMIN=1 for the strip.
static partial class Shot {
  static void BossGateScene() {
    string gs = Environment.GetEnvironmentVariable("GATE");
    if (gs == null) return;
    var ci = System.Globalization.CultureInfo.InvariantCulture;
    var gm = host.Game; gm.Bot = false;
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Props.Clear(); gm.Orbs.Clear(); gm.Minions.Clear();
    gm.Layout = LayoutId.Open; gm.State = MatchState.Intermission; gm.StateTimer = 20f;
    V2 at = new V2(Game.Width / 2f, Game.Height / 2f);
    gm.GateUp = true; gm.GatePos = at; gm.GateFill = float.Parse(gs, ci);
    app.gateAgeOverride = float.Parse(Environment.GetEnvironmentVariable("GATEAGE") ?? "5", ci);
    int inn = int.Parse(Environment.GetEnvironmentVariable("GATEIN") ?? "1");
    int k = 0;
    foreach (var p in gm.Players.Values) {
      p.Pos = p.Disp = k < inn ? at + new V2(-36f + 72f * k, 46f) : at + new V2(-260f + 520f * (k - inn) / Math.Max(1, 2 - inn), 210f);
      p.Aim = (at - p.Pos).Angle; k++;
    }
  }
}
