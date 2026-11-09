using System;
using System.Reflection;
using Arena;

// PHALANX=<seconds since the cast>: the Tank Summoner's Bone Phalanx (Core/BonePhalanx.cs, Game/BonePhalanxLook.cs)
// played through the real game, like the mockup's "The wave" (Docs/Mockups/bone-phalanx.html): you aim right, grunts and
// a Juggernaut walk in, snipers fire at you (a red aimed shot every so often), the line marches, shoves, spears and
// blocks. PHALANXCAP=1 casts it mastered. FXOFF=1 shows the plain fallback (Visual effects off).
// Use with mode hud. The cast is at (420, 450) facing right, so crop about (300, 250)-(1000, 650).
static partial class Shot {
  static void PhalanxScene() {
    string ts = Environment.GetEnvironmentVariable("PHALANX");
    if (ts == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Summoner; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Weapon = Defs.Weapon(Role.Tank, Kit.Summoner, 0);
    me.MaxHp = me.Def.Hp * Defs.KitHpMult(Kit.Summoner); me.Hp = me.MaxHp; me.ShieldHp = 0f;
    V2 home = new V2(420f, 450f);
    int ai = 0;
    foreach (var q in gm.Players.Values) if (q != me) { q.Pos = q.Disp = home + new V2(-60f, ai == 0 ? -150f : 150f); ai++; }
    Func<EnemyType, V2, float, Enemy> Mob = (type, at, hp) => {
      var e = (Enemy)spawn.Invoke(gm, new object[] { type, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = hp; e.Pos = e.Disp = at; e.FireT = 99f; return e;
    };
    var walkers = new System.Collections.Generic.List<Enemy>();
    var spots = new[] { new V2(780f, 360f), new V2(830f, 430f), new V2(870f, 500f), new V2(930f, 390f), new V2(760f, 480f), new V2(990f, 460f), new V2(950f, 540f), new V2(1030f, 380f) };
    foreach (var s in spots) walkers.Add(Mob(EnemyType.Grunt, s, 60f));
    walkers.Add(Mob(EnemyType.Juggernaut, new V2(840f, 406f), 420f));
    var guns = new[] { Mob(EnemyType.Sniper, new V2(1120f, 330f), 80f), Mob(EnemyType.Sniper, new V2(1150f, 560f), 80f), Mob(EnemyType.Sniper, new V2(1100f, 450f), 80f) };
    me.Slots[Upgrades.UltSlot].Id = AbilityId.BonePhalanx; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    me.Pos = me.Disp = home; me.Aim = 0f;
    if (Environment.GetEnvironmentVariable("PHALANXCAP") != null) me.MasteryXp[AbilityId.BonePhalanx] = 99999f;
    cast.Invoke(gm, new object[] { 1, Upgrades.UltSlot, home + new V2(300f, 0f), false });
    int frames = (int)Math.Round(float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture) * 60f);
    for (int f = 0; f < frames; f++) {
      float t = f / 60f;
      foreach (var e in walkers) if (e.Hp > 0f && !e.Def.IsBoss) { e.Pos += (home - e.Pos).Normalized() * (e.Def.Heavy ? 30f : 44f) / 60f; e.FireT = 99f; }
      for (int gi = 0; gi < guns.Length; gi++) {
        guns[gi].FireT = 99f;
        for (float s0 = 0.4f + gi * 0.23f; s0 < 9f; s0 += 0.7f)
          if (s0 >= t && s0 < t + 1f / 60f) {
            V2 dv = (home - guns[gi].Pos).Normalized();
            bool red = (int)(s0 / 0.7f) % 3 == 0;
            shoot.Invoke(gm, new object[] { red ? Defs.PierceShot : Defs.SmallShot, guns[gi].Pos + dv * 16f, dv * 260f, 0f, red ? 9f : 5f, red ? 22f : 9f, 0, false, 0f });
          }
      }
      me.Pos = home; me.InvulnT = 1f; me.Aim = 0f;
      gm.Minions.Clear();  // (the staff's golem would stand in the way of the view)
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(300f, 0f) }); gm.Update(1f / 60f);
    }
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    foreach (var z in gm.Zones.Values) if (z.Kind == ZoneKind.Phalanx)
      Console.WriteLine("phalanx: age {0:0.00} marched {1:0} bones {2}", Game.PhalanxAge(z), z.Marched, string.Join(",", Array.ConvertAll(z.Bones, b => b.ToString())));
  }
}
