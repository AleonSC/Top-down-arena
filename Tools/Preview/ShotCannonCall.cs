using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// CANNON=<seconds since the cast>: the DPS Knight's Cannon Call (Core/CannonCall.cs, Game/CannonCallLook.cs) played through the
// real game (CANNONCAP=1 mastered): you at (560, 470), a pack of grunts and a Heavy round the mark 320 px ahead, the cast 0.3 s
// into the scene. The flare flies 0.08-0.3 s, the mark counts down, the ball drops 0.35-1.25 s, slams at 1.25 s and the crater
// smokes to 4.25 s. Try 0.2, 0.9, 1.15, 1.3, 1.5, 2.5. Use with `shot.sh x hud 1 6 1` (6 s in, so the wave is fighting).
static partial class Shot {
  static void CannonCallScene() {
    string ts = Environment.GetEnvironmentVariable("CANNON");
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
    me.Kit = Kit.Knight; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Knight, 0);
    V2 home = new V2(560f, 470f), mark = home + new V2(320f, -10f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    EnemyType heavy = EnemyType.Grunt;
    foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) { if ((int)et >= Defs.Enemies.Length) continue; var d = Defs.Get(et); if (d != null && d.Heavy && !d.IsBoss) { heavy = et; break; } }
    var spots = new[] { new V2(-16f, -36f), new V2(30f, 26f), new V2(-40f, 30f), new V2(52f, -30f), new V2(10f, 0f), new V2(95f, 50f), new V2(-80f, -60f), new V2(130f, -60f), new V2(-130f, 90f), new V2(190f, 20f) };
    var mobs = new List<Enemy>();
    var at0 = new Dictionary<Enemy, V2>();
    for (int i = 0; i < spots.Length; i++) {
      V2 at = mark + spots[i];
      var e = (Enemy)spawn.Invoke(gm, new object[] { i == 5 ? heavy : EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = i == 5 ? 400f : i < 5 ? 60f : 120f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e); at0[e] = at;
    }
    me.Slots[0].Id = AbilityId.CannonCall; me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f;
    if (Environment.GetEnvironmentVariable("CANNONCAP") != null) me.MasteryXp[AbilityId.CannonCall] = 99999f;
    me.Aim = (mark - home).Angle;
    float secs = float.Parse(ts, inv);
    int frames = (int)Math.Round((secs + 0.3f) * 60f), castF = 18, landF = castF + (int)Math.Round(Game.CcLandT * 60f);
    for (int f = 0; f < frames; f++) {
      if (f == castF) cast.Invoke(gm, new object[] { me.Id, 0, mark, false });
      foreach (var e in mobs) { e.FireT = 99f; if (f < landF && at0.ContainsKey(e)) e.Pos = e.Disp = at0[e]; }
      me.Pos = me.Disp = home;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = mark }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    }
    Console.Error.WriteLine("cannon: enemies {0} effects {1}", gm.Enemies.Count, gm.Effects.Count);
  }
}
