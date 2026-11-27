using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// SPIDERWEB=<seconds since the cast>: the Healer Summoner's Mending Web (Core/MendingWeb.cs, Game/MendingWebLook.cs) played through
// the real game in the opening rooms: the web strung between the two facing walls nearest you (anywhere in the map), a teammate standing in it and
// grunts walking into it (one outside). The spider runs out by ~0.8 s and finishes weaving by ~1.7 s. SPIDERWEBPICK=1 shows the pick
// instead, with the first anchor down and the cursor on the second wall. Use with `ZOOM=2 shot.sh x hud 1 6 2`.
static partial class Shot {
  static void MendingWebScene() {
    string ts = Environment.GetEnvironmentVariable("SPIDERWEB");
    bool picking = Environment.GetEnvironmentVariable("SPIDERWEBPICK") != null;
    if (ts == null && !picking) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    var stepFrom = typeof(Game).GetField("StepFrom", flags);
    typeof(ArenaApp).GetField("juiceGame", flags).SetValue(app, gm);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear();
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    foreach (var q in gm.Players.Values) gm.DevSetGod(q.Id, true);
    for (int i = 0; i < 30; i++) { foreach (var e in gm.Enemies.Values) e.Hp = 0f; Step(host); }  // (the walls rise)
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Summoner; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Summoner, 0);
    // two facing walls near the middle, 200-380 px apart
    V2 c = me.Pos, a = c, b = c;
    float best = float.MaxValue;
    for (float x = gm.ArenaMin.X + 40f; x < gm.ArenaMax.X - 40f; x += 30f)
      for (float y = gm.ArenaMin.Y + 40f; y < gm.ArenaMax.Y - 40f; y += 30f) {
        V2 p0 = new V2(x, y), a0, b0; WebAnchor wa, wb;
        if (!gm.WebAuto(p0, V2.Zero, 380f, out a0, out b0) || a0.DistanceTo(b0) < 200f) continue;
        if (!gm.WebSnap(a0, 6f, out wa) || !gm.WebSnap(b0, 6f, out wb) || gm.WebPair(wa, wb, Game.WebMaxLen) != null) continue;
        float score = p0.DistanceTo(c);
        if (score < best) { best = score; a = a0; b = b0; }
      }
    V2 mid = (a + b) * 0.5f, u = (b - a).Normalized(), n = new V2(-u.Y, u.X);
    V2 home = mid + n * 150f - u * 60f, allyAt = mid - u * 50f + n * 4f;
    Player ally = null;
    foreach (var q in gm.Players.Values) if (q != me) { if (ally == null) ally = q; else q.Pos = q.Disp = gm.ArenaMin + new V2(80f, 80f); }
    var mobs = new List<Enemy>();
    var spots = new Dictionary<Enemy, V2>();
    V2[] at = { mid + u * 45f - n * 6f, mid + u * 95f + n * 10f, mid + u * 10f - n * 70f };
    foreach (var p in at) {
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, p, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 9999f; e.Pos = e.Disp = p; e.FireT = 99f; mobs.Add(e); spots[e] = p;
    }
    me.Slots[0].Id = AbilityId.MendingWeb; me.Slots[0].Charges = 2; me.Slots[0].Timer = 0f;
    me.Aim = (b - home).Angle;
    if (picking) {
      gm.WebPickSlot = 0;
      WebAnchor first; gm.WebSnap(a, 6f, out first); gm.WebFirst = first;
      me.Pos = me.Disp = home; me.AimAt = b - u * 10f;
      if (ally != null) ally.Pos = ally.Disp = allyAt;
      return;
    }
    int frames = (int)Math.Round((float.Parse(ts, inv) + 0.3f) * 60f);
    for (int f = 0; f < frames; f++) {
      if (f == 18) { stepFrom.SetValue(gm, (V2?)a); cast.Invoke(gm, new object[] { me.Id, 0, b, false }); stepFrom.SetValue(gm, null); }
      foreach (var e in mobs) { e.FireT = 99f; e.Pos = e.Disp = spots[e]; e.KnockLeft = V2.Zero; }
      me.Pos = me.Disp = home;
      if (ally != null) { ally.Pos = ally.Disp = allyAt; ally.Hp = Math.Min(ally.Hp, ally.MaxHp * 0.6f); }
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = b }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    }
    Console.Error.WriteLine("web: {0},{1} to {2},{3}, zones {4}", a.X, a.Y, b.X, b.Y, gm.Zones.Count);
  }
}
