using System;
using System.Reflection;
using Arena;

// BONEGUARD=<seconds into the scene>: the Bone Guard's Marrow Tether and Ossuary Wall (Core/BoneGuard.cs, Game/BoneGuardLook.cs)
// played through the real game, like concept B of Docs/Mockups/bone-guard-rework.html: darts thread three grunts (0-1 s), the
// third dart in the middle one cages it and reels the other two in (about 1.2 s), then Alt throws an Ossuary Wall at 1.6 s
// that snipers' shots stop on. FXOFF=1 shows the plain fallback. Use with mode hud; the player stands at (420, 450) aiming
// right, so crop about (330, 270)-(930, 630).
static partial class Shot {
  static void BoneGuardScene() {
    string ts = Environment.GetEnvironmentVariable("BONEGUARD");
    if (ts == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
    var alt = typeof(Game).GetMethod("HostWeaponDash", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Summoner; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Weapon = Defs.Weapon(Role.Tank, Kit.Summoner, 1);
    me.MaxHp = me.Def.Hp * Defs.KitHpMult(Kit.Summoner); me.Hp = me.MaxHp; me.ShieldHp = 0f; me.BoneWallCd = 0f;
    foreach (var sl in me.Slots) sl.Id = AbilityId.None;
    foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
    V2 home = new V2(420f, 450f);
    int ai = 0;
    foreach (var q in gm.Players.Values) if (q != me) { q.Pos = q.Disp = home + new V2(-60f, ai == 0 ? -150f : 150f); ai++; }
    Func<EnemyType, V2, float, Enemy> Mob = (type, at, hp) => {
      var e = (Enemy)spawn.Invoke(gm, new object[] { type, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = hp; e.Pos = e.Disp = at; e.FireT = 99f; return e;
    };
    var grunts = new[] { Mob(EnemyType.Grunt, new V2(700f, 360f), 900f), Mob(EnemyType.Grunt, new V2(730f, 450f), 900f), Mob(EnemyType.Grunt, new V2(700f, 540f), 900f) };
    var guns = new[] { Mob(EnemyType.Sniper, new V2(880f, 400f), 80f), Mob(EnemyType.Sniper, new V2(890f, 500f), 80f) };
    int[] order = { 0, 1, 2, 1, 1, 1, 1, 1 };
    me.Pos = me.Disp = home; me.Aim = 0f;
    int frames = (int)Math.Round(float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture) * 60f);
    bool thrown = false;
    var seenB = new System.Collections.Generic.HashSet<int>(); int darts = 0;
    for (int f = 0; f < frames; f++) {
      float t = f / 60f;
      var before = Array.ConvertAll(grunts, e => e.Pos);
      var knocked = Array.ConvertAll(grunts, e => e.KnockLeft.Length > 0f);
      for (int gi = 0; gi < guns.Length; gi++) {
        guns[gi].FireT = 99f;
        for (float s0 = 2.0f + gi * 0.25f; s0 < 9f; s0 += 0.5f)
          if (s0 >= t && s0 < t + 1f / 60f) {
            V2 dv = (home - guns[gi].Pos).Normalized();
            shoot.Invoke(gm, new object[] { Defs.SmallShot, guns[gi].Pos + dv * 16f, dv * 260f, 0f, 5f, 9f, 0, false, 0f });
          }
      }
      int k = Math.Min(order.Length - 1, (int)((t + 0.15f) / 0.3f));
      bool fire = t < 2.1f && grunts[1].CageT <= 0f;
      V2 aim = fire ? grunts[order[k]].Pos : home + new V2(170f, 0f);
      if (!thrown && t >= 2.2f) { thrown = true; alt.Invoke(gm, new object[] { 1, 29, home + new V2(170f, 0f) }); }
      me.Pos = home; me.InvulnT = 1f;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aim, Fire = fire }); gm.Update(1f / 60f);
      for (int gi = 0; gi < grunts.Length; gi++) {  // the grunts walk straight in (no dodging), except while a pull carries them
        var e = grunts[gi]; e.Hp = e.MaxHp; e.FireT = 99f;
        if (knocked[gi] || e.KnockLeft.Length > 0f) continue;
        e.Pos = before[gi] + (e.CageT > 0f ? V2.Zero : (home - before[gi]).Normalized() * 14f / 60f);
      }
      foreach (var b in gm.Bullets.Values) if (b.Owner == me.Id && b.Kind == Defs.BoneShot && seenB.Add(b.Id)) darts++;
    }
    Console.Error.WriteLine("boneguard: darts fired {0}", darts);
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    Console.Error.WriteLine("boneguard: links fx {0}, grunts {1}", gm.Effects.FindAll(x => x.Kind == FxKind.MarrowLink).Count, string.Join(" ", Array.ConvertAll(grunts, e => e.Pos.X.ToString("0") + "," + e.Pos.Y.ToString("0") + "/" + e.BoneDarts + "/" + e.CageT.ToString("0.0"))));
  }
}
