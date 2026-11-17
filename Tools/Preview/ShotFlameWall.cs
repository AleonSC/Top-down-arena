using System;
using System.Reflection;
using Arena;

// FLAMEWALL=<seconds since the cast>: Flame Wall (Game/FlameWallLook.cs) played through the real game, like the mockup
// (Docs/Mockups/flame-wall-ring.html): you at (420, 450), the wall cast at your cursor 250 px to the right, enemy shots
// flying in from the right. FXOFF=1 the plain ring. Use with mode hud.
static partial class Shot {
  static void FlameWallScene() {
    string ts = Environment.GetEnvironmentVariable("FLAMEWALL");
    if (ts == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var addZone = typeof(Game).GetMethod("AddZone", flags);
    var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 home = new V2(420f, 450f), at = home + new V2(250f, 0f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    var rng = new Random(3);
    int frames = (int)Math.Round(float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture) * 60f);
    addZone.Invoke(gm, new object[] { ZoneKind.FlameWall, me, at, 90f, 5f });
    for (int f = 0; f < frames; f++) {
      if (f % 12 == 0) {
        V2 from = new V2(1180f, 250f + (float)rng.NextDouble() * 400f);
        shoot.Invoke(gm, new object[] { (byte)10, from, (home - from).Normalized() * 260f, 0f, 9f, 8f, -1, false, 0f });
      }
      me.Pos = home; me.InvulnT = 1f; me.Aim = 0f;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = at }); gm.Update(1f / 60f);
    }
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
  }
}
