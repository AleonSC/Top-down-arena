using System; using Arena;
// MOCKUP: REAPERLOOK=twilight|dual,<concept 0-3>: the Twin Reapers staged for the look rework (ReaperConceptLook.cs):
// twilight = Dusk and Dawn with their tether lit for the sweep; dual = the Dual Reaper firing its lasers, pillars and a hole.
static partial class Shot {
  static void ReaperLookScene(Player me) {
    var env = Environment.GetEnvironmentVariable("REAPERLOOK");
    if (env == null) return;
    var parts = env.Split(',');
    ArenaApp.ReaperConcept = parts.Length > 1 ? int.Parse(parts[1]) : 0;
    var g = host.Game; g.Enemies.Clear(); g.Bullets.Clear();
    var sp = typeof(Game).GetMethod("SpawnEnemyAt", f2);
    Func<EnemyType, V2, float, Enemy> put = (t, at, face) => { var e = (Enemy)sp.Invoke(g, new object[] { t, at, 0f, 0, false }); e.SpawnT = 0f; e.Spawning = false; e.Disp = e.Pos = at; e.Facing = face; return e; };
    me.Pos = me.Disp = new V2(800f, 640f);
    foreach (var o in g.Players.Values) if (o != me) o.Pos = o.Disp = new V2(-900f, -900f);
    var R = g.Reapers = new ReaperFight();
    if (parts[0] == "dual") {
      R.Stage = ReaperStage.Dual; R.Turn = ReaperTurn.Lasers; R.TurnT = 2f; R.Laser = 0.42f;
      put(EnemyType.DualReaper, new V2(800f, 400f), 0f);
      put(EnemyType.ReaperPillar, new V2(610f, 290f), 0f); put(EnemyType.ReaperPillar, new V2(1000f, 540f), 0f);
      R.Holes.Add(new V2(990f, 250f));
    } else {
      R.Stage = ReaperStage.Twilight; R.Turn = ReaperTurn.Sweep; R.TurnT = 1f;
      var a = put(EnemyType.ReaperOfDusk, new V2(600f, 420f), 0f); var b = put(EnemyType.ReaperOfDawn, new V2(1000f, 330f), 0f);
      a.Facing = (float)Math.Atan2(me.Pos.Y - a.Pos.Y, me.Pos.X - a.Pos.X); b.Facing = (float)Math.Atan2(me.Pos.Y - b.Pos.Y, me.Pos.X - b.Pos.X);
    }
  }
}
