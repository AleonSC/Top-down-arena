using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// ARMADILLO=<seconds since the cast>: the Tank Summoner's Iron Armadillo (Core/IronArmadillo.cs, Game/IronArmadilloLook.cs)
// played through the real game: you as a Tank Summoner at (560, 560), grunts and a Heavy walking in, the ultimate cast 0.3 s in,
// then the ball rolling under its own steering toward a cursor that circles the arena's middle. ARMREV=<s> holds Fire for the
// last that many seconds (the rev and its launch arrow); FXOFF=1 the plain look. Use with mode hud.
static partial class Shot {
  static void ArmadilloScene() {
    string ts = Environment.GetEnvironmentVariable("ARMADILLO");
    if (ts == null) return;
    var ci = System.Globalization.CultureInfo.InvariantCulture;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    var look = typeof(ArenaApp).GetMethod("ArmState", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Summoner; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Weapon = Defs.Weapon(Role.Tank, Kit.Summoner, 0);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    EnemyType heavy = EnemyType.Grunt;
    foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) { if ((int)et >= Defs.Enemies.Length) continue; var d = Defs.Get(et); if (d != null && d.Heavy && !d.IsBoss) { heavy = et; break; } }
    var rng = new Random(7);
    var mobs = new List<Enemy>();
    V2 mid = new V2(820f, 450f);
    for (int i = 0; i < 18; i++) {
      float a = i / 18f * MathX.Tau + (float)rng.NextDouble() * 0.3f, d = 180f + (float)rng.NextDouble() * 320f;
      V2 at = mid + new V2((float)Math.Cos(a) * d * 1.3f, (float)Math.Sin(a) * d * 0.7f);
      var e = (Enemy)spawn.Invoke(gm, new object[] { i == 4 ? heavy : EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = i == 4 ? 400f : 200f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e);
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.IronArmadillo; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    float total = float.Parse(ts, ci) + 0.3f;
    float rev = Environment.GetEnvironmentVariable("ARMREV") != null ? float.Parse(Environment.GetEnvironmentVariable("ARMREV"), ci) : 0f;
    int frames = (int)Math.Round(total * 60f);
    me.Pos = me.Disp = new V2(560f, 560f);
    UnityEngine.Time.deltaTime = 1f / 60f;
    for (int f = 0; f < frames; f++) {
      float t = f / 60f;
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, me.Pos, false });
      V2 aimAt = mid + new V2((float)Math.Cos(t * 1.1f) * 420f, (float)Math.Sin(t * 1.1f) * 230f);
      bool fire = rev > 0f && t > total - rev;
      foreach (var e in mobs) if (e.Hp > 0f) { e.FireT = 99f; if (!e.Pos.CloserThan(me.Pos, 90f)) e.Pos += (me.Pos - e.Pos).Normalized() * (e.Def.Heavy ? 25f : 45f) / 60f; }
      me.InvulnT = 1f; me.Aim = (aimAt - me.Pos).Angle; me.AimAt = aimAt; me.Firing = fire;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt, Fire = fire }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      look.Invoke(app, new object[] { me, gm.AnimT });
    }
    Console.Error.WriteLine("armadillo: time {0:0.0} age {1:0.0} speed {2:0} hits {3} bashes {4} ricochets {5} at {6:0},{7:0}", me.ArmT, me.ArmAge, me.ArmSpd, me.ArmHits, me.ArmBashes, me.ArmRicochets, me.Pos.X, me.Pos.Y);
  }
}
