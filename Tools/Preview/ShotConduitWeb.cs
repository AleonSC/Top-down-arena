using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// WEB=<s since the cast> [WEBMASTERED=1]: Conduit Web, the Healer Elementalist's fourth ultimate (Core/ConduitWeb.cs,
// Game/ConduitWebLook.cs), cast and played through the real game: you in the middle aiming right, a hurt teammate in the field
// and one outside it, grunts, riot shields and a thug all round, wired into the circuit. Use with mode hud.
static partial class Shot {
  static void ConduitWebScene() {
    string ts = Environment.GetEnvironmentVariable("WEB");
    if (ts == null) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    float T = float.Parse(ts, inv);
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 mid = new V2(700f, 500f), mate = mid + new V2(-150f, 90f);
    bool first = true;
    foreach (var q in gm.Players.Values) if (q != me) { q.Pos = q.Disp = first ? mate : mid + new V2(-260f, -380f); q.Hp = q.MaxHp * 0.45f; first = false; }
    me.Kit = Kit.Elementalist; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Slots[Upgrades.UltSlot].Id = AbilityId.ConduitWeb;
    if (Environment.GetEnvironmentVariable("WEBMASTERED") == "1") me.MasteryXp[AbilityId.ConduitWeb] = 1e6f;
    me.Pos = me.Disp = mid; me.Aim = 0f;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var rnd = new Random(5);
    for (int i = 0; i < 16; i++) {
      float a = i * MathX.Tau / 16f + 0.3f + (float)rnd.NextDouble() * 0.3f;
      V2 at = mid + V2.FromAngle(a) * (90f + (float)rnd.NextDouble() * (i % 4 == 0 ? 330f : 220f));
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
        host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = mid + new V2(200f, 0f) }); gm.Update(1f / 60f);
      }
    };
    run(T);
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    Console.Error.WriteLine("web: T {0:0.00} reach {1:0} wired {2} most {3} pulses {4} healed {5:0}", me.CwT, Game.CwReach(me), me.CwWired, me.CwMostWired, me.CwPulses, me.CwHealed);
  }
}
