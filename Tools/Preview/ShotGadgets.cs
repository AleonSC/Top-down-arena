using System;
using System.Reflection;
using Arena;

// GADGETS=wheel|wheel2|floor|clouds|recall|energy|shield|defib (mode hud): wheel2 = the wheel with the newer kinds (a Grenade in hand, x2);
// clouds = a Smoke Bomb's, a Gas Grenade's and an Incendiary's ground; shield = your Shield Generator's dome; defib = you downed, your Defib Kit charging; wheel = the gadget wheel open at the cursor (a Magnet in hand, a Recall, an Energy Drink and a Magnet in the
// bag) with the chip beside the keys; floor = every kind lying on the floor (plus a part-used Energy Drink and a Grenade with one charge); recall = you channelling a Recall (rings rising,
// the progress arc); energy = you running on an Energy Drink (speed lines, the time ring). Use ZOOM=2 CAMAT=<x,y> for the close views.
static partial class Shot {
  static void GadgetsScene() {
    string mode = Environment.GetEnvironmentVariable("GADGETS");
    if (mode == null) return;
    var gm = host.Game; gm.Bot = false;
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 850f);
    me.Hp = me.MaxHp; me.InvulnT = 99f; me.LevelFlash = 0f;
    if (mode == "clouds") {  // what the throwables leave: a Smoke Bomb's cloud, a Gas Grenade's, an Incendiary's burning ground
      var sm = gm.AddZone(ZoneKind.SmokeShell, me, new V2(470f, 450f), Game.SmokeBombRadius, Game.GlSmokeLand(200f) + Game.SmokeBombLife); sm.GuardFrac = 200f; sm.T = Game.SmokeBombLife - 1f;
      var gas = gm.AddZone(ZoneKind.ToxicGas, me, new V2(700f, 450f), Game.GadgetGasRadius, Game.TearGasFlight + Game.GadgetGasLife); gas.T = Game.GadgetGasLife - 1.5f;
      var fire = gm.AddZone(ZoneKind.FlameWall, me, new V2(920f, 450f), Game.GadgetFireRadius, Game.GadgetFireLife); fire.T = Game.GadgetFireLife - 1f; fire.BlockHp = 0f;
      me.Pos = me.Disp = new V2(150f, 900f);
      return;
    }
    if (mode == "floor") {
      for (int k = 0; k < (int)GadgetId.None; k++)  // every kind in a row, a part-used Energy Drink and a Grenade with one charge left after
        gm.SpawnGadget((GadgetId)k, new V2(470f + (k % 5) * 70f, 410f + (k / 5) * 80f), Game.GadgetTime((GadgetId)k));
      gm.SpawnGadget(GadgetId.EnergyDrink, new V2(820f, 490f), 25f);
      gm.SpawnGadget(GadgetId.Grenade, new V2(890f, 490f), 0f).Uses = 1;
      me.Pos = me.Disp = new V2(150f, 900f);
      return;
    }
    me.Gadget = mode == "recall" ? GadgetId.Lodestone : mode == "energy" ? GadgetId.EnergyDrink : mode == "shield" ? GadgetId.ShieldGenerator
      : mode == "defib" ? GadgetId.DefibKit : mode == "wheel2" ? GadgetId.Grenade : GadgetId.Magnet;
    me.GadgetLeft = mode == "recall" ? 1.2f : mode == "energy" ? 38f : mode == "shield" ? 12f : mode == "defib" ? 0.8f : 42f;
    me.GadgetUses = Game.GadgetUses(me.Gadget);
    if (mode == "wheel2") me.GadgetUses = 2;
    me.GadgetOn = mode == "recall" || mode == "energy" || mode == "shield" || mode == "defib";
    if (mode == "defib") { me.Downed = true; me.Hp = 0f; }
    me.BagKind[0] = GadgetId.Lodestone; me.BagLeft[0] = Game.LodestoneChannel; me.BagUses[0] = 2;
    me.BagKind[1] = GadgetId.EnergyDrink; me.BagLeft[1] = Game.EnergyTime;
    if (mode == "wheel2") {  // the newer kinds in the bag
      me.BagKind[0] = GadgetId.ShieldGenerator; me.BagLeft[0] = Game.ShieldGenTime;
      me.BagKind[1] = GadgetId.DefibKit; me.BagUses[1] = 1;
      me.BagKind[2] = GadgetId.SmokeBomb; me.BagUses[2] = 3;
      me.BagKind[3] = GadgetId.Incendiary; me.BagUses[3] = 3;
      return;
    }
    me.BagKind[2] = GadgetId.Magnet; me.BagLeft[2] = 18f;
  }

  // After the app exists: the wheel's state (what the key's hold would have set).
  static void GadgetsWheelScene() {
    if (Environment.GetEnvironmentVariable("GADGETS") == "energy") {  // (moving right: the speed lines trail off to the left)
      ((System.Collections.Generic.Dictionary<int, V2>)typeof(ArenaApp).GetField("energyLast", f2).GetValue(app))[me.Id] = me.Disp;
      ((System.Collections.Generic.Dictionary<int, V2>)typeof(ArenaApp).GetField("energyVel", f2).GetValue(app))[me.Id] = new V2(420f, -120f);
    }
    string wm = Environment.GetEnvironmentVariable("GADGETS");
    if (wm != "wheel" && wm != "wheel2") return;
    var slots = (System.Collections.Generic.List<int>)typeof(ArenaApp).GetField("gadgetWheelSlots", f2).GetValue(app);
    slots.Clear(); slots.Add(-1); slots.Add(0); slots.Add(1); slots.Add(2);
    if (wm == "wheel2") slots.Add(3);
    set("gadgetWheelOpen", true); set("gadgetWheelK", 1f); set("gadgetHover", 2);
    set("gadgetAnchor", new UnityEngine.Vector2(640f, 330f));
  }
}
