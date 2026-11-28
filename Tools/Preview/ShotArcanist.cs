using System;
using System.Linq;
using System.Reflection;
using Arena;

// ARCANIST=tank|healer|dps (mode hud; ZOOM=1.6 for a closer look): the Arcanist's kit (Core/Arcanist.cs, Game/ArcanistLook.cs) drawn by
// the game's own paths.
//   tank: Orbit Shards round you (one flung at a grunt), Kinetic Grasp holding a Brute, a Grand Sigil with enemy shots frozen on its rim.
//   healer: the Glyph Stylus (rune bolts in flight through an ally), a Sanctum Sigil with four of its six runes left.
//   dps: Dancing Blades (one dancing on a Brute, DANCE=<s> into its Crossing Cuts, default 0.4; one flying, one waiting), Runic Array's rift open
//     with blades curving in, a Telekinetic Crush grip open (three grunts lifted, three caught shots, the meter at 62%).
static partial class Shot {
  static void ArcanistScene() {
    string mode = Environment.GetEnvironmentVariable("ARCANIST");
    if (mode == null) return;
    var gm = host.Game; gm.Bot = false;
    var bf = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", bf);
    var spawnM = typeof(Game).GetMethod("SpawnMinion", bf);
    var addZ = typeof(Game).GetMethod("AddZone", bf);
    var spawnB = typeof(Game).GetMethod("SpawnBullet", bf);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    if (gm.Profile != null) foreach (EnemyType et2 in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et2.ToString()] = 99;
    foreach (var bp in gm.Players.Values) { bp.AegisT = 0f; bp.BarrierT = 0f; }
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(-400f, -400f);
    me.Role = mode == "tank" ? Role.Tank : mode == "healer" ? Role.Healer : Role.Dps; me.Def = Defs.Get(me.Role);
    me.Kit = Kit.Arcanist; me.Weapon = Defs.Weapon(me.Role, Kit.Arcanist, 0);
    me.MaxHp = me.Def.Hp * Defs.KitHpMult(Kit.Arcanist); me.Hp = me.MaxHp;
    me.Slots[0].Id = Upgrades.FirstAbility(me.Role, Kit.Arcanist, 0); me.Slots[Upgrades.UltSlot].Id = Upgrades.Ultimate(me.Role, Kit.Arcanist);
    me.Pos = me.Disp = new V2(700f, 470f);
    Func<EnemyType, V2, float, Enemy> put = (t, p, face) => {
      var e = (Enemy)spawn.Invoke(gm, new object[] { t, p, 0f, 0, false });
      e.SpawnT = 0f; e.Pos = e.Disp = p; e.Facing = face; e.FireT = 99f; return e;
    };
    Func<MinionKind, V2, Minion> companion = (k, p) => { var m = (Minion)spawnM.Invoke(gm, new object[] { k, me, p, -1f, true }); m.Pos = m.Disp = p; return m; };
    Func<ZoneKind, V2, float, float, float, Zone> zone = (k, p, r, dur, left) => { var z = (Zone)addZ.Invoke(gm, new object[] { k, me, p, r, dur }); z.T = left; return z; };
    Func<byte, V2, V2, int, Bullet> shot = (k, p, v, owner) => { var b = (Bullet)spawnB.Invoke(gm, new object[] { k, p, v, 0f, 3f, 10f, owner, false, 0f }); b.Pos = p; return b; };
    if (mode == "tank") {
      var brute = put(EnemyType.Brute, me.Pos + new V2(60f, -10f), MathX.Pi);
      me.Aim = 0f;
      brute.Pos = brute.Disp = Game.GraspHoldPoint(me, brute.Radius); brute.StunT = 1f;
      me.KgHeld = brute.Id;
      var grunt = put(EnemyType.Grunt, new V2(980f, 330f), MathX.Pi);
      for (int i = 0; i < 4; i++) {
        float a = 0.9f + MathX.Tau * i / 4f;
        var m = companion(MinionKind.ArcShard, me.Pos + V2.FromAngle(a) * Game.OsOrbit);
        m.Aim = a + MathX.Pi / 2f;
        if (i == 0) { m.Sent = true; m.Pos = m.Disp = me.Pos + (grunt.Pos - me.Pos) * 0.6f; m.Aim = (grunt.Pos - me.Pos).Angle; m.Wind = 0.5f; }
      }
      var sig = zone(ZoneKind.GrandSigil, me.Pos, Game.SigilRadius, 8f, 5f);
      for (int k = 0; k < 11; k++) {
        float a = -2.4f + k * 0.42f;
        shot(Defs.SmallShot, sig.Pos + V2.FromAngle(a) * (Game.SigilRadius - 10f), V2.FromAngle(a + MathX.Pi) * 200f, 0);
      }
      put(EnemyType.Grunt, me.Pos + new V2(-120f, 90f), 0f).SlowT = 1f;
      put(EnemyType.Sniper, new V2(420f, 200f), 0.6f);
      put(EnemyType.Sniper, new V2(1000f, 650f), MathX.Pi * 1.2f);
    } else if (mode == "healer") {
      var ally = gm.Players.Values.FirstOrDefault(q => q != me);
      if (ally != null) { ally.Pos = ally.Disp = me.Pos + new V2(150f, -30f); ally.Hp = ally.MaxHp * 0.55f; }
      me.Aim = (new V2(1000f, 400f) - me.Pos).Angle;
      var z = zone(ZoneKind.SanctumSigil, me.Pos, Game.SanctumRadius, 8f, 5.5f);
      z.Aim = 1 | 2 | 8 | 32;
      for (int k = 0; k < 3; k++) shot(Defs.GlyphShot, me.Pos + V2.FromAngle(me.Aim) * (60f + k * 95f), V2.FromAngle(me.Aim) * 650f, me.Id);
      put(EnemyType.Grunt, new V2(1010f, 390f), MathX.Pi);
      put(EnemyType.Grunt, Game.SanctumRune(z, 2) + new V2(30f, -18f), MathX.Pi * 1.3f);
      put(EnemyType.Sniper, new V2(460f, 230f), 0.7f);
    } else {
      var brute = put(EnemyType.Brute, new V2(960f, 400f), MathX.Pi);
      brute.Hp = brute.MaxHp * 0.6f;
      me.Aim = (brute.Pos - me.Pos).Angle;
      var dance = companion(MinionKind.ArcBlade, brute.Pos); dance.Sent = true; dance.Wind = 1f; dance.Aim = me.Aim;  // came in on your aim
      float danceAt; if (float.TryParse(Environment.GetEnvironmentVariable("DANCE") ?? "0.4", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out danceAt)) ArenaApp.BladeDancePreview = danceAt;
      var fly = companion(MinionKind.ArcBlade, me.Pos + (brute.Pos - me.Pos) * 0.45f); fly.Sent = true; fly.Wind = 0.5f; fly.Aim = me.Aim;
      var wait = companion(MinionKind.ArcBlade, me.Pos + V2.FromAngle(me.Aim + MathX.Pi) * Game.BladeHover); wait.Aim = me.Aim;
      me.ArrayT = 4f;
      me.AimAt = brute.Pos; ArenaApp.ArrayOpenPreview = 1f;
      for (int k = 0; k < 4; k++) {  // rift blades curving in on the Brute
        V2 from = Game.ArrayRift(me.Pos, me.Aim), to = brute.Pos - from;
        V2 side = new V2(-to.Y, to.X).Normalized() * ((k % 2) * 2f - 1f) * (60f - k * 12f);
        V2 at = from + to * (0.25f + k * 0.18f) + side, dir = (brute.Pos - at).Normalized() - side.Normalized() * 0.4f;
        shot(Defs.SpectralBlade, at, dir.Normalized() * 700f, me.Id);
      }
      var crush = zone(ZoneKind.ArcCrush, new V2(545f, 335f), Game.CrushGripR, Game.CrushZoneLife, Game.CrushZoneLife);  // the grip, open
      crush.Aim = 3f;  // three caught shots orbiting inside
      me.CrushOn = true; me.CrushMeter = 0.62f; me.CrushZone = crush.Id;
      put(EnemyType.Grunt, crush.Pos + new V2(-22f, 8f), 0f).StunT = 0.3f;
      put(EnemyType.Grunt, crush.Pos + new V2(20f, -16f), 0f).StunT = 0.3f;
      put(EnemyType.Grunt, crush.Pos + new V2(6f, 26f), 0f).StunT = 0.3f;
      put(EnemyType.Sniper, new V2(1050f, 640f), MathX.Pi * 1.2f);
    }
    Console.Error.WriteLine("arcanist: " + mode);
  }
}
