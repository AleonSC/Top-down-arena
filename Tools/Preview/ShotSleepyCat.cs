using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// CAT=<s since the cast> [CATCAP=1]: Sleepy Cat, the Healer Summoner's fourth ultimate (Core/SleepyCat.cs, Game/SleepyCatLook.cs),
// cast and played through the real game: three knots of grunts (a heavy in one) round you and a teammate; the cursor runs to the
// biggest knot of unmarked enemies and clicks as the cat gets there, as a player would. Past 5 s the cat naps (the teammate stands
// in its purr). CATCAP=1 casts it mastered (five meows, a wider reach).
static partial class Shot {
  static void SleepyCatScene() {
    string ts = Environment.GetEnvironmentVariable("CAT");
    if (ts == null) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    float T = float.Parse(ts, inv);
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 mid = new V2(560f, 560f), mate = mid + new V2(150f, -70f);
    foreach (var q in gm.Players.Values) if (q != me) { q.Pos = q.Disp = mate; q.Hp = q.MaxHp * 0.5f; }
    me.Kit = Kit.Summoner; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Slots[Upgrades.UltSlot].Id = AbilityId.SleepyCat;
    me.Pos = me.Disp = mid; me.Aim = 0f;
    foreach (var q in gm.Players.Values) q.DevGod = true;  // (no going down mid-shot)
    if (Environment.GetEnvironmentVariable("CATCAP") == "1") me.MasteryXp[AbilityId.SleepyCat] = 99999f;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var rnd = new Random(5);
    V2[] knots = { mid + new V2(330f, -150f), mid + new V2(-260f, -190f), mid + new V2(120f, 210f) };
    for (int i = 0; i < 15; i++) {
      V2 at = knots[i % 3] + new V2((float)(rnd.NextDouble() - 0.5) * 120f, (float)(rnd.NextDouble() - 0.5) * 100f);
      var e = (Enemy)spawn.Invoke(gm, new object[] { i == 4 ? EnemyType.Thug : EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Pos = e.Disp = at; e.Hp = e.MaxHp = 1e5f;
    }
    var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(gm);
    sq.Clear(); for (int i = 0; i < 50; i++) sq.Add(EnemyType.Grunt);
    gm.State = MatchState.Fighting;
    var slot = me.Slots[Upgrades.UltSlot]; slot.Charges = 1; slot.Timer = 0f;
    var keep = new HashSet<int>(gm.Enemies.Keys);
    V2 cursor = mid + new V2(60f, 0f);
    typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null).Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, cursor, false });
    int frames = (int)Math.Round(T * 60f);
    bool clicked = false;
    for (int f = 0; f < frames; f++) {
      foreach (var id in new List<int>(gm.Enemies.Keys)) if (!keep.Contains(id)) gm.Enemies.Remove(id);
      gm.State = MatchState.Fighting; me.Pos = mid;
      foreach (var q in gm.Players.Values) if (q != me) q.Pos = mate;
      Zone cat = null; foreach (var z in gm.Zones.Values) if (z.Kind == ZoneKind.SleepyCat) cat = z;
      bool fire = false;
      if (cat != null && !cat.Quiet) {
        Enemy best = null; float bestN = -1e9f;
        foreach (var e in gm.Enemies.Values) {
          if (e.CatMark == cat.Id) continue;
          float n = 0f; foreach (var o in gm.Enemies.Values) if (o.CatMark != cat.Id && o.Pos.DistanceTo(e.Pos) < cat.Radius * 0.9f) n++;
          n -= e.Pos.DistanceTo(cat.Pos) / 400f;
          if (n > bestN) { bestN = n; best = e; }
        }
        if (best != null) {
          cursor = cursor + (best.Pos - cursor) * 0.08f;
          if (best.Pos.DistanceTo(cat.Pos) < 30f && !clicked) fire = true;
        }
      }
      clicked = fire;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cursor, Fire = fire }); gm.Update(1f / 60f);
    }
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    foreach (var e in gm.Enemies.Values) { e.Disp = e.Pos; e.CatNap = CatByte(gm, e); }
    Zone cz = null; foreach (var z in gm.Zones.Values) if (z.Kind == ZoneKind.SleepyCat) cz = z;
    int marked = 0, asleep = 0; foreach (var e in gm.Enemies.Values) { if (Game.CatMarked(e)) marked++; if (Game.CatAsleep(e)) asleep++; }
    Console.Error.WriteLine("cat: zone {0} napping {1} age {2:0.00} meows left {3} marked {4} asleep {5}", cz != null, cz != null && cz.Quiet, cz != null ? Game.ScAge(cz) : -1f, cz != null ? cz.BoltsLeft : -1, marked, asleep);
  }

  static byte CatByte(Game gm, Enemy e) => (byte)typeof(Game).GetMethod("CatNapByte", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { e });
}
