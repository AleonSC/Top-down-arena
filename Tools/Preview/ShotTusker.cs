using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;
using UnityEngine;

// TUSKER=<seconds since the cast> [TUSKERCAP=1]: the Tank Summoner's Tidal Tusker (Core/TidalTusker.cs, Game/TidalTuskerLook.cs)
// played through the real game: you walk slowly right from (520, 470) with a ring of grunts and a Heavy closing in, the cast
// 0.3 s into the scene (TUSKERCAP=1 mastered: two streams). It rises to 0.8 s, follows and sprays to 12.8 s, then stomps (lands
// at 13.2 s) and fades by 13.5 s (mastered: 16 s longer). Try 0.4, 2.5, 6, 13.0, 13.25.
// Use with `ZOOM=1.6 CAMAT=640,450 shot.sh x hud 1 6 1`.

static partial class Shot {
  static void TuskerScene() {
    string ts = Environment.GetEnvironmentVariable("TUSKER");
    if (ts == null) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    bool capped = Environment.GetEnvironmentVariable("TUSKERCAP") == "1";
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    typeof(ArenaApp).GetField("juiceGame", flags).SetValue(app, gm);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Summoner; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Weapon = Defs.Weapon(Role.Tank, Kit.Summoner, 0);
    V2 home = new V2(520f, 470f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    EnemyType heavy = EnemyType.Grunt;
    foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) { if ((int)et >= Defs.Enemies.Length) continue; var d = Defs.Get(et); if (d != null && d.Heavy && !d.IsBoss) { heavy = et; break; } }
    var rng = new System.Random(5);
    var mobs = new List<Enemy>();
    for (int tries = 0; mobs.Count < 11 && tries < 3000; tries++) {
      float a = (float)rng.NextDouble() * MathX.Tau, r = 170f + (float)rng.NextDouble() * 220f;
      V2 at = home + new V2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.75f);
      if (mobs.Exists(o => o.Pos.CloserThan(at, 50f))) continue;
      var e = (Enemy)spawn.Invoke(gm, new object[] { mobs.Count == 4 ? heavy : EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 1e5f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e);
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.TidalTusker; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    int frames = (int)Math.Round((float.Parse(ts, inv) + 0.3f) * 60f);
    Time.unscaledDeltaTime = 1f / 60f;
    V2 aimAt = home + new V2(300f, -40f);
    for (int f = 0; f < frames; f++) {
      V2 at = home + new V2(Mathf.Min(f, 300) * 0.5f, 0f);  // a slow walk right (75 px a second), then stand
      me.Aim = (aimAt - at).Angle; me.AimAt = aimAt;
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, aimAt, capped });
      foreach (var e in mobs) { e.FireT = 99f; e.Hp = e.MaxHp; }
      me.Pos = me.Disp = at; me.Hp = me.MaxHp;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      Time.time = f / 60f;
      if (f > 18 && f >= frames - 60) app.GetType().GetMethod("TickTuskerViews", flags).Invoke(app, new object[] { 1f / 60f });
    }
    Zone tz = null; foreach (var z in gm.Zones.Values) if (z.Kind == ZoneKind.Tusker) tz = z;
    Console.Error.WriteLine("tusker: age {0:0.00} pos {1} hp {2:0.00} hose {3} {4}", tz != null ? Game.TkAge(tz) : -1f, tz != null ? tz.Pos.ToString() : "-", tz != null ? tz.GuardFrac : 0f, tz != null ? tz.Side : 0, tz != null ? tz.Rider : 0);
  }
}
