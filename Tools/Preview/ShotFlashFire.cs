using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// FLASHFIRE=<seconds since the cast>: the DPS Summoner's Flash Fire (Core/FlashFire.cs, Game/FlashFireLook.cs) played
// through the real game (FLASHCAP=1 mastered): you at (800, 470) with grunts and a Heavy standing round you, some inside the
// 160 px circle and some just outside, the cast 0.3 s into the scene. The kindle runs 0-0.3 s, the ring rolls 0.3-0.55 s,
// the pyre stands to 3.55 s (mastered 5.55 s), then sinks over 0.5 s. Try 0.2, 0.42, 0.7, 2.0, 3.8. Use with
// `ZOOM=2 shot.sh x hud 1 6 1` (6 s in, so the wave is fighting).
static partial class Shot {
  static void FlashFireScene() {
    string ts = Environment.GetEnvironmentVariable("FLASHFIRE");
    if (ts == null) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
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
    me.Kit = Kit.Summoner; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Summoner, 0);
    V2 home = new V2(800f, 470f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    EnemyType heavy = EnemyType.Grunt;
    foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) { if ((int)et >= Defs.Enemies.Length) continue; var d = Defs.Get(et); if (d != null && d.Heavy && !d.IsBoss) { heavy = et; break; } }
    var rng = new Random(7);
    var mobs = new List<Enemy>();
    var spots = new Dictionary<Enemy, V2>();
    for (int i = 0, tries = 0; mobs.Count < 12 && tries < 2000; tries++) {
      float a = (float)rng.NextDouble() * MathX.Tau, d = i < 8 ? 50f + (float)rng.NextDouble() * 95f : 200f + (float)rng.NextDouble() * 45f;
      V2 at = home + V2.FromAngle(a) * d;
      if (mobs.Exists(o => o.Pos.CloserThan(at, 42f))) continue;
      var e = (Enemy)spawn.Invoke(gm, new object[] { i == 3 ? heavy : EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = i == 3 ? 400f : 200f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e); spots[e] = at; i++;
    }
    me.Slots[0].Id = AbilityId.FlashFire; me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f;
    if (Environment.GetEnvironmentVariable("FLASHCAP") != null) me.MasteryXp[AbilityId.FlashFire] = 99999f;
    V2 aimAt = home + new V2(100f, -30f);
    me.Aim = (aimAt - home).Angle;
    int frames = (int)Math.Round((float.Parse(ts, inv) + 0.3f) * 60f);
    for (int f = 0; f < frames; f++) {
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, 0, aimAt, false });
      foreach (var e in mobs) { e.FireT = 99f; if (f < 18) e.Pos = e.Disp = spots[e]; }
      me.Pos = me.Disp = home;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    }
    Console.Error.WriteLine("flashfire: enemies {0} effects {1}", gm.Enemies.Count, gm.Effects.Count);
  }
}
