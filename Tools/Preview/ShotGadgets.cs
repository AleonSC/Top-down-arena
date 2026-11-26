using System;
using System.Reflection;
using Arena;

// GADGETS=wheel|wheel2|bag|floor|vest|clouds|cloak|overclock|recall|energy|shield (mode hud): wheel2 = the wheel with the newer kinds (a Grenade in hand, x2); bag = the same, wheel shut (the chip's bag row);
// cloak / overclock = yours switched on; clouds = a Smoke Bomb's, a Gas Grenade's and an Incendiary's ground; shield = your Shield Generator's dome; wheel = the gadget wheel open at the cursor (a Magnet in hand, a Recall, an Energy Drink and a Magnet in the
// bag) with the chip beside the keys; floor = every kind lying on the floor (plus a part-used Energy Drink and a Grenade with one charge); recall = you channelling a Recall (rings rising,
// the progress arc); vest = Tactical Vest just popped (TacticalVest.cs): three drops round you on their gold fizzle arcs, one still in the air; energy = you running on an Energy Drink (speed lines, the time ring). Use ZOOM=2 CAMAT=<x,y> for the close views.
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
    if (mode == "vest") {  // Tactical Vest a moment after a press: two landed on their fizzle arcs, one landing, one long down
      me.Pos = me.Disp = new V2(600f, 450f);
      me.Slots[0].Id = AbilityId.TacticalVest; me.Slots[0].Timer = 17f;
      var kinds = new[] { GadgetId.Grenade, GadgetId.Incendiary, GadgetId.Emp, GadgetId.LandMine };
      var spots = new[] { new V2(670f, 420f), new V2(540f, 500f), new V2(600f, 375f), new V2(760f, 520f) };
      var left = new[] { 11f, 11f, 11.8f, 4f };
      for (int i = 0; i < 4; i++) {
        var g = gm.SpawnGadget(kinds[i], spots[i], Game.GadgetTime(kinds[i]));
        g.Owner = me.Id; g.Fizzle = left[i]; g.From = me.Pos;
        if (i == 2) { g.To = spots[i]; g.FlyT = 0.25f; g.Pos = g.Disp = V2.Lerp(me.Pos, g.To, 0.5f); }
      }
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
      : mode == "wheel2" || mode == "bag" ? GadgetId.Grenade : mode == "cloak" ? GadgetId.Cloak : mode == "overclock" ? GadgetId.OverclockChip : GadgetId.Magnet;
    me.GadgetLeft = mode == "recall" ? 1.2f : mode == "energy" ? 38f : mode == "shield" ? 12f : mode == "cloak" ? 9f : mode == "overclock" ? 21f : 42f;
    me.GadgetUses = Game.GadgetUses(me.Gadget);
    if (mode == "wheel2" || mode == "bag") me.GadgetUses = 2;
    me.GadgetOn = mode == "recall" || mode == "energy" || mode == "shield" || mode == "cloak" || mode == "overclock";
    if (mode == "cloak") me.VanishT = 0.2f;
    me.BagKind[0] = GadgetId.Lodestone; me.BagLeft[0] = Game.LodestoneChannel; me.BagUses[0] = 2;
    me.BagKind[1] = GadgetId.EnergyDrink; me.BagLeft[1] = Game.EnergyTime;
    if (mode == "wheel2" || mode == "bag") {  // the newer kinds in the bag (bag: the same, wheel shut, for the chip's bag row)
      me.BagKind[0] = GadgetId.ShieldGenerator; me.BagLeft[0] = Game.ShieldGenTime;
      me.BagKind[1] = GadgetId.Medkit; me.BagUses[1] = 2;
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
