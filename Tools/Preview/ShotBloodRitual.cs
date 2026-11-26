using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// RITUAL=<seconds since the cast, default 4> [RITUALCAP=1]: the Tank Hemomancer's Blood Ritual (Core/BloodRitual.cs,
// Game/BloodRitualLook.cs) played through the real game: you as a Tank Hemomancer at (520, 470), the sigil cast 0.3 s into the
// scene onto (760, 440), and a ring of tough grunts round it for the imps to claw (they hold their fire). Try RITUAL=0.8 (the
// pentagram half drawn), 1.5 (the portal opening), 2.2 (the first burst), 5 (the swarm), 9.9 (flying home).
// Use with `ZOOM=1.4 CAMAT=700,440 shot.sh x hud 1 6 3`.
static partial class Shot {
  static void BloodRitualScene() {
    string v = Environment.GetEnvironmentVariable("RITUAL");
    if (v == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Hemomancer; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Weapon = Defs.Weapon(Role.Tank, Kit.Hemomancer, 0);
    V2 spot = new V2(760f, 440f), mine = new V2(520f, 470f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(460f, 380f);
    var mobs = new List<Enemy>();
    for (int i = 0; i < 9; i++) {
      V2 at = spot + V2.FromAngle(-1.9f + i * 0.48f) * (i % 2 == 0 ? 170f : 230f);
      var e = (Enemy)spawn.Invoke(gm, new object[] { i == 4 ? EnemyType.Brute : EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 9000f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e);
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.BloodRitual; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    if (Environment.GetEnvironmentVariable("RITUALCAP") != null) me.MasteryXp[AbilityId.BloodRitual] = 99999f;
    float secs = float.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
    int frames = (int)Math.Round((secs + 0.3f) * 60f);
    UnityEngine.Time.deltaTime = 1f / 60f;
    for (int f = 0; f < frames; f++) {
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, spot, false });
      foreach (var e in mobs) { e.FireT = 99f; if (e.Hp < 100f) e.Hp = e.MaxHp; }
      me.Pos = mine; me.Aim = (spot - mine).Angle; me.AimAt = spot;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = spot }); gm.Update(1f / 60f);
      me.Pos = me.Disp = mine;
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      foreach (var m in gm.Minions.Values) m.Disp = m.Pos;
    }
    int imps = 0; foreach (var m in gm.Minions.Values) if (m.Kind == MinionKind.BloodImp) imps++;
    Console.Error.WriteLine("ritual: zones {0}, imps {1}, hp {2:0}/{3:0}", gm.Zones.Count, imps, me.Hp, me.MaxHp);
  }
}
