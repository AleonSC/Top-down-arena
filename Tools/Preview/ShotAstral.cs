using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// ASTRAL=<s since the cast>: Astral Projection, the Healer Ninja's second ultimate (Core/AstralProjection.cs, Game/AstralProjectionLook.cs),
// cast and played through the real game with Fire held: you (NINJA=0) left behind on the left, the spirit riding a teammate to the
// right (3 players), leaning toward enemies past them; ASTRALSOLO=1 rides your own body instead. Use with mode hud.
static partial class Shot {
  static void AstralScene() {
    string ts = Environment.GetEnvironmentVariable("ASTRAL");
    if (ts == null) return;
    float T = float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture);
    bool solo = Environment.GetEnvironmentVariable("ASTRALSOLO") == "1";
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 body = new V2(560f, 520f), mate = new V2(820f, 440f), other = new V2(700f, 680f);
    int n = 0;
    foreach (var q in gm.Players.Values) if (q != me) { q.Pos = q.Disp = solo ? new V2(150f, 860f) : n++ == 0 ? mate : other; q.Hp = q.MaxHp * 0.5f; }
    me.Kit = Kit.Ninja; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Slots[Upgrades.UltSlot].Id = AbilityId.AstralProjection;
    me.Pos = me.Disp = body; me.Aim = 0f;
    if (solo) foreach (var q in gm.Players.Values) if (q != me) q.Downed = true;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var rnd = new Random(7);
    for (int i = 0; i < 6; i++) {
      V2 at = (solo ? body : mate) + new V2(260f + (float)rnd.NextDouble() * 80f, -150f + i * 60f);
      var e = (Enemy)spawn.Invoke(gm, new object[] { i == 3 ? EnemyType.Sniper : EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Pos = e.Disp = at; e.Hp = e.MaxHp = 1e5f; e.FrozenT = 999f;
    }
    var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(gm);
    sq.Clear(); for (int i = 0; i < 50; i++) sq.Add(EnemyType.Grunt);
    gm.State = MatchState.Fighting;
    var slot = me.Slots[Upgrades.UltSlot]; slot.Charges = 1; slot.Timer = 0f;
    var keep = new HashSet<int>(gm.Enemies.Keys);
    typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null).Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, body, false });
    V2 aim = (solo ? body : mate) + new V2(300f, -40f);
    int frames = (int)Math.Round(T * 60f);
    for (int f = 0; f < frames; f++) {
      foreach (var id in new List<int>(gm.Enemies.Keys)) if (!keep.Contains(id)) gm.Enemies.Remove(id);
      gm.State = MatchState.Fighting; me.Pos = body;
      n = 0;
      foreach (var q in gm.Players.Values) if (q != me && !solo) q.Pos = n++ == 0 ? mate : other;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aim, Fire = true }); gm.Update(1f / 60f);
    }
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    Console.Error.WriteLine("astral: T {0:0.00} host {1} bolts {2} spirit {3:0},{4:0}", me.ApT, me.ApHost, me.ApBolts, me.ApPos.X, me.ApPos.Y);
  }
}
