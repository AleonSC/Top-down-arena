using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// BUBBLE=<s since the cast> [BUBFILL=<0-1>] [BUBBURST=<s since the burst>]: Static Bubble, the Healer Elementalist's second
// ultimate (Core/StaticBubble.cs, Game/StaticBubbleLook.cs), cast and played through the real game: you in the middle with a
// teammate beside you, grunts walking in from round you and shots raining in on the bubble. BUBFILL sets how full it is at the
// end (it grows to match); BUBBURST fills it up and shows its burst and Static Rush that many seconds later. Use with mode hud.
static partial class Shot {
  static void StaticBubbleScene() {
    string ts = Environment.GetEnvironmentVariable("BUBBLE");
    if (ts == null) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    float T = float.Parse(ts, inv);
    string fs = Environment.GetEnvironmentVariable("BUBFILL"), bs = Environment.GetEnvironmentVariable("BUBBURST");
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 mid = new V2(700f, 500f), mate = mid + new V2(-22f, 18f);
    bool first = true;
    foreach (var q in gm.Players.Values) if (q != me) { q.Pos = q.Disp = first ? mate : new V2(150f, 860f); q.Hp = q.MaxHp * 0.6f; first = false; }
    me.Kit = Kit.Elementalist; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Slots[Upgrades.UltSlot].Id = AbilityId.StaticBubble;
    me.Pos = me.Disp = mid; me.Aim = 0f;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
    var rnd = new Random(11);
    for (int i = 0; i < 7; i++) {
      float a = i * MathX.Tau / 7f + 0.2f;
      V2 at = mid + V2.FromAngle(a) * (110f + (float)rnd.NextDouble() * 70f);
      var type = i == 4 ? EnemyType.Thug : i % 3 == 0 ? EnemyType.RiotShield : EnemyType.Shambler;
      var e = (Enemy)spawn.Invoke(gm, new object[] { type, at, 0f, 0, false });
      e.SpawnT = 0f; e.Pos = e.Disp = at; e.Hp = e.MaxHp = 1e5f;
    }
    var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(gm);
    sq.Clear(); for (int i = 0; i < 50; i++) sq.Add(EnemyType.Grunt);
    gm.State = MatchState.Fighting;
    var slot = me.Slots[Upgrades.UltSlot]; slot.Charges = 1; slot.Timer = 0f;
    var keep = new HashSet<int>(gm.Enemies.Keys);
    typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null).Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, mid, false });
    Action<float> run = secs => {
      int frames = (int)Math.Round(secs * 60f);
      for (int f = 0; f < frames; f++) {
        foreach (var id in new List<int>(gm.Enemies.Keys)) if (!keep.Contains(id)) gm.Enemies.Remove(id);
        gm.State = MatchState.Fighting; me.Pos = mid;
        first = true;
        foreach (var q in gm.Players.Values) if (q != me && first) { q.Pos = mate; first = false; }
        if (f % 4 == 0) {  // shots raining in on the bubble
          float a = (float)rnd.NextDouble() * MathX.Tau;
          V2 from = mid + V2.FromAngle(a) * 240f;
          shoot.Invoke(gm, new object[] { (byte)10, from, (mid - from).Normalized() * 220f, 0f, 3f, 10f, 0, false, 0f });
        }
        host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = mid + new V2(200f, 0f) }); gm.Update(1f / 60f);
      }
    };
    run(T);
    if (fs != null && me.BubUp) {  // (set, not fed: crowded round it, it would burst before the shot)
      float fill = Math.Min(0.95f, float.Parse(fs, inv));
      me.BubCharge = Game.BubFull * fill;
      me.BubR = Game.BubSize(fill);
    }
    if (bs != null && me.BubUp) {
      me.BubCharge = Game.BubFull;
      run(1f / 60f);
      run(float.Parse(bs, inv));
    }
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    Console.Error.WriteLine("bubble: T {0:0.00} R {1:0} charge {2:0} eaten {3} fed {4} bursts {5} rush {6:0.0}", me.BubT, me.BubR, me.BubCharge, me.BubEaten, me.BubFed, me.BubBursts, me.RushT);
  }
}
