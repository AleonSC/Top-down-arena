using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// CLONEMEDICS=<s since the cast>: Shadow Clone Medics, the Healer Ninja's fourth ultimate (Core/ShadowCloneMedics.cs,
// Game/ShadowCloneMedicsLook.cs), cast and played through the real game: you (NINJA=0) in the middle, two teammates either
// side, grunts round the edge sending a shot at a player every 0.2 s, so the clones step into them. CMHP=<0-1> sets every
// clone's health. Use with mode hud, ZOOM=1.6; try 0.15 (stepping out of the smoke), 1.5, 9.5 (flickering out).
static partial class Shot {
  static void CloneMedicsScene() {
    string ts = Environment.GetEnvironmentVariable("CLONEMEDICS");
    if (ts == null) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    float T = float.Parse(ts, inv);
    string hp = Environment.GetEnvironmentVariable("CMHP");
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 mid = new V2(700f, 470f);
    V2[] spots = { mid, mid + new V2(-170f, -70f), mid + new V2(170f, 60f) };
    var team = new List<Player> { me };
    foreach (var q in gm.Players.Values) if (q != me) team.Add(q);
    me.Kit = Kit.Ninja; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Slots[Upgrades.UltSlot].Id = AbilityId.ShadowCloneMedics;
    for (int i = 0; i < team.Count; i++) { var q = team[i]; q.Pos = q.Disp = i < spots.Length ? spots[i] : new V2(150f, 860f); q.Hp = q.MaxHp * 0.6f; q.InvulnT = 999f; }
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var shoot = typeof(Game).GetMethod("SpawnBullet", flags, null,
      new[] { typeof(byte), typeof(V2), typeof(V2), typeof(float), typeof(float), typeof(float), typeof(int), typeof(bool), typeof(float) }, null);
    var foes = new List<Enemy>();
    for (int i = 0; i < 7; i++) {
      V2 at = mid + V2.FromAngle(MathX.Tau * i / 7f + 0.3f) * 360f;
      var e = (Enemy)spawn.Invoke(gm, new object[] { i == 2 ? EnemyType.Sniper : EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Pos = e.Disp = at; e.Hp = e.MaxHp = 1e5f; e.FireT = 99f; foes.Add(e);
    }
    var keep = new HashSet<int>(gm.Enemies.Keys);
    var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(gm);
    sq.Clear(); for (int i = 0; i < 50; i++) sq.Add(EnemyType.Grunt);
    gm.State = MatchState.Fighting;
    var slot = me.Slots[Upgrades.UltSlot]; slot.Charges = 1; slot.Timer = 0f;
    typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null).Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, mid, false });
    var rnd = new Random(5);
    int frames = (int)Math.Round(T * 60f);
    for (int f = 0; f < frames; f++) {
      foreach (var id in new List<int>(gm.Enemies.Keys)) if (!keep.Contains(id)) gm.Enemies.Remove(id);
      gm.State = MatchState.Fighting;
      for (int i = 0; i < team.Count && i < spots.Length; i++) team[i].Pos = spots[i] + new V2((float)Math.Sin(f / 60f * 1.3f + i * 2f) * 30f, (float)Math.Cos(f / 60f * 1.1f + i) * 20f);
      foreach (var e in foes) e.FireT = 99f;
      if (f % 12 == 6) {
        var e = foes[rnd.Next(foes.Count)];
        var q = team[rnd.Next(Math.Min(team.Count, spots.Length))];
        V2 dir = (q.Pos - e.Pos).Normalized();
        bool red = rnd.Next(5) == 0;
        shoot.Invoke(gm, new object[] { red ? Defs.PierceShot : Defs.SmallShot, e.Pos + dir * 20f, dir * 320f, 0f, 3f, 8f, 0, false, 0f });
      }
      if (hp != null) foreach (var m in gm.Minions.Values) if (m.Kind == MinionKind.CloneMedic) m.Hp = m.MaxHp * float.Parse(hp, inv);
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = mid + new V2(300f, -40f) }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      foreach (var m in gm.Minions.Values) m.Disp = m.Pos;
    }
    foreach (var m in gm.Minions.Values) if (m.Kind == MinionKind.CloneMedic) m.GuardFrac = m.Hp / Math.Max(1f, m.MaxHp);
    int clones = 0; foreach (var m in gm.Minions.Values) if (m.Kind == MinionKind.CloneMedic) clones++;
    Console.Error.WriteLine("clone medics: clones {0} bullets {1}", clones, gm.Bullets.Count);
  }
}
