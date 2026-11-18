using System;
using Arena;

// CHESTLOOKS=1: the six chest kinds (Core ChestKind, Game/ChestLooks.cs): Gold, Vector, Upgrade, Relic, Room card, Buildables,
// closed on the top row and just opened on the bottom. Use with mode hud, ZOOM=2 CAMAT=640,470 (you stand off view).
static partial class Shot {
  static void ChestLooksScene() {
    if (Environment.GetEnvironmentVariable("CHESTLOOKS") == null) return;
    var gm = host.Game; gm.Bot = false;
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 850f);
    var kinds = new[] { ChestKind.Gold, ChestKind.Vector, ChestKind.Upgrade, ChestKind.Relic, ChestKind.Piece, ChestKind.Buildable };
    for (int row = 0; row < 2; row++)
      for (int i = 0; i < 6; i++) {
        var p = gm.AddProp(PropKind.Chest, new V2(420f + 88f * i, row == 0 ? 400f : 590f));
        p.Age = 5f; p.SeenAt = p.Pos;
        if (row == 1) { p.Open = true; p.OpenT = 0.7f; }
        p.Chest = kinds[i];
      }
    me.Pos = me.Disp = new V2(150f, 900f); me.Hp = me.MaxHp; me.InvulnT = 99f;
  }
}
