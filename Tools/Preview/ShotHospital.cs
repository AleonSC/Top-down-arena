using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// HOSP=pick|<seconds since the cast>: the Healer Soldier's Field Hospital (Core/FieldHospital.cs, Game/FieldHospitalLook.cs) in a Grid
// run's core (use with EXPANDMAP=0 GRIDCORE=1, mode hud): you as a Healer Soldier near the core's top wall, a mystery door on that wall
// (its spot shows red on the pick). pick: the pick waiting for a click, the cursor on the door above you. <s>: the cell opened that
// long ago, an ally inside, enemies outside walking at the gate (HOSP=58 shows the eject countdown). HOSPTENT=1: outside a Grid run,
// the tent (use without EXPANDMAP). Camera: CAMAT=x,y ZOOM=z (the hospital opens at about 1100,-700).
static partial class Shot {
  static void HospitalScene() {
    string hs = Environment.GetEnvironmentVariable("HOSP");
    if (hs == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear();
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Soldier; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Soldier, 0);
    var slot = me.Slots[Upgrades.UltSlot];
    slot.Id = AbilityId.FieldHospital; slot.Charges = 1; slot.Timer = 0f;
    me.Slots[Upgrades.UltSlot] = slot;
    bool grid = gm.HospOnGrid;
    V2 home = grid ? new V2(1100f, -250f) : new V2(800f, 470f);
    me.Pos = me.Disp = home;
    var ally = (Player)null;
    foreach (var q in gm.Players.Values) if (q != me) { ally = q; q.Pos = q.Disp = home + new V2(-160f, 80f); q.Hp = q.MaxHp * 0.4f; }
    if (grid && hs == "pick" && gm.MysteryDoors.Count == 0)  // a mystery door on the top wall, two spots along (the pick only: it has no plan behind it)
      gm.MysteryDoors.Add(new MysteryDoor { X = 500, Y = -400, Vertical = false, Price = 9 });
    V2 door = new V2(1100f, -400f);
    if (hs == "pick") {
      gm.HospPickSlot = Upgrades.UltSlot;
      me.AimAt = door + new V2(20f, -120f);
      me.Aim = (me.AimAt - me.Pos).Angle;
      Console.Error.WriteLine("hospital pick: {0} spots, {1} free", gm.HospSpots(me).Count, gm.HospSpots(me).FindAll(s => s.Free).Count);
      return;
    }
    float secs = float.Parse(hs, System.Globalization.CultureInfo.InvariantCulture);
    gm.State = MatchState.Fighting;
    cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, grid ? door : home + new V2(0f, -160f), false });
    Zone z = null;
    foreach (var zz in gm.Zones.Values) if (zz.Kind == ZoneKind.FieldHospital) z = zz;
    if (z == null) { Console.Error.WriteLine("hospital: no zone"); return; }
    var mobs = new List<Enemy>();
    var rng = new Random(5);
    for (int i = 0; i < 5; i++) {
      V2 at = grid ? new V2(900f + i * 90f, 120f + (float)rng.NextDouble() * 80f) : z.Pos + V2.FromAngle(i * 1.3f) * 420f;
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 70f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e);
    }
    // jump the clock: most of the time passes at once, the last second is played
    float skip = Math.Max(0f, secs - 1f);
    bool skipped = false;
    V2 inside = z.Pos + new V2(-90f, 40f), inside2 = z.Pos + new V2(110f, 60f);
    for (int f = 0; f < 60 + (secs < 1f ? -60 + (int)(secs * 60f) : 0); f++) {
      if (f == 45 && !skipped) { z.T -= skip; skipped = true; }  // (after the orbs have popped out)
      foreach (var e in mobs) if (e.Hp > 0f) { e.FireT = 99f; e.Pos += (z.Pos - e.Pos).Normalized() * 80f / 60f; e.Disp = e.Pos; }
      gm.State = MatchState.Fighting;  // (mid-wave: in the break the countdown would start at once)
      me.Pos = me.Disp = inside; me.InvulnT = 1f; me.Hp = me.MaxHp;  // (full health: the orbs stay on the floor to be seen)
      if (ally != null) { ally.Pos = ally.Disp = inside2; ally.Hp = ally.MaxHp; }
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = z.Pos }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      foreach (var o in gm.Orbs.Values) o.Disp = o.Pos;
    }
    if (grid) foreach (var e in mobs) { e.Pos = e.Disp = door + new V2((e.Id % 5 - 2) * 40f, 40f + (e.Id % 3) * 26f); }
    Console.Error.WriteLine("hospital: t {0:0.0} rooms {1} orbs {2} used {3}", z.T, gm.Expanded.Count, gm.Orbs.Count, z.Side);
  }
}
