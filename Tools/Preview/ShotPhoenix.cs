using System;
using System.Reflection;
using Arena;

// PHOENIX=<s since the cast>: the Phoenix (Core/Phoenix.cs, Game/PhoenixLook.cs) played through the real game: you, a
// Summoner Healer, at (600, 450) with two hurt allies near you and a few grunts round about; the Phoenix is cast at 0.5 s
// and glides after your cursor, between you and the ally up and to the left, pulsing heals every 2 s. Mode hud, 3 players.
static partial class Shot {
  static void PhoenixScene() {
    string ts = Environment.GetEnvironmentVariable("PHOENIX");
    if (ts == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 home = new V2(600f, 450f);
    var allies = new System.Collections.Generic.List<Player>();
    foreach (var q in gm.Players.Values) if (q != me) allies.Add(q);
    V2[] spots = { home + new V2(-130f, -70f), home + new V2(150f, 90f) };
    for (int i = 0; i < allies.Count; i++) { allies[i].Pos = allies[i].Disp = i < 2 ? spots[i] : new V2(150f, 820f); allies[i].Hp = allies[i].MaxHp * (0.35f + 0.2f * i); }
    var mobs = new System.Collections.Generic.List<Enemy>(); var mspots = new System.Collections.Generic.List<V2>();
    var rng = new Random(9);
    for (int i = 0; i < 5; i++) {
      V2 at = home + V2.FromAngle(i * 1.25f + 0.3f) * (300f + (float)rng.NextDouble() * 60f);
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 50000f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e); mspots.Add(at);
    }
    me.Slots[0].Id = AbilityId.Phoenix; me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f;
    float secs = float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture);
    int frames = (int)Math.Round((secs + 0.5f) * 60f);
    for (int f = 0; f < frames; f++) {
      if (f == 30) cast.Invoke(gm, new object[] { me.Id, 0, home + new V2(-60f, -30f), false });
      for (int k = 0; k < mobs.Count; k++) { mobs[k].FireT = 99f; mobs[k].Pos = mobs[k].Disp = mspots[k]; }
      me.Pos = home; me.InvulnT = 1f; me.Aim = 0f;
      foreach (var q in allies) q.InvulnT = 1f;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(-60f, -30f) }); gm.Update(1f / 60f);
    }
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
  }
}
