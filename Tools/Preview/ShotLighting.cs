using System;
using System.Reflection;
using Arena;

// LIGHTING=1: dynamic lighting and shadows (Game/LightingLook.cs) on the Pillar Ring: a Spinner in the middle throwing a
// ring of shots out past the eight pillars (their shadows fan out), Grunts round you, your shots, a crate and a barrel,
// a blast going off by a pillar and a Flame Wall in the corner. LIGHTING=2: the same moment a little later. LIGHTS=0 the
// lighting off. Use with mode hud, e.g. LIGHTING=1 shot.sh x hud 3 1 1.
static partial class Shot {
  static void LightingScene() {
    string ls = Environment.GetEnvironmentVariable("LIGHTING");
    if (ls == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var addZone = typeof(Game).GetMethod("AddZone", flags);
    var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    gm.Enemies.Clear(); gm.Props.Clear();
    gm.RoomsUp = false; gm.DevSetLayout(LayoutId.PillarRing); gm.LayoutAge = 99f;
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 home = new V2(420f, 450f), mid = new V2(800f, 450f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(330f, 300f);
    me.Pos = me.Disp = home; me.Aim = 0f;
    spawn.Invoke(gm, new object[] { EnemyType.Spinner, mid, 0f, 0, false });
    foreach (var at in new[] { new V2(560f, 330f), new V2(590f, 560f), new V2(1060f, 250f), new V2(1120f, 640f) })
      spawn.Invoke(gm, new object[] { EnemyType.Grunt, at, 0f, 0, false });
    foreach (var e in gm.Enemies.Values) { e.Spawning = false; e.SpawnT = 0f; e.Disp = e.Pos; }
    gm.Props[9001] = new Prop { Id = 9001, Kind = PropKind.Crate, Pos = new V2(390f, 250f) };
    gm.Props[9002] = new Prop { Id = 9002, Kind = PropKind.Barrel, Pos = new V2(1250f, 420f) };
    gm.Props[9003] = new Prop { Id = 9003, Kind = PropKind.Chest, Pos = new V2(1300f, 220f) };
    int ring = ls == "2" ? 2 : 1;
    for (int r = 0; r < ring; r++)
      for (int k = 0; k < 16; k++) {  // the Spinner's ring, out past the pillars
        float a = MathX.Tau * (k + 0.5f * r) / 16f, d = 70f + 150f * (ring - r);
        shoot.Invoke(gm, new object[] { Defs.BigShot, mid + V2.FromAngle(a) * d, V2.FromAngle(a) * 200f, 0f, 9f, 8f, -1, false, 0f });
      }
    for (int k = 0; k < 5; k++)  // your stream at the nearest Grunt
      shoot.Invoke(gm, new object[] { Defs.DpsShot, home + new V2(40f + 34f * k, -16f * k), new V2(700f, -320f), 0f, 2f, 10f, me.Id, false, 0f });
    gm.Effects.Add(new Effect { Kind = FxKind.Boom, Pos = new V2(1150f, 330f), Radius = 70f, Color = new Rgba(1f, 0.6f, 0.2f), T = 0.08f, Dur = 0.6f });
    addZone.Invoke(gm, new object[] { ZoneKind.FlameWall, me, new V2(250f, 200f), 80f, 5f });
    foreach (var pr in gm.Props.Values) pr.Age = 5f;
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
  }
}
