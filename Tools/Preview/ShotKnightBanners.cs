using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// BANNER=aegis|pilgrim|war [BANNERT=<seconds since the cast, default 3>] [BANNERCAP=1]: the Knight banner ultimates
// (Core/KnightBanners.cs, Game/KnightBannersLook.cs) played through the real game: you as that role's Knight at (600, 520),
// the standard thrown 0.3 s into the scene onto (700, 450), your teammates standing in its ring and a few grunts to the right.
// Aegis: one ally's shield is knocked down to show the bubble wearing; Pilgrim's: the allies start hurt; War: you swing at a
// grunt. Try BANNERT=0.2 (in the air), 0.45 (the slam), 3. Use with `ZOOM=1.3 CAMAT=700,430 shot.sh x hud 1 6 3`.
static partial class Shot {
  static void KnightBannersScene() {
    string kind = Environment.GetEnvironmentVariable("BANNER");
    if (kind == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    Role role = kind == "aegis" ? Role.Tank : kind == "pilgrim" ? Role.Healer : Role.Dps;
    AbilityId id = kind == "aegis" ? AbilityId.AegisStandard : kind == "pilgrim" ? AbilityId.PilgrimsStandard : AbilityId.WarStandard;
    me.Kit = Kit.Knight; me.Role = role; me.Def = Defs.Get(role); me.Weapon = Defs.Weapon(role, Kit.Knight, 0);
    V2 spot = new V2(700f, 450f), mine = new V2(600f, 520f);
    var allies = new List<V2> { new V2(770f, 380f), new V2(810f, 520f) };
    int n = 0;
    foreach (var q in gm.Players.Values) if (q != me) { q.Kit = Kit.Knight; q.Pos = q.Disp = allies[n++ % allies.Count]; }
    var mobs = new List<Enemy>();
    V2[] spots = { new V2(1010f, 360f), new V2(1050f, 470f), new V2(640f + 46f, 520f) };
    for (int i = 0; i < spots.Length; i++) {
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, spots[i], 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 9000f; e.Pos = e.Disp = spots[i]; e.FireT = 99f; mobs.Add(e);
    }
    me.Slots[Upgrades.UltSlot].Id = id; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    if (Environment.GetEnvironmentVariable("BANNERCAP") != null) me.MasteryXp[id] = 99999f;
    float secs = float.Parse(Environment.GetEnvironmentVariable("BANNERT") ?? "3", System.Globalization.CultureInfo.InvariantCulture);
    int frames = (int)Math.Round((secs + 0.3f) * 60f);
    UnityEngine.Time.deltaTime = 1f / 60f;
    foreach (var q in gm.Players.Values) if (role == Role.Healer) q.Hp = q.MaxHp * (q == me ? 0.6f : 0.35f);
    for (int f = 0; f < frames; f++) {
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, spot, false });
      V2 aim = role == Role.Dps ? spots[2] : spot;
      if (role != Role.Healer) foreach (var q in gm.Players.Values) q.Hp = q.MaxHp;
      foreach (var e in mobs) { e.FireT = 99f; e.Disp = e.Pos; }
      me.Pos = mine; me.Aim = (aim - mine).Angle; me.AimAt = aim;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aim, Fire = role == Role.Dps }); gm.Update(1f / 60f);
      me.Pos = me.Disp = mine; me.Aim = (aim - mine).Angle; me.AimAt = aim;
      n = 0;
      foreach (var q in gm.Players.Values) { if (q != me) q.Pos = allies[n++ % allies.Count]; q.Disp = q.Pos; }
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    }
    if (role == Role.Tank) foreach (var q in gm.Players.Values) if (q != me && q.StdShieldMax > 0f) { q.StdShieldHp = q.StdShieldMax * 0.4f; break; }
    Console.Error.WriteLine("banner {0}: zones {1}, my shield {2:0}/{3:0}, fire x{4:0.00}", kind, gm.Zones.Count, me.StdShieldHp, me.StdShieldMax, gm.WarFireMult(me));
  }
}
