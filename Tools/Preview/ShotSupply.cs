using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// SUPPLY=<seconds since the cast>: the Healer Soldier's Supply Drop (Core/SupplyDrop.cs, Game/SupplyDropLook.cs) played through
// the real game: you as a Healer Soldier at (640, 560), the flare thrown 0.3 s into the scene at (860, 450) among grunts, the
// other players walking into the zone once it opens. SUPPLYCAP=1 calls it mastered; FXOFF=1 the plain look. Use with mode hud.
static partial class Shot {
  static void SupplyScene() {
    string ss = Environment.GetEnvironmentVariable("SUPPLY");
    if (ss == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Soldier; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Soldier, 0);
    V2 drop = new V2(860f, 450f), mine = new V2(700f, 520f);
    var others = new List<Player>();
    foreach (var q in gm.Players.Values) if (q != me) { others.Add(q); q.Pos = q.Disp = drop + new V2(-260f + others.Count * 120f, 200f); q.Hp = q.MaxHp * 0.55f; }
    var rng = new Random(5);
    var mobs = new List<Enemy>();
    for (int i = 0; i < 12; i++) {
      bool near = i < 4;
      float a = i / 12f * MathX.Tau + (float)rng.NextDouble() * 0.4f, d = near ? 40f + (float)rng.NextDouble() * 40f : 250f + (float)rng.NextDouble() * 200f;
      V2 at = drop + new V2((float)Math.Cos(a) * d * 1.3f, (float)Math.Sin(a) * d * 0.8f);
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 100f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e);
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.SupplyDrop; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    if (Environment.GetEnvironmentVariable("SUPPLYCAP") != null) me.MasteryXp[AbilityId.SupplyDrop] = 99999f;
    me.Hp = me.MaxHp * 0.7f;
    int frames = (int)Math.Round((float.Parse(ss, System.Globalization.CultureInfo.InvariantCulture) + 0.3f) * 60f);
    UnityEngine.Time.deltaTime = 1f / 60f;
    for (int f = 0; f < frames; f++) {
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, drop, false });
      float since = (f - 18) / 60f;
      bool open = since > Game.SupplyCallIn + Game.SupplyOpen;
      for (int k = 0; k < others.Count; k++) {
        V2 want = open ? drop + V2.FromAngle(1.2f + k * 1.9f) * 80f : others[k].Pos;
        others[k].Pos += (want - others[k].Pos) * Math.Min(1f, 1.5f / 60f);
        others[k].InvulnT = 1f;
      }
      V2 mewant = open ? drop + new V2(-70f, 40f) : mine;
      mine += (mewant - mine) * Math.Min(1f, 1.5f / 60f);
      foreach (var e in mobs) if (e.Hp > 0f) { e.FireT = 99f; e.Disp = e.Pos; }
      me.Pos = me.Disp = mine; me.InvulnT = 1f; me.Aim = (drop - mine).Angle; me.AimAt = drop;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = drop }); gm.Update(1f / 60f);
      me.Pos = me.Disp = mine;
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    }
    int packs = 0; foreach (var o in gm.Orbs.Values) if (o.Med) packs++;
    Console.Error.WriteLine("supply: zones {0} packs {1} supplied {2}", gm.Zones.Count, packs, me.SupplyT > 0f);
  }
}
