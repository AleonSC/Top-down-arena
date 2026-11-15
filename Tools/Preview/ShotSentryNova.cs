using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// TURRET=<s since the cast> or NOVA=<s since the cast>: the Turret and Nova abilities (Core/Abilities.cs, Game/SentryLook.cs,
// Game/NovaLook.cs) played through the real game: you at (600, 450) facing right, a loose ring of grunts that stand still,
// the ability cast at 0.5 s into the scene. ABILCAP=1 casts it mastered (two turrets / 48 shots); FXOFF=1 the plain looks.
// TURRETEND=1 (with TURRET): the turret runs out 0.6 s after the cast instead, TURRET=<s> after that (its power-down). Use with mode hud.
static partial class Shot {
  static void SentryNovaScene() {
    string tt = Environment.GetEnvironmentVariable("TURRET"), tn = Environment.GetEnvironmentVariable("NOVA");
    if (tt == null && tn == null) return;
    bool turret = tt != null;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    var look = typeof(ArenaApp).GetMethod("UpdateSentryNovaLook", flags);
    typeof(ArenaApp).GetField("juiceGame", flags).SetValue(app, gm);  // so the first drawn frame doesn't clear what the look saw
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 home = new V2(600f, 450f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    var rng = new Random(5);
    var mobs = new List<Enemy>(); var spots = new List<V2>();
    for (int i = 0; i < 7; i++) {
      float a = -1.2f + i * 0.4f + (float)rng.NextDouble() * 0.2f, d = 190f + (float)rng.NextDouble() * 120f;
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, home + V2.FromAngle(a) * d, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 50000f; e.Pos = e.Disp = home + V2.FromAngle(a) * d; e.FireT = 99f; mobs.Add(e); spots.Add(e.Pos);
    }
    var id = turret ? AbilityId.Turret : AbilityId.Nova;
    me.Slots[0].Id = id; me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f;
    if (Environment.GetEnvironmentVariable("ABILCAP") != null) me.MasteryXp[id] = 99999f;
    float secs = float.Parse(turret ? tt : tn, System.Globalization.CultureInfo.InvariantCulture);
    bool end = turret && Environment.GetEnvironmentVariable("TURRETEND") != null;
    int frames = (int)Math.Round((secs + 0.5f + (end ? 0.6f : 0f)) * 60f);
    V2 aimAt = home + new V2(100f, 0f);
    for (int f = 0; f < frames; f++) {
      if (f == 30) {
        cast.Invoke(gm, new object[] { me.Id, 0, aimAt, false });
        if (end) foreach (var m in gm.Minions.Values) m.T = 0.6f;
      }
      for (int k = 0; k < mobs.Count; k++) { mobs[k].FireT = 99f; mobs[k].Pos = mobs[k].Disp = spots[k]; }
      me.Pos = turret && f >= 30 ? home + new V2(-70f, 60f) : home; me.InvulnT = 1f; me.Aim = 0f;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt }); gm.Update(1f / 60f);
      if (look != null) look.Invoke(app, new object[] { gm.AnimT });
    }
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    foreach (var m in gm.Minions.Values) m.Disp = m.Pos;
  }
}
