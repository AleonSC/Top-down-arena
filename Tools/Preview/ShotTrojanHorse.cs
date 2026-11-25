using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// HORSE=<seconds since the cast>: the Tank Knight's Trojan Horse (Core/TrojanHorse.cs, Game/TrojanHorseLook.cs) played through the
// real game: you as a Tank Knight at (520, 470) cast it 0.3 s into the scene aiming right, so it rolls toward the arena's right
// edge; your teammates climb in 1 s after the cast and shoot arrows up at the grunts. HORSEALL=1: you climb in too (everyone in:
// the grunts go for the horse, the red pulse); HORSEWEAR=<0-1> wears it to that health; HORSEEND=broke|sally: it comes down
// (broke: worn out; sally: its time runs out) that long before HORSE. Use with mode hud and 3 players.
static partial class Shot {
  static void HorseScene() {
    string hs = Environment.GetEnvironmentVariable("HORSE");
    if (hs == null) return;
    var ci = System.Globalization.CultureInfo.InvariantCulture;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    var ride = typeof(Game).GetMethod("SetRide", flags);
    var end = typeof(Game).GetMethod("EndHorse", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Knight; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Weapon = Defs.Weapon(Role.Tank, Kit.Knight, 0);
    bool all = Environment.GetEnvironmentVariable("HORSEALL") != null;
    string wearS = Environment.GetEnvironmentVariable("HORSEWEAR"), endS = Environment.GetEnvironmentVariable("HORSEEND");
    float at = float.Parse(hs, ci);
    var rng = new Random(11);
    var mobs = new List<Enemy>();
    for (int i = 0; i < 12; i++) {
      V2 p = new V2(380f + (float)rng.NextDouble() * 900f, i % 2 == 0 ? 150f + (float)rng.NextDouble() * 120f : 700f + (float)rng.NextDouble() * 110f);
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, p, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 400f; e.Pos = e.Disp = p; e.FireT = 99f; mobs.Add(e);
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.TrojanHorse; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    V2 home = new V2(520f, 470f);
    var mates = new List<Player>();
    foreach (var q in gm.Players.Values) if (q != me) mates.Add(q);
    for (int i = 0; i < mates.Count; i++) mates[i].Pos = mates[i].Disp = home + new V2(-60f, i == 0 ? -70f : 70f);
    int frames = (int)Math.Round((at + 0.3f) * 60f);
    int endAt = endS != null ? frames - 40 : -1;
    UnityEngine.Time.deltaTime = 1f / 60f;
    Zone horse = null;
    for (int f = 0; f < frames; f++) {
      if (f == 18) {
        me.Aim = 0f; me.AimAt = home + new V2(400f, 0f);
        cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, me.AimAt, false });
        foreach (var z in gm.Zones.Values) if (z.Kind == ZoneKind.TrojanHorse) horse = z;
      }
      if (horse != null && f == 18 + 60)
        foreach (var q in mates) { q.Pos = horse.Pos; ride.Invoke(gm, new object[] { q, true }); }
      if (horse != null && all && f == 18 + 75) { me.Pos = horse.Pos; ride.Invoke(gm, new object[] { me, true }); }
      if (horse != null && wearS != null && f == 18 + 80) { horse.BlockHp = horse.SoakMax * float.Parse(wearS, ci); horse.GuardFrac = horse.BlockHp / horse.SoakMax; }
      if (horse != null && f == endAt) {
        if (endS == "broke") end.Invoke(gm, new object[] { horse, true, false });
        else horse.T = 0.01f;
      }
      bool inside = Game.Riding(me);
      V2 aimAt = inside ? horse.Pos + new V2(-60f, -300f) : home + new V2(300f, -200f);
      if (!inside) { me.Pos = me.Disp = home; }
      me.InvulnT = inside ? 0f : 1f; me.BarrierT = 0f; me.ShieldHp = 0f; me.AegisT = 0f; me.Aim = (aimAt - me.Pos).Angle; me.AimAt = aimAt;
      bool fire = inside;
      me.Firing = fire;
      for (int i = 0; i < mates.Count; i++) {
        var q = mates[i];
        q.InvulnT = 0f; q.Firing = Game.Riding(q);
        q.Aim = (i == 0 ? -1.9f : 1.6f); q.FiringSeen = q.Firing;
      }
      foreach (var e in mobs) if (e.Hp > 0f) {
        e.FireT = 99f;
        if (all && horse != null && horse.T > 0f && !e.Pos.CloserThan(horse.Pos, 70f)) e.Pos += (horse.Pos - e.Pos).Normalized() * 40f / 60f;
        e.Disp = e.Pos;
      }
      host.Poll(); foreach (var c in others) c.Poll();  // (the teammates' games keep their connections)
      gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt, Fire = fire }); gm.Update(1f / 60f);
      if (!Game.Riding(me)) me.Pos = home;
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    }
    int riders = 0; foreach (var q in gm.Players.Values) if (Game.Riding(q)) riders++;
    Console.Error.WriteLine("horse: {0} at {1:0},{2:0} rolled {3:0} parked {4} health {5:0}/{6:0} riders {7} arrows {8} bullets {9}",
      horse != null && horse.T > 0f ? "up" : "down", horse?.Pos.X ?? 0f, horse?.Pos.Y ?? 0f, horse?.Marched ?? 0f, horse?.Quiet ?? false, horse?.BlockHp ?? 0f, horse?.SoakMax ?? 0f, riders, mates.Count > 0 ? mates[0].HorseArrows : 0, gm.Bullets.Count);
  }
}
