using System;
using System.Reflection;
using Arena;

// The Warden's net and Holding Cells (Core/HoldingCell.cs, Game/HoldingCellLook.cs). Mode hud, 2 players, a Warden wave:
//   HOLDCELL=<s>   you locked in cell C-05 that long (the clock down by that and three ability casts), Riot Guards
//                  round you (one winding up, one coming through a gap); HOLDCELLS=2: your friend next door in C-04,
//                  so the fight is paused. HOLDCELL=0.2: the fade in.
//   HOLDLOCK=1     you still fighting, your friend locked up: their padlock out in the arena, a shot on its way,
//                  the strip naming them at the top.
//   HOLDNET=<s>    the Warden's net that far after the throw (0.25: flying, 0.8: wrapped round you, 1.3: the fade).
// e.g. HOLDCELL=6 HOLDCELLS=2 Tools/Preview/shot.sh cell hud 10 1 2
static partial class Shot {
  static void HoldingCellScene() {
    string cs = Environment.GetEnvironmentVariable("HOLDCELL"), ls = Environment.GetEnvironmentVariable("HOLDLOCK"), ns = Environment.GetEnvironmentVariable("HOLDNET");
    if (cs == null && ls == null && ns == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var jail = typeof(Game).GetMethod("JailPlayer", flags);
    foreach (var e in gm.Enemies.Values) if (e.Type != EnemyType.Warden) e.Hp = 0f;
    for (int i = 0; i < 2; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Orbs.Clear(); gm.Coins.Clear();
    Player friend = null;
    foreach (var q in gm.Players.Values) if (q != me) friend = q;
    var w = gm.TheWarden();
    if (w == null) {
      w = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Warden, new V2(800f, 330f), 0f, 0, false });
      w.SpawnT = 0f; w.Spawning = false;
    }
    w.Pos = w.Disp = new V2(820f, 330f);
    Step(host);
    var ws = w.Warden;
    if (ws != null) { ws.Turn = WardenTurn.Light; ws.LightMode = WardenLight.Follow; ws.LightT = 3f; ws.TurnT = 6f; }
    Func<V2, float, Enemy> guard = (at, face) => {
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Guard, at, 0f, 0, false });
      e.SpawnT = 0f; e.Spawning = false; e.Disp = e.Pos; e.Facing = face; e.FireT = 99f; return e;
    };
    if (cs != null) {
      float age = float.Parse(cs, System.Globalization.CultureInfo.InvariantCulture);
      jail.Invoke(gm, new object[] { me, new V2(640f, 600f) });
      var cell = gm.CellOf(me);
      cell.Age = age; cell.Left = Math.Max(1f, Game.CellTime - age - 3f); cell.Skills = 3; cell.LockHits = 2;
      V2 c = Game.CellCentre(cell.Slot);
      me.Pos = me.Disp = c + new V2(30f, 60f); me.Aim = -0.6f; me.WarpT = 0f;
      if (age >= Game.CellFadeIn) {
        V2[] spots = { c + new V2(-150f, -40f), c + new V2(140f, -110f), c + new V2(-60f, 170f), c + new V2(190f, 120f), c + new V2(0f, -Game.CellHalf + 26f) };
        for (int k = 0; k < spots.Length; k++) {
          var g = guard(spots[k], (me.Pos - spots[k]).Angle);
          if (k == 1) { g.LarderStage = 1; g.LarderT = 0.15f; }  // winding up its baton
          if (k == 4) { g.SpawnT = 0.2f; g.Spawning = true; }   // squeezing in through the top gap
        }
      }
      if (Environment.GetEnvironmentVariable("HOLDCELLS") != null && friend != null) {
        jail.Invoke(gm, new object[] { friend, new V2(980f, 640f) });
        var fc = gm.CellOf(friend);
        fc.Age = age + 4f; fc.Left = Math.Max(1f, Game.CellTime - age - 9f);
        V2 c2 = Game.CellCentre(fc.Slot);
        friend.Pos = friend.Disp = c2 + new V2(-40f, 20f); friend.Aim = 0.4f; friend.WarpT = 0f;
        foreach (var at in new[] { c2 + new V2(80f, 60f), c2 + new V2(-160f, -120f), c2 + new V2(110f, -150f) }) guard(at, (friend.Pos - at).Angle);
      } else if (friend != null) {
        friend.Pos = friend.Disp = new V2(1150f, 640f);
      }
      typeof(ArenaApp).GetField("camCell", f2).SetValue(app, true);
      typeof(ArenaApp).GetField("camZoom", f2).SetValue(app, 1f);
      typeof(ArenaApp).GetField("camFocus", f2).SetValue(app, typeof(ArenaApp).GetMethod("CellViewFocus", f2).Invoke(app, new object[] { me }));
    }
    if (ls != null && friend != null) {
      jail.Invoke(gm, new object[] { friend, new V2(980f, 620f) });
      var fc = gm.CellOf(friend);
      fc.Age = 8f; fc.Left = 17.4f; fc.LockHits = 9;
      me.Pos = me.Disp = new V2(700f, 700f); me.WarpT = 0f;
      me.Aim = (fc.At - me.Pos).Angle;
      var sb = typeof(Game).GetMethod("SpawnBullet", flags);
      V2 dir = (fc.At - me.Pos).Normalized();
      for (int k = 0; k < 3; k++) sb.Invoke(gm, new object[] { Defs.TankShot, me.Pos + dir * (40f + 80f * k), dir * 600f, 0f, 1f, 10f, me.Id, false, 0f });
    }
    if (ns != null && ws != null) {
      float t = float.Parse(ns, System.Globalization.CultureInfo.InvariantCulture);
      me.Pos = me.Disp = new V2(620f, 640f); me.Aim = -0.4f;
      ws.LightMode = WardenLight.Net; ws.NetId = me.Id; ws.NetT = t; ws.NetFrom = w.Pos;
      ws.Light = ws.LightDisp = me.Pos;
      if (friend != null) friend.Pos = friend.Disp = new V2(1150f, 640f);
    }
  }
}
