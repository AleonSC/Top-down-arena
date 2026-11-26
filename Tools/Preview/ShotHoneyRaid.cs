using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// HONEY=<s since the cast> [HONEYCAP=1]: Honey Raid, the Healer Summoner's second ultimate (Core/HoneyRaid.cs,
// Game/HoneyRaidLook.cs), cast and played through the real game: you at the left with a teammate, a crowd of grunts and a
// heavy walking in from the right, the raid aimed 420 px ahead of you. HONEYCAP=1 casts it mastered (the wider pool).
static partial class Shot {
  static void HoneyRaidScene() {
    string ts = Environment.GetEnvironmentVariable("HONEY");
    if (ts == null) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    float T = float.Parse(ts, inv);
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 mid = new V2(420f, 560f), mate = new V2(700f, 470f), spot = mid + new V2(420f, -60f);
    bool first = true;
    foreach (var q in gm.Players.Values) if (q != me) { q.Pos = q.Disp = first ? mate : new V2(150f, 860f); q.Hp = q.MaxHp * 0.5f; first = false; }
    me.Kit = Kit.Summoner; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Slots[Upgrades.UltSlot].Id = AbilityId.HoneyRaid;
    me.Pos = me.Disp = mid; me.Aim = 0f;
    foreach (var q in gm.Players.Values) q.DevGod = true;  // (no going down mid-shot)
    if (Environment.GetEnvironmentVariable("HONEYCAP") == "1") me.MasteryXp[AbilityId.HoneyRaid] = 99999f;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var rnd = new Random(7);
    for (int i = 0; i < 16; i++) {
      V2 at = spot + new V2(-120f + (float)rnd.NextDouble() * 420f, -200f + (float)rnd.NextDouble() * 400f);
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
      foreach (var q in gm.Players.Values) if (q != me && first) { q.Pos = mate; first = false; }
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = spot }); gm.Update(1f / 60f);
    }
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    Zone hz = null; foreach (var z in gm.Zones.Values) if (z.Kind == ZoneKind.HoneyRaid) hz = z;
    int drips = 0; foreach (var z in gm.Zones.Values) if (z.Kind == ZoneKind.HoneyPuddle) drips++;
    Console.Error.WriteLine("honey: zone {0} age {1:0.00} R {2:0} drips {3}", hz != null, hz != null ? Game.HrAge(hz) : -1f, hz != null ? hz.Radius : 0f, drips);
  }
}
