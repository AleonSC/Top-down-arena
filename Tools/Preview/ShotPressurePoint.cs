using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// PRESS=<s since the cast>: Pressure Point Press, the Healer Ninja's third ultimate (Core/PressurePointPress.cs,
// Game/PressurePointPressLook.cs), cast and played through the real game: you (NINJA=0) in the middle, 10 enemies round you
// (8 in reach get marked, one far one doesn't) and a hurt teammate for the wisps. Use with mode hud.
static partial class Shot {
  static void PressurePointScene() {
    string ts = Environment.GetEnvironmentVariable("PRESS");
    if (ts == null) return;
    float T = float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture);
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 at = new V2(640f, 540f);
    Player mate = null;
    foreach (var q in gm.Players.Values) if (q != me) {
      if (mate == null) { mate = q; q.Pos = q.Disp = new V2(300f, 820f); q.Downed = false; q.Hp = q.MaxHp * 0.35f; }
      else { q.Pos = q.Disp = new V2(150f, 900f); q.Downed = true; }
    }
    me.Kit = Kit.Ninja; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Slots[Upgrades.UltSlot].Id = AbilityId.PressurePointPress;
    me.Pos = me.Disp = at; me.Aim = 0f; me.Hp = me.MaxHp;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var rnd = new Random(5);
    var spots = new List<V2>();
    for (int i = 0; i < 9; i++) {
      float a = i * MathX.Tau / 9f + (float)(rnd.NextDouble() - 0.5) * 0.5f, r = 130f + (float)rnd.NextDouble() * 200f;
      spots.Add(at + new V2((float)Math.Cos(a) * r, (float)Math.Sin(a) * r * 0.85f));
    }
    spots.Add(new V2(160f, 180f));  // out of reach
    for (int i = 0; i < spots.Count; i++) {
      var e = (Enemy)spawn.Invoke(gm, new object[] { i % 4 == 1 ? EnemyType.Sniper : EnemyType.Grunt, spots[i], 0f, 0, false });
      e.SpawnT = 0f; e.Pos = e.Disp = spots[i]; e.Hp = e.MaxHp = 200f;
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
      gm.State = MatchState.Fighting;
      if (mate != null) mate.Pos = new V2(300f, 820f);
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = me.Pos + new V2(300f, 0f) }); gm.Update(1f / 60f);
    }
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    Console.Error.WriteLine("press: T {0:0.00} presses {1} enemies {2}", T, me.PppPresses, gm.Enemies.Count);
  }
}
