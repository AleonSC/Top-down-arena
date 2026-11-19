using System;
using System.Reflection;
using Arena;

// REDSHOT=1: a boss's red aimed shots (Game/RedShotLook.cs): a fan of them flying at you from the
// right, with a violet boss shot and two small shots for scale. Use with mode hud (ZOOM=2 for a close look).
static partial class Shot {
  static void RedShotScene() {
    string ts = Environment.GetEnvironmentVariable("REDSHOT");
    if (ts == null) return;
    var gm = host.Game; gm.Bot = false;
    var shoot = typeof(Game).GetMethod("SpawnBullet", BindingFlags.NonPublic | BindingFlags.Instance);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 home = new V2(800f, 450f), from = new V2(1100f, 380f);
    me.Pos = me.Disp = home;
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    V2 aim = (home - from).Normalized();
    for (int f = 0; f < 60; f++) {
      if (f == 0 || f == 22) for (int i = -2; i <= 2; i++) {
        V2 d = V2.FromAngle(aim.Angle + i * 0.15f);
        shoot.Invoke(gm, new object[] { Defs.PierceShot, from, d * 210f, 0f, 8f, 30f, 0, true, 0f });
      }
      if (f == 10) {
        shoot.Invoke(gm, new object[] { Defs.BigShot, from + new V2(0f, 140f), new V2(-1f, -0.3f).Normalized() * 150f, 0f, 8f, 20f, 0, true, 0f });
        shoot.Invoke(gm, new object[] { Defs.SmallShot, from + new V2(0f, -150f), new V2(-1f, 0.4f).Normalized() * 200f, 0f, 8f, 8f, 0, false, 0f });
        shoot.Invoke(gm, new object[] { Defs.SmallShot, from + new V2(0f, -120f), new V2(-1f, 0.3f).Normalized() * 200f, 0f, 8f, 8f, 0, false, 0f });
      }
      me.Pos = home; me.InvulnT = 1f; me.Aim = 0f;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = from }); gm.Update(1f / 60f);
    }
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
  }
}
