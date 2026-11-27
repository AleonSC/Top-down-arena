using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// EYE=<s since the cast> [EYEMASTERED=1] [EYEGUST=<s since the gust>]: Eye of Calm, the Healer Elementalist's third ultimate
// (Core/EyeOfCalm.cs, Game/EyeOfCalmLook.cs), cast and played through the real game: you in the middle with a hurt teammate in
// the eye, grunts and a thug walking in and drifting round the wall, shots raining in. EYEGUST ends it and shows the final
// gust that many seconds later. Use with mode hud.
static partial class Shot {
  static void EyeOfCalmScene() {
    string ts = Environment.GetEnvironmentVariable("EYE");
    if (ts == null) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    float T = float.Parse(ts, inv);
    string gs = Environment.GetEnvironmentVariable("EYEGUST");
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 mid = new V2(700f, 500f), mate = mid + new V2(-40f, 26f);
    bool first = true;
    foreach (var q in gm.Players.Values) if (q != me) { q.Pos = q.Disp = first ? mate : new V2(150f, 860f); q.Hp = q.MaxHp * 0.45f; first = false; }
    me.Kit = Kit.Elementalist; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Slots[Upgrades.UltSlot].Id = AbilityId.EyeOfCalm;
    if (Environment.GetEnvironmentVariable("EYEMASTERED") == "1") me.MasteryXp[AbilityId.EyeOfCalm] = 1e6f;
    me.Pos = me.Disp = mid; me.Aim = 0f;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
    var rnd = new Random(5);
    for (int i = 0; i < 10; i++) {
      float a = i * MathX.Tau / 10f + 0.3f;
      V2 at = mid + V2.FromAngle(a) * (130f + (float)rnd.NextDouble() * 160f);
      var type = i == 4 ? EnemyType.Thug : i % 3 == 0 ? EnemyType.RiotShield : EnemyType.Grunt;
      var e = (Enemy)spawn.Invoke(gm, new object[] { type, at, 0f, 0, false });
      e.SpawnT = 0f; e.Pos = e.Disp = at; e.MaxHp = 1000f; e.Hp = 1000f * (0.4f + 0.5f * (float)rnd.NextDouble());
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
        foreach (var e in gm.Enemies.Values) e.Hp = Math.Max(e.Hp, 200f);
        gm.State = MatchState.Fighting; me.Pos = mid;
        first = true;
        foreach (var q in gm.Players.Values) if (q != me && first) { q.Pos = mate; first = false; }
        if (f % 5 == 0) {  // shots raining in on the storm
          float a = (float)rnd.NextDouble() * MathX.Tau;
          V2 from = mid + V2.FromAngle(a) * 420f;
          shoot.Invoke(gm, new object[] { (byte)10, from, (mid - from).Normalized() * 220f, 0f, 3f, 10f, 0, false, 0f });
        }
        host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = mid + new V2(200f, 0f) }); gm.Update(1f / 60f);
      }
    };
    run(T);
    if (gs != null && me.EyeUp) {
      me.EyeT = 1f / 120f;
      run(1f / 60f);
      run(float.Parse(gs, inv));
    }
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    Console.Error.WriteLine("eye: T {0:0.00} age {1:0.00} wall {2:0} hits {3} swept {4} healed {5:0} flung {6}", me.EyeT, me.EyeAge, me.EyeWallR, me.EyeHits, me.EyeSwept, me.EyeHealed, me.EyeFlung);
  }
}
