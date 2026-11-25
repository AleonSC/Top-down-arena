using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// ARMORDROP=<seconds since the cast>: the Tank Soldier's Armor Drop (Core/ArmorDrop.cs, Game/ArmorDropLook.cs) played through the
// real game: you as a Tank Soldier at (640, 560), the flare thrown 0.3 s into the scene at (860, 450) among grunts; once the crate
// opens the other players walk to a vest each and you to the third. ARMORCAP=1 calls it mastered (4 vests); ARMORHIT=<armor left>
// sets yours after the pickups (the bar chipped); FXOFF=1 the plain look. Use with mode hud.
static partial class Shot {
  static void ArmorDropScene() {
    string ss = Environment.GetEnvironmentVariable("ARMORDROP");
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
    me.Kit = Kit.Soldier; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Weapon = Defs.Weapon(Role.Tank, Kit.Soldier, 0);
    V2 drop = new V2(860f, 450f), mine = new V2(640f, 560f);
    var others = new List<Player>();
    foreach (var q in gm.Players.Values) if (q != me) { others.Add(q); q.Pos = q.Disp = drop + new V2(-300f + others.Count * 160f, -220f); }
    var rng = new Random(5);
    var mobs = new List<Enemy>();
    for (int i = 0; i < 10; i++) {
      bool near = i < 3;
      float a = i / 10f * MathX.Tau + (float)rng.NextDouble() * 0.4f, d = near ? 30f + (float)rng.NextDouble() * 40f : 260f + (float)rng.NextDouble() * 200f;
      V2 at = drop + new V2((float)Math.Cos(a) * d * 1.3f, (float)Math.Sin(a) * d * 0.8f);
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 100f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e);
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.ArmorDrop; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    if (Environment.GetEnvironmentVariable("ARMORCAP") != null) me.MasteryXp[AbilityId.ArmorDrop] = 99999f;
    int frames = (int)Math.Round((float.Parse(ss, System.Globalization.CultureInfo.InvariantCulture) + 0.3f) * 60f);
    UnityEngine.Time.deltaTime = 1f / 60f;
    var goal = new Dictionary<int, V2>();
    for (int f = 0; f < frames; f++) {
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, drop, false });
      if (goal.Count == 0) {  // once the vests are out, each player heads for one
        var vests = new List<Orb>();
        foreach (var o in gm.Orbs.Values) if (o.Vest) vests.Add(o);
        if (vests.Count >= 3) { goal[me.Id] = vests[2].Pos; for (int k = 0; k < others.Count && k < 2; k++) goal[others[k].Id] = vests[k].Pos; }
      }
      foreach (var q in gm.Players.Values) {
        V2 want = goal.TryGetValue(q.Id, out var gp) ? gp : q == me ? mine : q.Pos;
        if (q == me) { mine += (want - mine).LimitLength(240f / 60f); q.Pos = mine; }
        else q.Pos += (want - q.Pos).LimitLength(240f / 60f);
        q.InvulnT = 1f;
      }
      foreach (var e in mobs) if (e.Hp > 0f) { e.FireT = 99f; e.Disp = e.Pos; }
      me.Aim = (drop - mine).Angle; me.AimAt = drop;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = drop }); gm.Update(1f / 60f);
      me.Pos = me.Disp = mine;
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      foreach (var o in gm.Orbs.Values) if (o.Vest) o.Disp += (o.Pos - o.Disp) * 0.2f;
    }
    string hit = Environment.GetEnvironmentVariable("ARMORHIT");
    if (hit != null && me.Armor > 0f) me.Armor = float.Parse(hit, System.Globalization.CultureInfo.InvariantCulture);
    int vestsOut = 0; foreach (var o in gm.Orbs.Values) if (o.Vest) vestsOut++;
    var arm = new List<string>(); foreach (var q in gm.Players.Values) arm.Add(q.Armor.ToString("0"));
    Console.Error.WriteLine("armordrop: zones {0} vests {1} armor {2}", gm.Zones.Count, vestsOut, string.Join(",", arm));
  }
}
