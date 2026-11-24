using System; using Arena;
// REAPERLOOK=twilight|bond|dual|chase|sundered: the Twin Reapers staged (ReaperBodyLook.cs, ReaperLook.cs):
// twilight = Dusk and Dawn with their chain tether lit for the sweep; bond = the tether dark and Dawn in its Soul Bond shell;
// dual = the Dual Reaper firing its lasers, two gravestone pillars and a hole; chase = it chasing, its swing about to come;
// sundered = Dusk's reap marked (scythe drawn back) and Dawn enraged mid-swing;
// fuse,<s> / split,<s> = that far into the fusion or the split (ReaperShiftLook.cs; CAMAT=800,450 ZOOM=1.3). Use with CAMAT=800,400 ZOOM=2 shot.sh x hud 6 1 1.
static partial class Shot {
  static void ReaperLookScene(Player me) {
    var scene = Environment.GetEnvironmentVariable("REAPERLOOK");
    if (scene == null) return;
    float when = scene.Contains(",") ? float.Parse(scene.Split(',')[1], System.Globalization.CultureInfo.InvariantCulture) : 0f; scene = scene.Split(',')[0];
    var g = host.Game; g.Enemies.Clear(); g.Bullets.Clear();
    var sp = typeof(Game).GetMethod("SpawnEnemyAt", f2);
    Func<EnemyType, V2, Enemy> put = (t, at) => { var e = (Enemy)sp.Invoke(g, new object[] { t, at, 0f, 0, false }); e.SpawnT = 0f; e.Spawning = false; e.Disp = e.Pos = at; e.Facing = (float)Math.Atan2(me.Pos.Y - at.Y, me.Pos.X - at.X); return e; };
    me.Pos = me.Disp = new V2(800f, 640f);
    foreach (var o in g.Players.Values) if (o != me) o.Pos = o.Disp = new V2(-900f, -900f);
    var R = g.Reapers = new ReaperFight();
    V2 mid = Game.ArenaMid;
    if (scene == "fuse" || scene == "split") me.Pos = me.Disp = new V2(1250f, 760f);
    if (scene == "fuse") {
      R.Stage = ReaperStage.Twilight; R.Trans = 1; R.TransT = when; R.Step = 0;
      if (when < Game.FuseMerge) {  // the spiral (as TickReaperChange places them)
        float f = 1f - (1f - when / Game.FuseMerge) * (1f - when / Game.FuseMerge) * (1f - when / Game.FuseMerge), r = 360f * (1f - f), ang = when * (2f + 6f * f);
        put(EnemyType.ReaperOfDusk, mid + new V2((float)Math.Cos(ang + Math.PI) * r, (float)Math.Sin(ang + Math.PI) * r * 0.7f));
        put(EnemyType.ReaperOfDawn, mid + new V2((float)Math.Cos(ang) * r, (float)Math.Sin(ang) * r * 0.7f));
      } else put(EnemyType.DualReaper, mid);
      return;
    }
    if (scene == "split") {
      R.Stage = ReaperStage.Dual; R.Trans = 2; R.TransT = when; R.Step = 0;
      if (when < Game.SplitBurst) put(EnemyType.DualReaper, mid);
      else { float d = Math.Min(300f, 20f + 700f * (when - Game.SplitBurst)); var a = put(EnemyType.ReaperOfDusk, mid + new V2(-d, 0f)); var b = put(EnemyType.ReaperOfDawn, mid + new V2(d, 0f)); a.Facing = MathX.Pi; b.Facing = 0f; }
      return;
    }
    if (scene == "dual" || scene == "chase") {
      R.Stage = ReaperStage.Dual;
      if (scene == "dual") { R.Turn = ReaperTurn.Lasers; R.TurnT = 2f; R.Laser = 0.42f; }
      else { R.Turn = ReaperTurn.Chase; R.TurnT = 2f; R.Heading = 1.2f; R.SwingT = 0.2f; }
      put(EnemyType.DualReaper, new V2(800f, 400f));
      put(EnemyType.ReaperPillar, new V2(610f, 290f)); put(EnemyType.ReaperPillar, new V2(1000f, 540f));
      R.Holes.Add(new V2(990f, 250f));
    } else if (scene == "sundered") {
      R.Stage = ReaperStage.Sundered; R.Turn = ReaperTurn.Reap;
      var a = put(EnemyType.ReaperOfDusk, new V2(560f, 380f)); var b = put(EnemyType.ReaperOfDawn, new V2(1000f, 360f));
      R.ReapA.Phase = 1; R.ReapA.A = a.Facing; R.ReapA.Len = 260f; R.ReapA.T = 0.3f;
      R.ReapB.Phase = 3; R.ReapB.A = b.Facing; R.ReapB.T = 0.12f; b.Phase = 1;
    } else {
      R.Stage = ReaperStage.Twilight;
      if (scene == "bond") R.Turn = ReaperTurn.Rest; else { R.Turn = ReaperTurn.Sweep; R.TurnT = 1f; }
      put(EnemyType.ReaperOfDusk, new V2(600f, 420f)); var b = put(EnemyType.ReaperOfDawn, new V2(1000f, 330f));
      if (scene == "bond") b.Phase = 2;
    }
  }
}
