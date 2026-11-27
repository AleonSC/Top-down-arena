using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// SACRITUAL=<s since the cast> [SACKILLS=0]: Sacrificial Ritual, the Tank Hemomancer's fourth ultimate (Core/SacrificialRitual.cs,
// Game/SacrificialRitualLook.cs), cast and played through the real game: you in the middle, nine grunts round you. The rite runs to
// 1 s (try 0.3, 0.8); then the spirit drifts right with Fire held, its scythe killing grunts (2.5, 4); it returns at 11 s (11.3).
// SACKILLS=0: the grunts can't die (the return at 1 HP). Use with mode hud.
static partial class Shot {
  static void SacrificialRitualScene() {
    string ts = Environment.GetEnvironmentVariable("SACRITUAL");
    if (ts == null) return;
    float T = float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture);
    bool kills = Environment.GetEnvironmentVariable("SACKILLS") != "0";
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 home = new V2(700f, 470f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    me.Kit = Kit.Hemomancer; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Slots[Upgrades.UltSlot].Id = AbilityId.SacrificialRitual;
    me.Pos = me.Disp = home; me.Aim = 0f;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var rnd = new Random(7);
    for (int i = 0; i < 9; i++) {
      float ang = -1.3f + i * 0.33f + (float)rnd.NextDouble() * 0.12f;
      V2 at = home + V2.FromAngle(ang) * (150f + i * 22f);
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Pos = e.Disp = at; e.Hp = e.MaxHp = kills ? 60f : 1e5f; e.FrozenT = 999f;
    }
    var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(gm);
    sq.Clear(); for (int i = 0; i < 50; i++) sq.Add(EnemyType.Grunt);
    gm.State = MatchState.Fighting;
    var slot = me.Slots[Upgrades.UltSlot]; slot.Charges = 1; slot.Timer = 0f;
    var keep = new HashSet<int>(gm.Enemies.Keys);
    typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null).Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, home, false });
    int frames = (int)Math.Round(T * 60f);
    for (int f = 0; f < frames; f++) {
      foreach (var id in new List<int>(gm.Enemies.Keys)) if (!keep.Contains(id)) gm.Enemies.Remove(id);
      gm.State = MatchState.Fighting;
      Enemy aim = null;
      foreach (var e in gm.Enemies.Values) if (aim == null || e.Pos.DistanceSq(me.Pos) < aim.Pos.DistanceSq(me.Pos)) aim = e;
      V2 to = aim != null ? aim.Pos : me.Pos + new V2(100f, 0f);
      V2 move = to - me.Pos; move = move.Length > 45f ? move.Normalized() : V2.Zero;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = to, Move = move, Fire = true }); gm.Update(1f / 60f);
    }
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    Console.Error.WriteLine("sacritual: T {0:0.00} age {1:0.00} kills {2} claws {3} hp {4:0} returns {5}", me.SacT, me.SacAge, me.SacKills, me.SacClaws, me.Hp, me.SacReturns);
  }
}
