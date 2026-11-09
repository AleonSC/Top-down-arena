using System;
using System.Reflection;
using Arena;

// KNIGHTDPS=steel|gauntlet|mercy|bridge KDT=<seconds>: the DPS Knight's abilities replaying the scenes of
// Docs/Mockups/knight-dps-abilities.html through the real game (the host simulation, the casts, the carry), stopped
// KDT seconds in, for comparing frame by frame with the mockup at the same time. The mockup's 720 x 400 stage sits at
// (440, 250) in the arena, so crop (352, 200)-(928, 520) of the 1280 x 720 shot. Use with mode hud.
static partial class Shot {
  static readonly V2 KO = new V2(440f, 250f);
  static V2 KM(float x, float y) => KO + new V2(x, y);

  static void KnightDpsScene() {
    string which = Environment.GetEnvironmentVariable("KNIGHTDPS");
    if (which == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;  // no first-sight name tags
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Knight; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Knight, int.Parse(Environment.GetEnvironmentVariable("KDW") ?? "0"));
    me.MaxHp = me.Def.Hp * Defs.KitHpMult(Kit.Knight); me.Hp = me.MaxHp; me.ShieldHp = 0f;
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(1500f, 820f);
    var pinned = new System.Collections.Generic.Dictionary<Enemy, V2>();
    var moving = new System.Collections.Generic.HashSet<Enemy>();  // (walking in: not held still)
    Action Pin = () => {
      gm.Layout = LayoutId.Open; gm.Props.Clear();
      foreach (var q in gm.Players.Values) { q.AegisT = 0f; q.BarrierT = 0f; q.ShieldHp = 0f; }  // (the preview's own loadout: nothing else stops a shot)
      foreach (var e in new System.Collections.Generic.List<Enemy>(pinned.Keys)) {
        if (e.Hp <= 0f) continue;
        e.FireT = 99f;  // they hold their fire: the scene fires its own bullets
        if (moving.Contains(e) || !e.KnockLeft.IsZero) pinned[e] = e.Pos;  // sliding from a shove, or walking: keep where it got to
        else e.Pos = pinned[e];
      }
    };
    Func<EnemyType, V2, float, Enemy> Mob = (type, at, hp) => {
      var e = (Enemy)spawn.Invoke(gm, new object[] { type, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = hp; e.Pos = e.Disp = at; pinned[e] = at; return e;
    };
    float kdt = float.Parse(Environment.GetEnvironmentVariable("KDT") ?? "1", System.Globalization.CultureInfo.InvariantCulture);
    int frames = (int)Math.Round(kdt * 60f);
    Action<int, V2, int> Press = (slot, at, f) => { };
    if (which == "steel") {
      // The knight sways a little; gunners on both sides; bullets aimed to skim past or miss; the release at 3.6 s.
      V2 k0 = KM(245f, 214f);
      Func<float, V2> kp = t => { float tt = Math.Min(t, 3.6f), w = Math.Min(1f, tt / 0.5f); return k0 + new V2((float)Math.Sin(tt * 0.9f) * 7f * w, (float)Math.Sin(tt * 1.35f) * 13f * w); };
      V2 kr = kp(3.6f);
      float[] fan = new float[12]; for (int i = 0; i < 12; i++) fan[i] = -0.6f + 1.2f * i / 11f;
      var spots = new[] { new { f = 1, d = 300f, gun = true }, new { f = 4, d = 335f, gun = true }, new { f = 5, d = 180f, gun = false }, new { f = 7, d = 285f, gun = true }, new { f = 10, d = 315f, gun = true } };
      var shooters = new System.Collections.Generic.List<V2>();
      foreach (var sp in spots) { V2 at = kr + V2.FromAngle(fan[sp.f]) * sp.d; var e = Mob(sp.gun ? EnemyType.Sniper : EnemyType.Grunt, at, sp.gun ? 80f : 50f); if (!sp.gun) e.Hp = 15f; else shooters.Add(at); }
      shooters.Add(KM(66f, 60f)); shooters.Add(KM(92f, 362f));
      foreach (var at in new[] { KM(66f, 60f), KM(92f, 362f) }) Mob(EnemyType.Sniper, at, 80f);
      var fire = new System.Collections.Generic.List<Tuple<float, V2, V2>>();
      float[] miss = { 19f, -46f, -20f, 58f, 22f, -23f, 72f, -19f, 24f, -52f, -21f, 20f, 44f, -25f, 21f, -64f }; int mi = 0;
      for (int j = 0; j < shooters.Count; j++) {
        float per = j >= 4 ? 1.0f : 0.8f;
        for (float ts = 0.12f + j * 0.17f; ts < 3.35f; ts += per) {
          V2 o = shooters[j], tgt = kp(ts + o.DistanceTo(k0) / 240f), dv = (tgt - o).Normalized(), pp = new V2(-dv.Y, dv.X);
          fire.Add(Tuple.Create(ts, o, ((tgt + pp * miss[mi++ % miss.Length]) - o).Normalized()));
        }
      }
      me.Slots[0].Id = AbilityId.BattleFury; me.Slots[0].Charges = 1;
      me.Pos = me.Disp = k0;
      cast.Invoke(gm, new object[] { 1, 0, k0 + new V2(200f, 0f), false });
      for (int f = 0; f < frames; f++) {
        float t = f / 60f;
        foreach (var fr in fire) if (fr.Item1 >= t && fr.Item1 < t + 1f / 60f) shoot.Invoke(gm, new object[] { Defs.SmallShot, fr.Item2 + fr.Item3 * 15f, fr.Item3 * 240f, 0f, 5f, 8f, 0, false, 0f });
        if (Math.Abs(t - 3.6f) < 0.5f / 60f) { me.Slots[0].Charges = 0; cast.Invoke(gm, new object[] { 1, 0, kr + new V2(200f, 0f), false }); }
        me.Pos = kp(t + 1f / 60f); me.InvulnT = 1f; me.Aim = 0f;
        Pin(); host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = me.Pos + new V2(200f, 0f) }); gm.Update(1f / 60f);
      }
    } else if (which == "gauntlet") {
      V2 k0 = KM(95f, 252f), t1 = KM(470f, 148f), t2 = KM(645f, 318f);
      var tg1 = Mob(EnemyType.Sniper, t1, 96f);
      var tg2 = Mob(EnemyType.Sniper, t2, 96f);
      V2 d1 = (t1 - k0).Normalized(), p1 = new V2(-d1.Y, d1.X);
      Mob(EnemyType.Grunt, k0 + d1 * 150f + p1 * 10f, 60f); Mob(EnemyType.Brute, k0 + d1 * 212f + p1 * -13f, 60f); Mob(EnemyType.Grunt, k0 + d1 * 268f + p1 * 7f, 60f);
      V2 a1 = t1 - d1 * 32f, d2 = (t2 - a1).Normalized(), p2 = new V2(-d2.Y, d2.X);
      Mob(EnemyType.Grunt, a1 + d2 * 122f + p2 * -9f, 60f);
      Mob(EnemyType.Grunt, KM(250f, 352f), 60f); Mob(EnemyType.Brute, KM(372f, 318f), 60f); Mob(EnemyType.Sniper, KM(612f, 78f), 60f); Mob(EnemyType.Grunt, KM(330f, 62f), 60f);
      me.Slots[0].Id = AbilityId.Lunge; me.Pos = me.Disp = k0; me.Aim = d1.Angle;
      V2 aimAt = t1;
      for (int f = 0; f < frames; f++) {
        float t = f / 60f;
        if (Math.Abs(t - 0.22f) < 0.5f / 60f) { me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f; cast.Invoke(gm, new object[] { 1, 0, t1, false }); }
        if (Math.Abs(t - 1.62f) < 0.5f / 60f) tg1.Hp = 0f;  // the swings finish it (the mockup's two cuts)
        if (Math.Abs(t - 2.16f) < 0.5f / 60f) aimAt = t2;
        if (Math.Abs(t - 2.38f) < 0.5f / 60f) { me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f; cast.Invoke(gm, new object[] { 1, 0, t2, false }); }
        if (Math.Abs(t - 3.48f) < 0.5f / 60f) tg2.Hp = 0f;
        me.InvulnT = Math.Max(me.InvulnT, 0.5f);
        Pin(); host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt }); gm.Update(1f / 60f);
      }
    } else if (which == "mercy") {
      V2 k0 = KM(118f, 232f);
      var mobs = new[] { Mob(EnemyType.Grunt, KM(262f, 168f), 60f), Mob(EnemyType.Brute, KM(418f, 222f), 60f), Mob(EnemyType.Sniper, KM(560f, 150f), 60f), Mob(EnemyType.Grunt, KM(650f, 290f), 60f),
                         Mob(EnemyType.Brute, KM(300f, 312f), 60f), Mob(EnemyType.Grunt, KM(470f, 342f), 60f), Mob(EnemyType.Sniper, KM(372f, 82f), 60f), Mob(EnemyType.Grunt, KM(660f, 70f), 60f) };
      float[] hp = { 0.18f, 0.22f, 0.7f, 0.2f, 1f, 0.9f, 1f, 0.85f };
      for (int i = 0; i < mobs.Length; i++) mobs[i].Hp = mobs[i].MaxHp * hp[i];
      me.Slots[0].Id = AbilityId.Whirlwind; me.Slots[0].Charges = 1; me.Pos = me.Disp = k0;
      for (int f = 0; f < frames; f++) {
        float t = f / 60f;
        foreach (float s in new[] { 0.76f, 0.93f, 1.10f }) if (Math.Abs(t - s) < 0.5f / 60f) mobs[2].Hp -= mobs[2].MaxHp * 0.18f;  // the ally's shots (the mockup's blue gunner)
        if (Math.Abs(t - 1.35f) < 0.5f / 60f) cast.Invoke(gm, new object[] { 1, 0, mobs[0].Pos, false });
        me.InvulnT = Math.Max(me.InvulnT, 0.5f);
        Pin(); host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = mobs[0].Hp > 0f ? mobs[0].Pos : me.Pos + new V2(100f, 0f) }); gm.Update(1f / 60f);
      }
    } else if (which == "bridge") {
      V2 k0 = KM(108f, 206f);
      float aim = 0.06f;
      foreach (var at in new[] { KM(300f, 212f), KM(560f, 212f) }) Mob(EnemyType.Grunt, at, 300f);
      Mob(EnemyType.Brute, KM(440f, 232f), 300f);
      var melee = new[] { Mob(EnemyType.Grunt, KM(360f, 150f), 300f), Mob(EnemyType.Brute, KM(520f, 290f), 300f), Mob(EnemyType.Grunt, KM(250f, 92f), 300f), Mob(EnemyType.Grunt, KM(212f, 342f), 300f), Mob(EnemyType.Grunt, KM(694f, 196f), 300f) };
      var guns = new[] { KM(612f, 92f), KM(600f, 352f), KM(336f, 372f) };
      foreach (var g0 in guns) Mob(EnemyType.Sniper, g0, 300f);
      me.Slots[Upgrades.UltSlot].Id = AbilityId.BladeTempest; me.Slots[Upgrades.UltSlot].Charges = 1; me.Pos = me.Disp = k0; me.Aim = aim;
      cast.Invoke(gm, new object[] { 1, Upgrades.UltSlot, k0 + V2.FromAngle(aim) * 400f, false });
      V2 hinge = k0 + V2.FromAngle(aim) * Game.BridgeHingeOut;
      for (int f = 0; f < frames; f++) {
        float t = f / 60f;
        foreach (var e in melee) { moving.Add(e); if (t > 1.25f && e.Hp > 0f) e.Pos += (me.Pos - e.Pos).Normalized() * Math.Min(34f / 60f, Math.Max(0f, e.Pos.DistanceTo(me.Pos) - 24f)); else if (t <= 1.25f) e.Pos = pinned[e]; }
        for (int gi = 0; gi < guns.Length; gi++)
          for (float ts = 1.55f + gi * 0.13f; ts < 5.5f; ts += 0.55f)
            if (ts >= t && ts < t + 1f / 60f) { V2 dv = (me.Pos - guns[gi]).Normalized(); shoot.Invoke(gm, new object[] { Defs.SmallShot, guns[gi] + dv * 16f, dv * 260f, 0f, 5f, 8f, 0, false, 0f }); }
        float u = t < 1.45f ? -Game.BridgeHingeOut : -Game.BridgeHingeOut + (300f + Game.BridgeHingeOut) * Smooth((t - 1.45f) / 2.9f);
        me.Pos = hinge + V2.FromAngle(aim) * u; me.InvulnT = 1f;
        Pin(); host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = me.Pos + V2.FromAngle(aim) * 200f }); gm.Update(1f / 60f);
      }
    }
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    Console.WriteLine("knightdps {0}: steel {1} window {2:0.00} bullets {3} zones {4} effects {5} me ({6:0},{7:0})", which, me.Steel, me.SteelT, gm.Bullets.Count, gm.Zones.Count, gm.Effects.Count, me.Pos.X, me.Pos.Y);
  }

  static float Smooth(float f) { f = Math.Max(0f, Math.Min(1f, f)); return f * f * (3f - 2f * f); }
}
