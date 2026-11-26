using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// GONG=<s since the cast>: Five Shadow Gong, the Tank Ninja's fourth ultimate (Core/FiveShadowGong.cs, Game/FiveShadowGongLook.cs),
// cast and played through the real game: you (NINJA=0) in the middle aiming right, the gong dropping beside you, 18 enemies round
// the arena for the five shadows to cut. Use with mode hud.
static partial class Shot {
  static void ShadowGongScene() {
    string ts = Environment.GetEnvironmentVariable("GONG");
    if (ts == null) return;
    float T = float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture);
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 at = new V2(640f, 540f);
    foreach (var q in gm.Players.Values) if (q != me) { q.Pos = q.Disp = new V2(150f, 900f); q.Downed = true; }
    me.Kit = Kit.Ninja; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Slots[Upgrades.UltSlot].Id = AbilityId.FiveShadowGong;
    me.Pos = me.Disp = at; me.Aim = 0f;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var rnd = new Random(11);
    var spots = new List<V2>();
    while (spots.Count < 18) {
      V2 p = new V2(160f + (float)rnd.NextDouble() * 1000f, 200f + (float)rnd.NextDouble() * 680f);
      if (p.DistanceTo(at) < 170f || p.DistanceTo(at + new V2(95f, 0f)) < 110f) continue;
      bool close = false; foreach (var o in spots) close |= o.DistanceTo(p) < 70f;
      if (!close) spots.Add(p);
    }
    for (int i = 0; i < spots.Count; i++) {
      var e = (Enemy)spawn.Invoke(gm, new object[] { i % 5 == 2 ? EnemyType.Sniper : i % 7 == 3 ? EnemyType.Brute : EnemyType.Grunt, spots[i], 0f, 0, false });
      e.SpawnT = 0f; e.Pos = e.Disp = spots[i]; e.Hp = e.MaxHp = i % 7 == 3 ? 400f : 100f;
    }
    var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(gm);
    sq.Clear(); for (int i = 0; i < 50; i++) sq.Add(EnemyType.Grunt);
    gm.State = MatchState.Fighting;
    var slot = me.Slots[Upgrades.UltSlot]; slot.Charges = 1; slot.Timer = 0f;
    var keep = new HashSet<int>(gm.Enemies.Keys);
    typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null).Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, at + new V2(300f, 0f), false });
    int frames = (int)Math.Round(T * 60f);
    for (int f = 0; f < frames; f++) {
      foreach (var id in new List<int>(gm.Enemies.Keys)) if (!keep.Contains(id)) gm.Enemies.Remove(id);
      foreach (var e in gm.Enemies.Values) e.FireT = 5f;
      gm.State = MatchState.Fighting; me.Pos = at;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = at + new V2(300f, 0f) }); gm.Update(1f / 60f);
    }
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    Console.Error.WriteLine("gong: T {0:0.00} cuts {1} kills {2} enemies {3} zones {4}", T, me.FsgCuts, me.FsgKills, gm.Enemies.Count, gm.Zones.Count);
  }
}
