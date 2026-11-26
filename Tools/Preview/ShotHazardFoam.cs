using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// FOAM=<seconds since the cast> [FOAMCAP=1] [FOAMFIZZ=1]: the Healer Soldier's Hazard Foam (Core/HazardFoam.cs, Game/HazardFoamLook.cs)
// played through the real game: you as a Healer Soldier at (560, 520), a teammate behind you, the sprayer strapped on 0.3 s into
// the scene, then Fire held while the aim sweeps down a line 220 px in front of you, building a wall of foam. A line of grunts
// stands past it, shooting into the foam. Try 0.2, 1.5, 4, 8. FOAMCAP=1 mastered (a bigger tank). FOAMFIZZ=1 jumps every lump to
// halfway through its fizz (dissolving). Use with `ZOOM=1.3 CAMAT=700,520 shot.sh x hud 1 6 3`.
static partial class Shot {
  static void HazardFoamScene() {
    string ss = Environment.GetEnvironmentVariable("FOAM");
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
    V2 mine = new V2(560f, 520f);
    int n = 0;
    foreach (var q in gm.Players.Values) if (q != me) { q.Pos = q.Disp = new V2(470f, 450f + 140f * n++); }
    var mobs = new List<Enemy>();
    var home = new Dictionary<int, V2>();
    for (int i = 0; i < 6; i++) {
      V2 at = new V2(900f + (i % 2) * 40f, 380f + i * 55f);
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 900f; e.Pos = e.Disp = at; e.FireT = 0.5f + i * 0.4f; mobs.Add(e); home[e.Id] = at;
    }
    me.UltPick = 3; me.Slots[Upgrades.UltSlot].Id = AbilityId.HazardFoam; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    if (Environment.GetEnvironmentVariable("FOAMCAP") != null) me.MasteryXp[AbilityId.HazardFoam] = 99999f;
    float secs = float.Parse(ss, System.Globalization.CultureInfo.InvariantCulture);
    int frames = (int)Math.Round((secs + 0.3f) * 60f);
    UnityEngine.Time.deltaTime = 1f / 60f;
    for (int f = 0; f < frames; f++) {
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, mine, false });
      float since = f / 60f - 0.3f, k = Math.Max(0f, Math.Min(1f, since / 6.5f));
      V2 aim = new V2(780f, 330f + 380f * k);  // sweeping down the line
      bool spraying = Game.FoamOn(me) && since > 0.1f;
      foreach (var q in gm.Players.Values) q.Hp = q.MaxHp;
      foreach (var e in mobs) if (e.Hp > 0f) { e.Pos = home[e.Id]; e.Disp = e.Pos; }  // (held in place)
      me.Pos = mine; me.Aim = (aim - mine).Angle; me.AimAt = aim; me.Firing = spraying;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aim, Fire = spraying }); gm.Update(1f / 60f);
      me.Pos = me.Disp = mine; me.Aim = (aim - mine).Angle; me.AimAt = aim;
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    }
    if (Environment.GetEnvironmentVariable("FOAMFIZZ") != null)
      foreach (var z in gm.Zones.Values) if (z.Kind == ZoneKind.HazardFoam) z.T = Game.FoamFizz * 0.5f;
    int lumps = 0; foreach (var z in gm.Zones.Values) if (z.Kind == ZoneKind.HazardFoam) lumps++;
    Console.Error.WriteLine("hazardfoam: sprayer {0:0.0}s tank {1:0} globs {2} lumps {3}", me.FoamT, me.FoamTank, me.FoamGlobs, lumps);
  }
}
