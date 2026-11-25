using System;
using System.Reflection;
using Arena;

// COLSHEET=1: four Colossi in a row, one per phase (Awakening, Crumbling, Molten Core, Last Stand; ColossusBodyLook.cs).
// COLSHEET=2: its poses in a row: a stomp winding up, the slam, bracing for a charge, charging, a throw, dazed
// (COLSTAGE=<0-3>: in that phase, default 0). Mode hud, e.g. COLSHEET=1 ZOOM=1.8 CAMAT=800,450 shot.sh x hud 3 1 1
// (COLSHEET=2: ZOOM=1.1). COLCRUMBLE=<s into the change to Crumbling> (no COLSHEET): its plates rattling loose (to 0.75 s),
// then flying off to where the cover slabs land (at 2 s); mode hud, shot.sh x hud 3 1 1.
// COLFIGHT=<scene> (mode hud, shot.sh x hud 3 1 1): the fight's looks in the real arena, built as they'd stand:
//   stomp (Awakening, three times its size, a ring of orbs with three openings spreading past you), hold (a boulder
//   held over its head, the red ring where it'll land) and throw (one in the air), cracks (four scars and one erupting),
//   stagger (kneeling, its Break meter at COLBREAK, default 0.45), break (COLT=<s> after the break, default 0.35: its
//   shell blasting off), crumble (Crumbling, twice its size, chips coming off it), last (the Last Stand, four openings).
static partial class Shot {
  static void ColossusSheetScene() {
    if (ColossusFightScene()) return;
    string sheet = Environment.GetEnvironmentVariable("COLSHEET");
    string crumble = Environment.GetEnvironmentVariable("COLCRUMBLE");
    if (sheet == null && crumble == null) return;
    var gm = host.Game; gm.Bot = false;
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    foreach (var q in gm.Players.Values) q.Pos = q.Disp = new V2(-400f, -400f);
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
    if (crumble != null) {
      var at = new V2(800f, 450f);
      var en = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Colossus, at, 0f, 0, false });
      en.SpawnT = 0f; en.Spawning = false; en.Pos = en.Disp = at; en.Facing = MathX.Pi / 2f; en.Hp = en.MaxHp * 0.64f;
      en.Colossus = new ColossusState { Stage = ColStage.Awakening, TransTo = ColStage.Crumbling, TurnT = 999f,
        TransT = Math.Max(0.001f, Game.ColTransTime - float.Parse(crumble, System.Globalization.CultureInfo.InvariantCulture)) };
      return;
    }
    bool poses = sheet == "2";
    int n = poses ? 6 : 4;
    var hp = new[] { 0.9f, 0.5f, 0.2f, 0.05f };
    int stage = int.Parse(Environment.GetEnvironmentVariable("COLSTAGE") ?? "0");
    for (int i = 0; i < n; i++) {
      var at = poses ? new V2(800f + (i - 2.5f) * 220f, 450f) : new V2(485f + i * 210f, 450f);
      var en = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Colossus, at, 0f, 0, false });
      en.SpawnT = 0f; en.Spawning = false; en.Pos = en.Disp = at; en.Facing = MathX.Pi / 2f;
      var c = en.Colossus = new ColossusState { Stage = (ColStage)(poses ? stage : i), TurnT = 999f, ChargeDir = MathX.Pi / 2f };
      en.Hp = en.MaxHp * hp[(int)c.Stage];
      if (!poses) continue;
      switch (i) {
        case 0: c.Rings.Add(new StompRing { Center = at, Radius = en.Radius + 6f, Warn = Game.RingWarn * 0.15f, Gap1 = 1f, Orbs = true }); break;  // fists up
        case 1: c.Rings.Add(new StompRing { Center = at, Radius = en.Radius + 16f, Warn = 0f, Gap1 = 1f, Orbs = true }); break;  // the slam
        case 2: c.ChargeWarn = Game.ColChargeWarn * 0.2f; break;
        case 3: c.ChargeLeft = 400f; break;
        case 4: c.Drops.Add(new ColDrop { Kind = ColDrop.Boulder, From = at, Pos = at + new V2(260f, 160f), T = 0.05f, Dur = 1f, Radius = 40f }); break;
        case 5: c.DazeT = 1f; break;
      }
    }
  }

  static bool ColossusFightScene() {
    string scene = Environment.GetEnvironmentVariable("COLFIGHT");
    if (scene == null) return false;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    var gm = host.Game; gm.Bot = false;
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
    V2 mid = new V2(800f, 400f);
    var en = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Colossus, mid, 0f, 0, false });
    en.SpawnT = 0f; en.Spawning = false; en.Pos = en.Disp = mid; en.Facing = MathX.Pi / 2f;
    var stage = scene == "crumble" ? ColStage.Crumbling : scene == "last" ? ColStage.LastStand : ColStage.Awakening;
    var c = en.Colossus = new ColossusState { Stage = stage, TurnT = 999f, Step = 0 };
    en.Hp = en.MaxHp * (stage == ColStage.Awakening ? 0.8f : stage == ColStage.Crumbling ? 0.5f : 0.06f);
    var me = host.Game.Players.ContainsKey(1) ? host.Game.Players[1] : null;
    V2 you = mid + new V2(-40f, 330f);
    if (me != null) { me.Pos = me.Disp = you; me.Aim = -MathX.Pi / 2f; }
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(1350f, 760f);
    if (stage >= ColStage.Crumbling) for (int i = 0; i < Game.SlabSpots.Length; i++) { bool up = i >= 2; c.Slabs.Add(new ColSlab { Pos = Game.SlabSpots[i], W = up ? Game.SlabH : Game.SlabW, H = up ? Game.SlabW : Game.SlabH, Hp = 40 }); }
    Func<float, float, float[], StompRing> orbRing = (rad, rot, gaps) => {
      var r = new StompRing { Center = mid, Radius = rad, Orbs = true, Rot = rot, Gaps = gaps.Length };
      r.Gap1 = gaps[0]; if (gaps.Length > 1) r.Gap2 = gaps[1]; if (gaps.Length > 2) r.Gap3 = gaps[2]; if (gaps.Length > 3) r.Gap4 = gaps[3];
      c.Rings.Add(r); return r;
    };
    float aim = (you - mid).Angle;
    switch (scene) {
      case "stomp": orbRing(300f, 0.01f, new[] { aim + 0.32f, aim + 2.3f, aim - 2.1f }); break;
      case "last": orbRing(220f, 0.01f, new[] { aim - 0.4f, aim + 1.4f, aim + 3.1f, aim - 1.9f }); orbRing(420f, 0.02f, new[] { aim + 0.45f, aim + 1.9f, aim - 2.5f, aim - 1.1f }); break;
      case "hold": c.HoldT = 0.35f; c.HoldSide = 1; c.HoldAim = you + new V2(90f, -20f); break;
      case "throw": c.Drops.Add(new ColDrop { Kind = ColDrop.Boulder, From = Game.ColHand(en, -1), Pos = you + new V2(110f, -10f), T = 0.6f, Dur = Game.ColBoulderFlight, Radius = Game.ColBoulderR }); break;
      case "cracks":
        for (int k = 0; k < 4; k++) c.Cracks.Add(new ColCrack { From = mid + new V2((k - 1.5f) * 90f, 60f), Ang = 0.6f + k * 0.62f, Len = 900f, Max = 900f, Seed = 11 + k * 5, State = ColCrack.Dormant });
        { var k2 = new ColCrack { From = mid + new V2(0f, 60f), Ang = aim + 0.1f, Max = 900f, Seed = 77, State = ColCrack.Erupting, Wait = 0.3f }; k2.Len = 900f; c.Cracks.Add(k2); }
        break;
      case "stagger": c.Stagger = true; c.Break = float.Parse(Environment.GetEnvironmentVariable("COLBREAK") ?? "0.45", inv); c.TransTo = ColStage.Crumbling; break;
      case "break": c.TransTo = ColStage.Crumbling; c.TransT = Math.Max(0.001f, Game.ColTransTime - float.Parse(Environment.GetEnvironmentVariable("COLT") ?? "0.35", inv)); break;
    }
    en.Radius = Game.ColRadius(en);
    return true;
  }
}
