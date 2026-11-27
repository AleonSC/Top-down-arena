using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// ORACLE=<s since the cast> [ORACLECAP=1]: Tortoise Oracle, the Healer Summoner's third ultimate (Core/TortoiseOracle.cs,
// Game/TortoiseOracleLook.cs), cast and played through the real game: you and a teammate inside the circle, a third ally outside,
// a crowd of grunts and a heavy round it shooting in. ORACLECAP=1 casts it mastered (the wider circle).
static partial class Shot {
  static void TortoiseOracleScene() {
    string ts = Environment.GetEnvironmentVariable("ORACLE");
    if (ts == null) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    float T = float.Parse(ts, inv);
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 mid = new V2(640f, 540f), spot = mid + new V2(80f, -20f), mate = spot + new V2(110f, 90f), out3 = spot + new V2(420f, -230f);
    bool first = true;
    foreach (var q in gm.Players.Values) if (q != me) { q.Pos = q.Disp = first ? mate : out3; q.Hp = q.MaxHp * 0.5f; first = false; }
    me.Kit = Kit.Summoner; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Slots[Upgrades.UltSlot].Id = AbilityId.TortoiseOracle;
    me.Pos = me.Disp = mid; me.Aim = 0f;
    foreach (var q in gm.Players.Values) q.DevGod = true;  // (no going down mid-shot)
    if (Environment.GetEnvironmentVariable("ORACLECAP") == "1") me.MasteryXp[AbilityId.TortoiseOracle] = 99999f;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var rnd = new Random(7);
    for (int i = 0; i < 14; i++) {
      float a = (float)(rnd.NextDouble() * Math.PI * 2), d = 160f + (float)rnd.NextDouble() * 260f;
      V2 at = spot + new V2((float)Math.Cos(a) * d, (float)Math.Sin(a) * d * 0.8f);
      var type = i == 5 ? EnemyType.Thug : EnemyType.Grunt;
      var e = (Enemy)spawn.Invoke(gm, new object[] { type, at, 0f, 0, false });
      e.SpawnT = 0f; e.Pos = e.Disp = at; e.Hp = e.MaxHp = 1e5f;
    }
    var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(gm);
    sq.Clear(); for (int i = 0; i < 50; i++) sq.Add(EnemyType.Grunt);
    gm.State = MatchState.Fighting;
    var slot = me.Slots[Upgrades.UltSlot]; slot.Charges = 1; slot.Timer = 0f;
    var keep = new HashSet<int>(gm.Enemies.Keys);
    typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null).Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, spot, false });
    int frames = (int)Math.Round(T * 60f);
    for (int f = 0; f < frames; f++) {
      foreach (var id in new List<int>(gm.Enemies.Keys)) if (!keep.Contains(id)) gm.Enemies.Remove(id);
      gm.State = MatchState.Fighting; me.Pos = mid;
      first = true;
      foreach (var q in gm.Players.Values) if (q != me) { q.Pos = first ? mate : out3; first = false; }
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = spot }); gm.Update(1f / 60f);
    }
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    Zone oz = null; foreach (var z in gm.Zones.Values) if (z.Kind == ZoneKind.TortoiseOracle) oz = z;
    Console.Error.WriteLine("oracle: zone {0} age {1:0.00} R {2:0} bullets {3}", oz != null, oz != null ? Game.ToAge(oz) : -1f, oz != null ? oz.Radius : 0f, gm.Bullets.Count);
  }
}
