using System;
using System.Reflection;
using Arena;

// GADGETS=wheel|floor|recall (mode hud): wheel = the gadget wheel open at the cursor (a Magnet in hand, a Recall and a Magnet in the bag) with the chip beside the keys;
// floor = a Magnet and a Recall lying on the floor; recall = you channelling a Recall (rings rising, the progress arc). Use ZOOM=2 CAMAT=<x,y> for the close views.
static partial class Shot {
  static void GadgetsScene() {
    string mode = Environment.GetEnvironmentVariable("GADGETS");
    if (mode == null) return;
    var gm = host.Game; gm.Bot = false;
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 850f);
    me.Hp = me.MaxHp; me.InvulnT = 99f;
    if (mode == "floor") {
      gm.SpawnGadget(GadgetId.Magnet, new V2(560f, 450f), Game.MagnetTime);
      gm.SpawnGadget(GadgetId.Recall, new V2(660f, 450f), Game.RecallChannel);
      me.Pos = me.Disp = new V2(150f, 900f);
      return;
    }
    me.Gadget = mode == "recall" ? GadgetId.Recall : GadgetId.Magnet;
    me.GadgetLeft = mode == "recall" ? 1.2f : 42f;
    me.GadgetOn = mode == "recall";
    me.BagKind[0] = GadgetId.Recall; me.BagLeft[0] = Game.RecallChannel;
    me.BagKind[2] = GadgetId.Magnet; me.BagLeft[2] = 18f;
  }

  // After the app exists: the wheel's state (what the key's hold would have set).
  static void GadgetsWheelScene() {
    if (Environment.GetEnvironmentVariable("GADGETS") != "wheel") return;
    var slots = (System.Collections.Generic.List<int>)typeof(ArenaApp).GetField("gadgetWheelSlots", f2).GetValue(app);
    slots.Clear(); slots.Add(-1); slots.Add(0); slots.Add(2);
    set("gadgetWheelOpen", true); set("gadgetWheelK", 1f); set("gadgetHover", 1);
    set("gadgetAnchor", new UnityEngine.Vector2(640f, 330f));
  }
}
