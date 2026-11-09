using System;
using System.Reflection;
using Arena;

// LAG=pill|chart|guest (the lag chart, LagChartUi.cs): 30 s of made-up numbers fed through LagStats (ping about 45 ms
// with a spike, 20 snapshots a second with one stall, 144 fps with a hitch, wave/boss/storm ticks, three players' pings).
// "pill" and "chart" draw as the host sees it; "guest" draws the chart as a guest. LAGFROZEN=1 adds the FROZEN tag.
// Use with mode hud, 3 players.
static partial class Shot {
  static void LagScene() {
    string val = Environment.GetEnvironmentVariable("LAG");
    if (val == null) return;
    var lag = host.Game.Lag;
    var rng = new Random(5);
    double t0 = 1000.0;
    lag.Reset(t0);
    double nextSnap = t0, nextPing = t0;
    for (double t = t0; t < t0 + 31.0; t += 1.0 / 144.0) {
      double s = t - t0;
      float dt = (float)(1.0 / 144.0) + (s > 22.0 && s < 22.05 ? 0.04f : 0f);
      lag.AddFrame(t, dt + (float)rng.NextDouble() * 0.0012f);
      lag.AddBytes(t, (long)(26000 / 144.0), (long)(1300 / 144.0));
      if (t >= nextSnap) {
        lag.NoteSnapshot(t, 1300 + rng.Next(400));
        nextSnap = t + 0.05 + rng.NextDouble() * 0.008 + (s > 17.9 && s < 18.0 ? 0.27 : 0.0);
      }
      if (t >= nextPing) {
        float spike = s > 17.0 && s < 19.5 ? 140f * (float)Math.Sin((s - 17.0) / 2.5 * Math.PI) : 0f;
        lag.AddPing(t, 42f + (float)rng.NextDouble() * 9f + spike);
        nextPing = t + Game.PingEvery;
      }
      if (Math.Abs(s - 5.0) < 0.004) lag.Mark(t, LagMarkKind.Wave, "W6");
      if (Math.Abs(s - 13.0) < 0.004) lag.Mark(t, LagMarkKind.Boss, "Boss");
      if (Math.Abs(s - 17.6) < 0.004) lag.Mark(t, LagMarkKind.Storm, "512 shots");
    }
    int k = 0;
    foreach (var p in host.Game.Players.Values) lag.PlayerPing[p.Id] = k++ == 0 ? 0f : k == 2 ? 46f : 163f;
    if (Environment.GetEnvironmentVariable("LAGFROZEN") != null) lag.Frozen = true;
    set("lagMode", val == "pill" ? ArenaApp.LagPill : ArenaApp.LagFull);
    set("lagPreviewGuest", val == "guest");
  }
}
