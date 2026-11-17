using System;
using System.Reflection;
using Arena;

// CAUTER=1: Cauterize (Core/Cauterize.cs, Game/CauterizeLook.cs) on four bleeding Brutes in a row, seared 0.03, 0.12,
// 0.25 and 0.45 s before the shot, so one picture shows the look's stages left to right. FXOFF=1 the plain look.
// Use with mode hud and ZOOM=2.4 (you stand just under the row).
static partial class Shot {
  static void CauterizeScene() {
    if (Environment.GetEnvironmentVariable("CAUTER") == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var bleed = typeof(Game).GetMethod("ApplyBleed", flags);
    var hit = typeof(Game).GetMethod("OnBulletHit", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 home = new V2(600f, 530f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 800f);
    var ages = new[] { 0.03f, 0.12f, 0.25f, 0.45f };
    var mobs = new Enemy[ages.Length]; var spots = new V2[ages.Length];
    for (int i = 0; i < mobs.Length; i++) {
      spots[i] = new V2(480f + 80f * i, 440f);
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Brute, spots[i], 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 5000f; e.Pos = e.Disp = spots[i]; e.FireT = 99f; mobs[i] = e;
      for (int k = 0; k < 4; k++) bleed.Invoke(gm, new object[] { e, 8f, me.Id, true });
    }
    int total = 30;
    for (int f = 0; f < total; f++) {
      for (int i = 0; i < mobs.Length; i++) {
        if (f == total - (int)Math.Round(ages[i] * 60f))
          hit.Invoke(gm, new object[] { new Bullet { Owner = me.Id, Kind = Defs.FireballShot, Ignite = Game.FireballBurn, Dmg = 0f, Pos = mobs[i].Pos }, mobs[i] });
        mobs[i].Pos = spots[i]; mobs[i].FireT = 99f;
      }
      me.Pos = home; me.InvulnT = 1f; me.Aim = -MathX.Tau / 4f;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = new V2(600f, 300f) }); gm.Update(1f / 60f);
    }
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    int n = 0; foreach (var fx in gm.Effects) if (fx.Kind == FxKind.Cauterize) n++;
    Console.WriteLine("cauterize: {0} looks", n);
  }
}
