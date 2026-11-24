using System;
using System.Reflection;
using Arena;

// TKDOORS=1: the Turnkey's Cell Doors (Game/CellDoorLook.cs) in the outer wall north of the Rooms ring: calm, a Rioter due,
// opening, jammed, and one being built with the Turnkey hammering; its traps on the floor below (TKBEEP=1: the third mine
// beeping). TKT=<s>: the clock; TKCYCLE=1: each door counting down and opening (for clip.sh TKT=0:3.6:0.08); TKCLOSE=1: the
// traps close up (ZOOM=3.4). Use with EXPANDMAP=0 MAPVIEW=2,2,1 NINJA=0 ZOOM=2 shot.sh x hud 3.
static partial class Shot {
  static void CellDoorScene() {
    if (Environment.GetEnvironmentVariable("TKDOORS") == null) return;
    var gm = host.Game; gm.Layout = LayoutId.Open; V2 o = new V2(320f, -400f);  // the outer wall's top edge, off to the right of its north door gm.Bot = false; gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Patches.Clear(); gm.Props.Clear(); gm.Effects.Clear();
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    if (Environment.GetEnvironmentVariable("TKT") != null) gm.AnimT = float.Parse(Environment.GetEnvironmentVariable("TKT"), inv);
    me.Pos = me.Disp = Environment.GetEnvironmentVariable("TKCLOSE") != null ? new V2(800f, 520f) : o + new V2(800f, 140f);
    bool close = Environment.GetEnvironmentVariable("TKCLOSE") != null; V2 tr = close ? me.Pos + new V2(-190f, -80f) : o + new V2(600f, 250f); var meAt = o + new V2(800f, 165f);  // (the camera follows you) me.Aim = MathX.Pi / 2f + 0.4f;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99; me.InvulnT = 1f;
    foreach (var pl in gm.Players.Values) if (pl != me) pl.Pos = pl.Disp = new V2(300f, 1200f);
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
    var mine = new System.Collections.Generic.HashSet<int>();
    Func<EnemyType, V2, Enemy> put = (ty, at) => { var en = (Enemy)spawn.Invoke(gm, new object[] { ty, at, 0f, 0, false }); en.SpawnT = 0f; en.Spawning = false; en.Pos = en.Disp = at; en.Facing = (me.Pos - at).Angle; mine.Add(en.Id); return en; };
    float down = MathX.Pi / 2f, wallY = Game.DoorIn + Rooms.WallT / 2f;  // (a door body: DoorIn in from the wall face)
    var k = put(EnemyType.Turnkey, o + new V2(470f, 130f)); k.Phase = 3; k.PhaseT = Game.TurnkeyBuild * 0.35f;
    var bd = put(EnemyType.CellDoor, o + new V2(500f, wallY)); bd.Facing = down; bd.ParentId = k.Id; bd.Phase = 3; bd.PhaseT = k.PhaseT; bd.Elite = false; bd.Traits = 0;
    k.Facing = (bd.Pos - k.Pos).Angle;
    // four doors: calm, a Rioter due, open (one coming out), jammed
    float[] xs = { 645f, 790f, 935f, 1080f };
    for (int i = 0; i < 4; i++) {
      var d = put(EnemyType.CellDoor, o + new V2(xs[i], wallY));
      d.Facing = down; d.ParentId = k.Id;
      d.Elite = false; d.Traits = 0; d.Phase = (byte)(i == 3 ? 2 : 1); d.T2 = i == 0 ? 3.5f : i == 1 ? 0.5f : 4f; d.PhaseT = i == 2 ? Game.DoorSwing * 0.5f : 0f;
      if (Environment.GetEnvironmentVariable("TKCYCLE") != null && i < 3) {  // TKCYCLE=1 (with TKT): each door counting down to its Rioter, then swinging open
        float left = new[] { 3.2f, 2.0f, 0.8f }[i] - gm.AnimT % 3.6f;
        d.T2 = Math.Max(0.01f, left); d.PhaseT = left < 0f && left > -Game.DoorSwing ? Game.DoorSwing + left : 0f;
        if (left <= -Game.DoorSwing) d.T2 = 4.5f + left;
      }
    }
    var ri = Environment.GetEnvironmentVariable("TKCYCLE") != null ? put(EnemyType.Rioter, o + new V2(-900f, 500f)) : put(EnemyType.Rioter, o + new V2(xs[2], wallY + 40f)); ri.Facing = down;
    // traps along the floor: mines arming, armed, beeping; shackles open and snapped; a mop slick
    float[] mineAge = { 0.4f, 3f, 3f }; int mineBeep = Environment.GetEnvironmentVariable("TKBEEP") != null ? 2 : 1;
    for (int i = 0; i < 3; i++) { var m = put(EnemyType.FloorMine, tr + new V2(i * (close ? 60f : 55f), 0f)); m.T2 = mineAge[i]; m.Phase = (byte)(i == 0 ? 0 : i == 2 ? mineBeep : 1); m.PhaseT = 0.2f; }
    for (int i = 0; i < 2; i++) { var sn = put(EnemyType.ShackleTrap, tr + new V2((close ? 210f : 280f) + i * (close ? 60f : 55f), 0f)); sn.T2 = 3f; sn.Phase = (byte)(i + 1); sn.PhaseT = 0.4f; }
    foreach (var id in new System.Collections.Generic.List<int>(gm.Enemies.Keys)) if (!mine.Contains(id)) gm.Enemies.Remove(id);
    gm.Patches[81] = new PathPatch { Id = 81, Kind = PatchKind.Slick, Pos = close ? tr + new V2(370f, -5f) : o + new V2(1040f, 210f), Warn = Game.SlickWarn, Life = Game.SlickWarn + Game.SlickLife, R0 = Game.SlickRadius, R1 = Game.SlickRadius, Age = 3f };
  }
}
