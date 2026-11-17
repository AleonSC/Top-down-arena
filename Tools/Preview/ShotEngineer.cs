using System;
using System.Linq;
using System.Reflection;
using Arena;

// ENGINEER=strider|pylon|breach [ENGT=<s>]: the Engineer mockup (Docs/Engineer.md, Game/EngineerLook.cs) drawn in the real arena.
//   strider: you (a DPS with the hard hat) at (560, 560), the Strider punching a locked-on Charger with grunts round it; ENGT is
//     its build time (ENGT=0.6: half drawn; default 9 = built, mid-slam).
//   pylon: a Drag Pylon at (900, 450) with five grunts crossing it (their drag rings), and the old pylon folding at (520, 300).
//   breach: with EXPANDMAP=0 (the hub only): you under the top wall, ENGT seconds into Breach (0.4 hammer drawing, 0.8 the hit,
//     1.6 the room building in, 3 done); BREACHSHAPE=ring|cross|l|t|chamfer|stash, BREACHLOOT=0-4 (vectors, crate, repair, bench, ambush).
// Use with mode hud.
static partial class Shot {
  static void EngineerScene() {
    string mode = Environment.GetEnvironmentVariable("ENGINEER");
    if (mode == null) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    float et = float.Parse(Environment.GetEnvironmentVariable("ENGT") ?? (mode == "breach" ? "2" : "9"), inv);
    var gm = host.Game; gm.Bot = false;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    if (gm.Profile != null) foreach (EnemyType et2 in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et2.ToString()] = 99;
    me.Kit = Kit.Soldier; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Soldier, 0);
    me.MaxHp = me.Def.Hp; me.Hp = me.MaxHp;
    foreach (var bp in gm.Players.Values) { bp.AegisT = 0f; bp.BarrierT = 0f; }
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(-400f, -400f);
    Func<EnemyType, V2, float, Enemy> put = (t, p, face) => {
      var e = (Enemy)spawn.Invoke(gm, new object[] { t, p, 0f, 0, false });
      e.SpawnT = 0f; e.Pos = e.Disp = p; e.Facing = face; e.FireT = 99f; return e;
    };
    var m = new EngineerMock();
    if (mode == "strider") {
      me.Pos = me.Disp = new V2(770f, 540f);
      var target = put(EnemyType.Charger, new V2(985f, 440f), MathX.Pi);
      target.Hp = target.MaxHp * 0.45f;
      put(EnemyType.Grunt, new V2(1060f, 360f), MathX.Pi * 0.9f);
      put(EnemyType.Grunt, new V2(1080f, 520f), MathX.Pi * 1.1f);
      put(EnemyType.Grunt, new V2(800f, 300f), MathX.Pi * 0.7f).Hp *= 0.6f;
      me.Aim = (new V2(800f, 300f) - me.Pos).Angle;
      m.Strider = true; m.StriderPos = new V2(935f, 445f); m.Face = (target.Pos - m.StriderPos).Angle;
      m.Build = Math.Min(1f, et / 1.2f); m.Step = 1.1f; m.Punch = 0.85f; m.Hp = 0.72f; m.LockOn = target.Id;
    } else if (mode == "pylon") {
      me.Pos = me.Disp = new V2(700f, 580f); me.Aim = (new V2(900f, 450f) - me.Pos).Angle;
      m.Pylon = true; m.PylonPos = new V2(900f, 450f); m.PylonAge = 2f; m.OldPylonPos = new V2(520f, 300f); m.OldFold = 0.45f;
      var spots = new[] { new V2(1120f, 400f), new V2(990f, 420f), new V2(900f, 520f), new V2(820f, 380f), new V2(1150f, 520f) };
      foreach (var s in spots) put(EnemyType.Grunt, s, MathX.Pi);
      m.Strider = true; m.StriderPos = new V2(690f, 470f); m.Face = 0f; m.Step = 0.4f;
    } else if (mode == "breach") {
      string[] names = { "ring", "cross", "l", "t", "chamfer", "stash" };
      var shape = (SurpriseShape)Math.Max(0, Array.IndexOf(names, Environment.GetEnvironmentVariable("BREACHSHAPE") ?? "cross"));
      V2 wall = new V2(800f, 0f);
      me.Pos = me.Disp = new V2(800f, 170f); me.Aim = -MathX.Pi * 0.5f;
      m.Breach = true; m.WallPt = wall; m.Out = new V2(0f, -1f); m.BreachT = et;
      put(EnemyType.Grunt, new V2(690f, 70f), MathX.Pi * 1.4f); put(EnemyType.Grunt, new V2(930f, 80f), MathX.Pi * 1.7f);
      if (et >= 0.75f) {
        var opts = Expansion.SurpriseOptions(gm.Expanded, shape, wall + new V2(0f, -120f));
        if (opts.Count > 0) {
          var rooms = opts[0].Rooms; gm.Expanded.AddRange(rooms);
          typeof(Game).GetField("expWorld", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(gm, null);
          gm.SurpriseKind = rooms[0].Surprise; gm.SurpriseWave = gm.Wave; gm.SurpriseAge = et - 0.75f;
          float x0 = rooms.Min(r => r.X), x1 = rooms.Max(r => r.X1), y0 = rooms.Min(r => r.Y), y1 = rooms.Max(r => r.Y1);
          m.Loot = int.Parse(Environment.GetEnvironmentVariable("BREACHLOOT") ?? "0"); if (m.Loot == 0 && shape == SurpriseShape.Stash) m.Loot = -1;  // the Stash draws its own vector cache m.LootPos = new V2((x0 + x1) / 2f, (y0 + y1) / 2f); m.LootAge = et - 1.5f;
        } else Console.Error.WriteLine("breach: no room fits");
      }
      foreach (var e in gm.Enemies.Values) if (et >= 0.75f) { V2 d = (e.Pos - wall); e.Pos = e.Disp = e.Pos + d.Normalized() * 70f; }
    }
    m.Model = int.Parse(Environment.GetEnvironmentVariable("ENGMODEL") ?? "0");  // ENGMODEL=0-2 the Engineer look, -1 the stand-in Soldier
    m.FollowLook = int.Parse(Environment.GetEnvironmentVariable("ENGFOLLOW") ?? "0");  // ENGFOLLOW=0-2 the Strider look
    ArenaApp.engineerMock = m;
    Console.Error.WriteLine("engineer: " + mode + " t=" + et + " enemies=" + gm.Enemies.Count);
  }
}
