using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// TANK=<seconds since the cast>: the Tank Soldier's A Literal Tank (Core/LiteralTank.cs, Game/LiteralTankLook.cs) played through
// the real game: you as a Tank Soldier driving a slow curve from (700, 520), grunts and a Heavy walking in, the tank called 0.3 s
// into the scene, firing at the cursor ahead of you. TANKCAP=1 calls it mastered; FXOFF=1 the plain look. Use with mode hud.
static partial class Shot {
  static void TankScene() {
    string ts = Environment.GetEnvironmentVariable("TANK");
    if (ts == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    var look = typeof(ArenaApp).GetMethod("TankState", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Soldier; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Weapon = Defs.Weapon(Role.Tank, Kit.Soldier, 0);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    EnemyType heavy = EnemyType.Grunt;
    foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) { if ((int)et >= Defs.Enemies.Length) continue; var d = Defs.Get(et); if (d != null && d.Heavy && !d.IsBoss) { heavy = et; break; } }
    var rng = new Random(7);
    var mobs = new List<Enemy>();
    V2 mid = new V2(820f, 450f);
    for (int i = 0; i < 14; i++) {
      float a = i / 14f * MathX.Tau + (float)rng.NextDouble() * 0.3f, d = 260f + (float)rng.NextDouble() * 300f;
      V2 at = mid + new V2((float)Math.Cos(a) * d * 1.3f, (float)Math.Sin(a) * d * 0.7f);
      var e = (Enemy)spawn.Invoke(gm, new object[] { i == 4 ? heavy : EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = i == 4 ? 400f : 60f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e);
    }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.LiteralTank; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    if (Environment.GetEnvironmentVariable("TANKCAP") != null) me.MasteryXp[AbilityId.LiteralTank] = 99999f;
    int frames = (int)Math.Round((float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture) + 0.3f) * 60f);
    V2 pos = new V2(760f, 560f);
    float heading = -0.3f;
    UnityEngine.Time.deltaTime = 1f / 60f;
    for (int f = 0; f < frames; f++) {
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, pos, false });
      bool driving = f > 18 + 36;
      if (driving) { heading += 0.9f / 60f; pos += V2.FromAngle(heading) * (me.Speed / 60f); }
      foreach (var e in mobs) if (e.Hp > 0f) { e.FireT = 99f; if (!e.Pos.CloserThan(pos, 60f)) e.Pos += (pos - e.Pos).Normalized() * (e.Def.Heavy ? 25f : 45f) / 60f; e.Disp = e.Pos; }
      V2 aimAt = pos + V2.FromAngle(heading - 0.9f) * 380f;
      me.Pos = me.Disp = pos; me.InvulnT = 1f; me.BarrierT = 0f; me.ShieldHp = 0f; me.AegisT = 0f; me.Aim = (aimAt - pos).Angle; me.AimAt = aimAt; me.Firing = driving;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt, Fire = driving }); gm.Update(1f / 60f);
      me.Pos = me.Disp = pos;
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      look.Invoke(app, new object[] { me, gm.AnimT });
    }
    Console.Error.WriteLine("tank: time {0:0.0} armour {1:0}/{2:0} shells {3} crushed {4} bullets {5} at {6:0},{7:0}", me.TankT, me.TankArmor, me.TankMax, me.TankShells, me.TankCrushes, gm.Bullets.Count, me.Pos.X, me.Pos.Y);
  }
}
