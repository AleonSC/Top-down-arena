using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// SPIRIT=<s since the cast>: Spirit Avatar, the Tank Ninja's third ultimate (Core/SpiritAvatar.cs, Game/SpiritAvatarLook.cs),
// cast and played through the real game with Fire held: you (NINJA=0) in the middle, nine enemies coming at you (a Thunderhead among
// them, heavy: half the push) and a Sniper's shots swallowed. The rise runs to 0.8 s; the first Moon Cleave winds up 0.8-1.7 s and sweeps to
// 1.94 s (try 0.5, 1.4, 1.85, 2.6); it bursts at 12.8 s (13.1). Use with mode hud.
static partial class Shot {
  static void SpiritAvatarScene() {
    string ts = Environment.GetEnvironmentVariable("SPIRIT");
    if (ts == null) return;
    float T = float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture);
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 home = new V2(800f, 470f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    me.Kit = Kit.Ninja; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Slots[Upgrades.UltSlot].Id = AbilityId.SpiritAvatar;
    me.Pos = me.Disp = home; me.Aim = -MathX.Pi / 2f;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var rnd = new Random(5);
    for (int i = 0; i < 9; i++) {
      float ang = -MathX.Pi / 2f - 1.5f + i * 0.37f + (float)rnd.NextDouble() * 0.15f;
      V2 at = home + V2.FromAngle(ang) * (110f + (float)rnd.NextDouble() * 90f);
      var type = i == 4 ? EnemyType.Thunderhead : i == 7 ? EnemyType.Sniper : EnemyType.Grunt;
      var e = (Enemy)spawn.Invoke(gm, new object[] { type, at, 0f, 0, false });
      e.SpawnT = 0f; e.Pos = e.Disp = at; e.Hp = e.MaxHp = 1e5f;
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
      gm.State = MatchState.Fighting; me.Pos = home; me.Hp = me.MaxHp;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(0f, -100f), Fire = true }); gm.Update(1f / 60f);
    }
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    Console.Error.WriteLine("spirit: T {0:0.00} age {1:0.00} wind {2:0.00} cleaves {3} swallowed {4}", me.SpT, me.SpAge, me.SpWind, me.SpCleaves, me.SpSwallowed);
  }
}
