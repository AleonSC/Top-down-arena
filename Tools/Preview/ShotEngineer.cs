using System;
using System.Linq;
using System.Reflection;
using Arena;

// ENGINEER=pylon|breach|real|bots [ENGT=<s>]: the Engineer mockup (Docs/Engineer.md, Game/EngineerLook.cs) drawn in the real arena.
//   pylon: a Drag Pylon at (900, 450) with five grunts crossing it (their drag rings), and the old pylon folding at (520, 300).
//   breach: with EXPANDMAP=0 (the hub only): you under the top wall, ENGT seconds into Breach (0.4 hammer drawing, 0.8 the hit,
//     1.6 the room building in, 3 done); BREACHSHAPE=ring|cross|l|t|chamfer|stash, BREACHLOOT=0-4 (0 vectors, 1 crate, 4 ambush).
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
    if (mode == "bots") {
      // ENGINEER=bots ENGROLE=dps|tank|healer [ENGBUILD=<0-1>] (ZOOM=2.2): the robot weapons (Core/EngineerBots.cs, Game/EngineerBotLook.cs),
      // drawn by the game's own paths: you mid-attack (the Vector Blade's swipe, the Iron Fist's shove, the Stasis Gun's field on
      // two grunts) and your robot sent at a Brute (ENGBUILD: still building in).
      var bf = BindingFlags.NonPublic | BindingFlags.Instance;
      var spawnM = typeof(Game).GetMethod("SpawnMinion", bf); var addZ = typeof(Game).GetMethod("AddZone", bf);
      string role = Environment.GetEnvironmentVariable("ENGROLE") ?? "dps";
      me.Role = role == "tank" ? Role.Tank : role == "healer" ? Role.Healer : Role.Dps; me.Def = Defs.Get(me.Role);
      me.Kit = Kit.Engineer; me.Weapon = Defs.Weapon(me.Role, Kit.Engineer, 1);
      me.MaxHp = me.Def.Hp; me.Hp = me.MaxHp;
      me.Pos = me.Disp = new V2(640f, 520f);
      var target = put(EnemyType.Brute, new V2(905f, 470f), MathX.Pi);
      target.Hp = target.MaxHp * 0.6f;
      var kind = Game.BotFor(me.Weapon.Id);
      var b = (Minion)spawnM.Invoke(gm, new object[] { kind, me, new V2(800f, 500f), -1f, true });
      b.MaxHp = Game.BotMaxHp(3, kind); b.Hp = b.MaxHp * 0.75f; b.GuardFrac = 0.75f; b.Aim = (target.Pos - b.Pos).Angle;
      b.Wind = 0.7f; b.Stacks = 1; b.LinkTo = target.Id; b.SentT = 5f;
      string eb2 = Environment.GetEnvironmentVariable("ENGBUILD");
      if (eb2 != null) { b.Wind = 2f + float.Parse(eb2, inv); b.Stacks = 0; b.LinkTo = 0; }
      if (me.Role == Role.Healer) {
        var g1 = put(EnemyType.Grunt, new V2(545f, 445f), 0.4f); var g2 = put(EnemyType.Grunt, new V2(585f, 420f), 0.9f);
        var z = (Zone)addZ.Invoke(gm, new object[] { ZoneKind.Stasis, me, new V2(565f, 435f), Game.StasisRadius, 100000f }); z.T = 99990f;
        me.Aim = (z.Pos - me.Pos).Angle; me.Firing = me.FiringSeen = true;
        me.Hp = me.MaxHp * 0.7f;
        put(EnemyType.Grunt, new V2(900f, 600f), MathX.Pi * 1.1f);
      } else {
        var near = put(EnemyType.Grunt, new V2(700f, 470f), MathX.Pi);
        var near2 = put(EnemyType.Grunt, new V2(712f, 545f), MathX.Pi);
        me.Aim = (near.Pos - me.Pos).Angle;
        V2 tip = me.Pos + V2.FromAngle(me.Aim) * (me.Role == Role.Tank ? 95f : 88f);
        me.FireCd = me.Weapon.FireInterval * 0.8f;
        gm.Effects.Add(new Effect { Kind = me.Role == Role.Tank ? FxKind.FistShove : FxKind.Slash, Pos = me.Pos, Pos2 = tip,
          Color = Kits.Get(kind).Color, Radius = me.Role == Role.Tank ? Game.FistHalf : Game.BladeHalf, T = 0.08f, Dur = me.Role == Role.Tank ? 0.35f : 0.22f });
        if (me.Role == Role.Tank) { near.Pos = near.Disp = new V2(745f, 450f); near2.Pos = near2.Disp = new V2(752f, 580f); }
      }
      Console.Error.WriteLine("engineer: bots " + role);
      return;
    }
    if (mode == "real") {
      // ENGINEER=real: the built class in a run, drawn by the game's own paths (no mock): you as a DPS Engineer and a Drag Pylon
      // zone with grunts crossing it.
      me.Kit = Kit.Engineer; me.Weapon = Defs.Weapon(Role.Dps, Kit.Engineer, 0);
      me.Pos = me.Disp = new V2(700f, 560f);
      var bf = BindingFlags.NonPublic | BindingFlags.Instance;
      var target = put(EnemyType.Brute, new V2(985f, 440f), MathX.Pi);
      target.Hp = target.MaxHp * 0.55f;
      var z = (Zone)typeof(Game).GetMethod("AddZone", bf).Invoke(gm, new object[] { ZoneKind.DragPylon, me, new V2(560f, 330f), Game.PylonRadius, 100000f });
      z.T = 99990f;
      put(EnemyType.Grunt, new V2(600f, 300f), MathX.Pi * 0.2f); put(EnemyType.Grunt, new V2(520f, 380f), MathX.Pi * 0.1f);
      put(EnemyType.Grunt, new V2(1080f, 520f), MathX.Pi * 1.1f);
      me.Aim = (new V2(600f, 300f) - me.Pos).Angle;
      string er = Environment.GetEnvironmentVariable("ENGROLE");  // ENGROLE=tank|healer: that role's kit instead (a Vector Wall; Med Pylon and Safe Room)
      if (er != null) {
        var spawnM = typeof(Game).GetMethod("SpawnMinion", bf); var addZ = typeof(Game).GetMethod("AddZone", bf);
        gm.Zones.Clear();
        me.Role = er == "tank" ? Role.Tank : Role.Healer; me.Def = Defs.Get(me.Role); me.Weapon = Defs.Weapon(me.Role, Kit.Engineer, 0);
        if (er == "tank") {
          for (int i = 0; i < 5; i++) { var b = (Minion)spawnM.Invoke(gm, new object[] { MinionKind.VectorBlock, me, new V2(560f + i * 2f, 300f + (i - 2) * 36f), 8f, false }); b.Aim = 0f; b.GuardFrac = 1f; b.T = 6f; }
        } else {
          var mp = (Zone)addZ.Invoke(gm, new object[] { ZoneKind.MedPylon, me, new V2(560f, 330f), Game.PylonRadius, 100000f }); mp.T = 99990f;
          var sr = (Zone)addZ.Invoke(gm, new object[] { ZoneKind.SafeRoom, me, new V2(1000f, 520f), Game.SafeDomeRadius, 12f }); sr.T = 8f;
          me.Hp = me.MaxHp * 0.6f;
        }
      }
      string el = Environment.GetEnvironmentVariable("ENGLOOT");  // ENGLOOT=<0-4>: a Breach room's loot node beside you (0 vectors, 1 crate, 4 ambush)
      if (el != null) gm.Nodes.Add(new MapNode { X = 860, Y = 640, Kind = NodeKind.BreachLoot, Rule = byte.Parse(el) });
      Console.Error.WriteLine("engineer: real");
      return;
    }
    if (mode == "pylon") {    } else if (mode == "pylon") {
      me.Pos = me.Disp = new V2(700f, 580f); me.Aim = (new V2(900f, 450f) - me.Pos).Angle;
      m.Pylon = true; m.PylonPos = new V2(900f, 450f); m.PylonAge = 2f; m.OldPylonPos = new V2(520f, 300f); m.OldFold = 0.45f;
      var spots = new[] { new V2(1120f, 400f), new V2(990f, 420f), new V2(900f, 520f), new V2(820f, 380f), new V2(1150f, 520f) };
      foreach (var s in spots) put(EnemyType.Grunt, s, MathX.Pi);
    } else if (mode == "breach") {
      string[] names = { "ring", "cross", "l", "t", "chamfer", "stash" };
      var shape = (SurpriseShape)Math.Max(0, Array.IndexOf(names, Environment.GetEnvironmentVariable("BREACHSHAPE") ?? "cross"));
      V2 wall = new V2(800f, 0f);
      me.Pos = me.Disp = new V2(800f, 170f); me.Aim = -MathX.Pi * 0.5f;
      m.Breach = true; m.WallPt = wall; m.Out = new V2(0f, -1f); m.BreachT = et;
      put(EnemyType.Grunt, new V2(690f, 70f), MathX.Pi * 1.4f); put(EnemyType.Grunt, new V2(930f, 80f), MathX.Pi * 1.7f);
      if (et >= 0.75f) {
        var opts = Expansion.GridSurpriseOptions(gm.Expanded, shape, wall + new V2(0f, -120f));
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
    ArenaApp.engineerMock = m;
    Console.Error.WriteLine("engineer: " + mode + " t=" + et + " enemies=" + gm.Enemies.Count);
  }
}
