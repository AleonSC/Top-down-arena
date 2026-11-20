using System;
using System.Reflection;
using Arena;

// CHALKFIRE=<s since the ignite> (mode hud, ZOOM=2.4): the Spirit Chalk's Alt played through the real game. A loop sealed round
// three held grunts, then the ignite (-1: not yet pressed, the "before" shot). Chalk.cs, Game/ChalkLook.cs.
static partial class Shot {
  static void ChalkFireScene() {
    string ts = Environment.GetEnvironmentVariable("CHALKFIRE");
    if (ts == null) return;
    float since = float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture);
    var gm = host.Game; gm.Bot = false;
    me.Weapon = Defs.Weapon(Role.Healer, Kit.Summoner, 2); me.Kit = Kit.Summoner;
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    V2 home = new V2(450f, 450f), ctr = home + new V2(130f, -20f);
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
    var held = new System.Collections.Generic.Dictionary<Enemy, V2>();
    foreach (var at in new[] { ctr + new V2(-62f, -26f), ctr + new V2(-12f, 3f), ctr + new V2(-44f, 24f) })
      held[(Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, at, 0f, 0, false })] = at;
    int total = 175 + (since >= 0f ? (int)Math.Round(since * 60f) : 0);
    for (int i = 0; i < total; i++) {
      me.Pos = me.Disp = home; me.InvulnT = 1f; me.Hp = me.MaxHp;
      foreach (var kv in held) { var e = kv.Key; e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; e.FireT = 99f; if (!gm.Ink.Exists(d => d.Pen != 0)) e.Pos = e.Disp = kv.Value; }
      float a = Math.Min(i * 3.2f / 60f, MathX.Tau + 0.6f);
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { Fire = i < 150, WeaponAction = i >= 175 && i < 178 && since >= 0f, AimAt = ctr + V2.FromAngle(a - 1.5f) * 68f }); gm.Update(1f / 60f);
    }
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
  }
}
