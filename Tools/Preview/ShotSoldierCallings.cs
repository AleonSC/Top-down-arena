using System;
using System.Reflection;
using Arena;

// SOLDIERCALL=<a>,<b>,<c>[,<s>]: three of the Soldier's Callings (Core/SoldierCallings.cs, Game/SoldierCallingsLook.cs)
// played through the real game, one per player (CallingId names: you, the ally to the left, the one to the right), for
// <s> seconds (default 1.2). Everyone fires up at the grunts; a second-slot Calling is swapped out; the left ally is hurt (so a
// Lifeline Tether or Nano Sprayer heals them). Mode hud, 3 players; ZOOM=1.6 CAMAT=600,420 frames them.
//   e.g. SOLDIERCALL=SiegeCannon,SlugBreacher,FlakMount  ·  DefibCannon,NanoSprayer,LifelineTether  ·  Railspike,RocketPod,ShoulderMinigun
static partial class Shot {
  static void SoldierCallingsScene() {
    string arg = Environment.GetEnvironmentVariable("SOLDIERCALL");
    if (arg == null) return;
    var parts = arg.Split(',');
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var swap = typeof(Game).GetMethod("DoSwap", flags);
    var enemyShot = typeof(Game).GetMethod("SpawnBullet", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 home = new V2(600f, 450f);
    var who = new System.Collections.Generic.List<Player> { me };
    foreach (var q in gm.Players.Values) if (q != me) who.Add(q);
    V2[] spots = { home, home + new V2(-170f, 30f), home + new V2(170f, 20f) };
    for (int i = 0; i < who.Count && i < 3 && i < parts.Length; i++) {
      var c = (CallingId)Enum.Parse(typeof(CallingId), parts[i]);
      who[i].Kit = Kit.Soldier;  // (a Soldier look for each)
      int card = Array.FindIndex(Upgrades.All, u => u.Calling == c && u.CallingTier == 1);
      gm.GrantUpgrade(who[i].Id, card);
      if (Game.WayOf(c) == Game.CallingWay.SecondSlot) swap.Invoke(gm, new object[] { who[i] });
      who[i].Pos = who[i].Disp = spots[i];
    }
    var mobs = new System.Collections.Generic.List<Enemy>(); var mspots = new System.Collections.Generic.List<V2>();
    V2[] at = { home + new V2(20f, -200f), home + new V2(-190f, -190f), home + new V2(200f, -180f), home + new V2(-60f, -260f), home + new V2(90f, -250f) };
    foreach (var p0 in at) {
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, p0, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 50000f; e.Pos = e.Disp = p0; e.FireT = 99f; mobs.Add(e); mspots.Add(p0);
    }
    float secs = parts.Length > 3 ? float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture) : 1.2f;
    int frames = (int)Math.Round(secs * 60f);
    for (int f = 0; f < frames; f++) {
      for (int k = 0; k < mobs.Count; k++) { mobs[k].FireT = 99f; mobs[k].Pos = mobs[k].Disp = mspots[k]; mobs[k].KnockLeft = V2.Zero; }
      if (f % 20 == 5)  // enemy bullets drifting in (for the Flak Mount to shoot down)
        enemyShot.Invoke(gm, new object[] { Defs.SmallShot, home + new V2(150f, -120f), new V2(-60f, 60f), 0f, 3f, 5f, 0, false, 0f });
      for (int i = 0; i < who.Count && i < 3; i++) {
        var q = who[i];
        q.Pos = spots[i]; q.InvulnT = 1f;
        if (i == 1) q.Hp = Math.Min(q.Hp, q.MaxHp * 0.5f);
        if (i > 0) { q.AimAt = mspots[i]; q.Aim = (mspots[i] - q.Pos).Angle; q.Firing = q.Weapon.Id != WeaponId.NanoSprayer || true; }
      }
      if (who.Count > 2 && who[2].Weapon.Id == WeaponId.NanoSprayer) { who[2].Aim = (spots[0] - spots[2]).Angle; }
      if (who.Count > 1 && who[1].Weapon.Id == WeaponId.NanoSprayer) { who[1].Aim = (spots[0] - spots[1]).Angle; }
      bool charge = me.Weapon.Charge;
      if (charge && f == frames - 20) me.BowRelease = 3f;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = mspots[0], Fire = !charge }); gm.Update(1f / 60f);
    }
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    foreach (var m in gm.Minions.Values) m.Disp = m.Pos;
  }
}
