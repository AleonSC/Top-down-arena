using System;
using System.Reflection;
using Arena;

// CARRIER=1: three Grunts, the middle one a Core Carrier (gold halo, CoreDrops.cs), you far away. Use with mode hud; crop about (380, 220)-(980, 520).
static partial class Shot {
  static void CarrierScene() {
    if (Environment.GetEnvironmentVariable("CARRIER") == null) return;
    var gm = host.Game; gm.Bot = false;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    foreach (var q in gm.Players.Values) q.Pos = q.Disp = new V2(150f, 900f);
    var spots = new[] { new V2(500f, 360f), new V2(640f, 360f), new V2(780f, 360f) };
    for (int i = 0; i < 3; i++) {
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, spots[i], 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 600f; e.Pos = e.Disp = spots[i]; e.FireT = 99f;
      e.Traits = i == 1 ? (byte)EliteTrait.CoreCarrier : (byte)0;
    }
    for (int f = 0; f < 30; f++) {
      foreach (var e in gm.Enemies.Values) { e.FireT = 99f; e.Speed = 0f; }
      me.Pos = new V2(150f, 900f); me.InvulnT = 1f;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = new V2(600f, 350f) }); gm.Update(1f / 60f);
    }
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
  }
}
