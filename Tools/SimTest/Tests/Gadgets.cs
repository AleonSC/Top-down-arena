using System;
using System.Linq;
using Arena;

// Gadgets (Core/Gadgets.cs): the Magnet lies on the floor, a player walks over it, switches it on (every coin on the map flies to
// them), throws it to a teammate who catches it with the time it had left, switches it on over the network until it's used up;
// going down drops it.
static partial class Program
{
    static void GadgetTest(float dt)
    {
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Action Quiet = () =>
        {
            foreach (var e in g.Enemies.Values) e.Hp = 0f;
            foreach (var q in g.Bullets.Values.Where(q => Defs.IsEnemyShot(q.Kind)).ToList()) g.Bullets.Remove(q.Id);
            foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; }
        };
        V2 a = new V2(500f, 450f), b = new V2(800f, 450f);
        Action Put = () => { g.Players[1].Pos = g.Players[1].Disp = a; cg.Players[2].Pos = cg.Players[2].Disp = b; g.Players[2].Pos = b; };
        Put();
        Run(s, dt, 0.4f, null, Quiet);

        // On the floor: both screens see it; walking over it picks it up.
        var mag = g.SpawnGadget(GadgetId.Magnet, new V2(500f, 650f), Game.MagnetTime);
        Run(s, dt, 0.4f, null, () => { Quiet(); Put(); });
        Check(cg.Gadgets.Count == 1 && cg.Gadgets.Values.First().Kind == GadgetId.Magnet && g.Players[1].Gadget == GadgetId.None, "a Magnet on the floor shows on both screens and nobody holds it yet");
        a = new V2(500f, 640f);
        Run(s, dt, 0.4f, null, () => { Quiet(); Put(); });
        Check(g.Gadgets.Count == 0 && cg.Gadgets.Count == 0 && g.Players[1].Gadget == GadgetId.Magnet && cg.Players[1].Gadget == GadgetId.Magnet && !g.Players[1].GadgetOn,
            "walking over it picks it up (both screens), switched off");

        // Switched on: coins far out of the 90 px reach fly in.
        g.Players[1].Gold = 0;
        for (int i = 0; i < 6; i++)
            g.Coins[5000 + i] = new Coin { Id = 5000 + i, Value = 5, Pos = new V2(1300f, 150f + i * 40f), Disp = new V2(1300f, 150f + i * 40f) };
        Run(s, dt, 0.5f, null, () => { Quiet(); Put(); });
        Check(g.Coins.Count == 6 && g.Players[1].Gold == 0, "switched off, far coins stay put");
        g.UseGadget();
        Run(s, dt, 3f, () => g.Coins.Count == 0, () => { Quiet(); Put(); });
        float left = g.Players[1].GadgetLeft;
        Check(g.Coins.Count == 0 && g.Players[1].Gold == 30 && g.Players[1].GadgetOn && cg.Players[1].GadgetOn && left < Game.MagnetTime && left > Game.MagnetTime - 3.5f,
            "switched on, every coin on the map flies to the holder (" + g.Players[1].Gold + "/" + g.Players[2].Gold + " gold, " + g.Coins.Count + " coins, " + left.ToString("0.0") + " s left)");
        g.UseGadget();
        Run(s, dt, 0.5f, null, () => { Quiet(); Put(); });
        left = g.Players[1].GadgetLeft;
        Check(!g.Players[1].GadgetOn && !cg.Players[1].GadgetOn, "a second tap switches it off, saving the rest");
        Run(s, dt, 0.5f, null, () => { Quiet(); Put(); });
        Check(Math.Abs(g.Players[1].GadgetLeft - left) < 0.001f, "switched off, its time doesn't run");

        // Thrown to the teammate: they catch it with the time it had left.
        g.ThrowGadget(b);
        Run(s, dt, 0.05f, null, () => { Quiet(); Put(); });
        Check(g.Players[1].Gadget == GadgetId.None && g.Gadgets.Count == 1 && g.Gadgets.Values.First().Flying, "thrown: out of the hand and in the air");
        Run(s, dt, 1f, () => g.Players[2].Gadget == GadgetId.Magnet, () => { Quiet(); Put(); });
        Check(g.Players[2].Gadget == GadgetId.Magnet && Math.Abs(g.Players[2].GadgetLeft - left) < 0.001f && g.Gadgets.Count == 0, "the teammate catches it with the time it had left");
        Run(s, dt, 0.3f, null, () => { Quiet(); Put(); });
        Check(cg.Players[2].Gadget == GadgetId.Magnet && cg.Me.Gadget == GadgetId.Magnet, "...and their own screen knows they hold it");

        // The client switches it on over the network and uses it up.
        cg.UseGadget();
        Run(s, dt, 0.5f, null, () => { Quiet(); Put(); });
        Check(g.Players[2].GadgetOn, "a client switches it on (Msg.Gadget)");
        Run(s, dt, Game.MagnetTime, () => g.Players[2].Gadget == GadgetId.None, () => { Quiet(); Put(); });
        Run(s, dt, 0.3f, null, () => { Quiet(); Put(); });
        Check(g.Players[2].Gadget == GadgetId.None && cg.Players[2].Gadget == GadgetId.None && g.Gadgets.Count == 0, "used up, it's gone");

        // Throwing past the range lands at the range; going down drops it.
        g.Players[1].Gadget = GadgetId.Magnet; g.Players[1].GadgetLeft = 4f;
        g.ThrowGadget(a + new V2(0f, -2000f));
        Run(s, dt, 1f, null, () => { Quiet(); Put(); });
        var land = g.Gadgets.Values.FirstOrDefault();
        Check(land != null && !land.Flying && Math.Abs(land.Pos.DistanceTo(a) - Game.GadgetThrowRange) < 2f && g.Players[1].Gadget == GadgetId.None, "a long throw lands at the throw range, and the thrower doesn't snatch it back");
        g.Gadgets.Clear();
        g.Players[2].Gadget = GadgetId.Magnet; g.Players[2].GadgetLeft = 5f; g.Players[2].GadgetOn = true;
        g.DevSetGod(2, false);
        g.Players[2].Hp = 0f; g.Players[2].Downed = true;
        Run(s, dt, 0.3f, null, Quiet);
        Check(g.Players[2].Gadget == GadgetId.None && g.Gadgets.Count == 1 && g.Gadgets.Values.First().Left > 4.5f, "going down drops it at your feet, switched off, with its time");

        // Drops: an Expanding arena kill drops one now and then, never more than one per player.
        int max = 0;
        var drop = typeof(Game).GetMethod("DropGadget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var e0 = g.Enemies.Values.FirstOrDefault();
        if (e0 == null)
        {
            var spawn = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            e0 = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(900f, 300f), 0f, 0, false });
        }
        g.Gadgets.Clear();
        for (int i = 0; i < 2000; i++)
        {
            drop.Invoke(g, new object[] { e0 });
            max = Math.Max(max, g.Gadgets.Count);
        }
        Check(max >= 1 && max <= g.Players.Count * 2, "kills drop gadgets now and then, at most one of each kind per player (" + max + ")");
        Check(g.Gadgets.Values.Any(x => x.Kind == GadgetId.Magnet) && g.Gadgets.Values.Any(x => x.Kind == GadgetId.EnergyDrink), "both the Magnet and the Energy Drink drop from kills");

        // The bag: a second gadget goes in the bag, the wheel's pick swaps it into the hand (over the network too), a used-up one is replaced from the bag.
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        g.Gadgets.Clear();
        var p2 = g.Players[2];
        p2.Downed = false; p2.Hp = p2.MaxHp;
        p2.Gadget = GadgetId.None; p2.GadgetOn = false;
        for (int i = 0; i < p2.BagKind.Length; i++) p2.BagKind[i] = GadgetId.None;
        var give = typeof(Game).GetMethod("GiveGadget", bf);
        Check((bool)give.Invoke(g, new object[] { p2, GadgetId.Magnet, 20f }) && p2.Gadget == GadgetId.Magnet && p2.GadgetLeft == 20f, "the first gadget goes in the hand");
        Check((bool)give.Invoke(g, new object[] { p2, GadgetId.Recall, Game.RecallChannel }) && p2.BagKind[0] == GadgetId.Recall, "the second goes in the bag");
        for (int i = 0; i < 2; i++) give.Invoke(g, new object[] { p2, GadgetId.Recall, Game.RecallChannel });
        give.Invoke(g, new object[] { p2, GadgetId.Magnet, 7f });
        Check(!(bool)give.Invoke(g, new object[] { p2, GadgetId.Magnet, 1f }) && !Game.CanHoldGadget(p2), "a full hand and bag takes no more");
        Run(s, dt, 0.5f, null, () => { Quiet(); Put(); });
        Check(cg.Players[2].BagKind[0] == GadgetId.Recall && cg.Players[2].BagKind[3] == GadgetId.Magnet && Math.Abs(cg.Players[2].BagLeft[3] - 7f) < 0.01f, "the bag reaches the client's screen");
        cg.EquipGadget(3);
        Run(s, dt, 0.5f, null, () => { Quiet(); Put(); });
        Check(p2.Gadget == GadgetId.Magnet && Math.Abs(p2.GadgetLeft - 7f) < 0.01f && p2.BagKind[3] == GadgetId.Magnet && Math.Abs(p2.BagLeft[3] - 20f) < 0.01f, "a client swaps a bag slot into the hand (Msg.Gadget 2), the old one takes its slot with its time");
        p2.GadgetOn = true; p2.GadgetLeft = 0.05f;
        Run(s, dt, 0.3f, null, () => { Quiet(); Put(); });
        Check(p2.Gadget == GadgetId.Recall && p2.BagKind[0] == GadgetId.None && !p2.GadgetOn, "a used-up gadget is replaced from the bag");

        // Recall: refused at home; a hit breaks the channel; finishing it sets you down in the hub and uses it up.
        p2.GadgetLeft = Game.RecallChannel;
        cg.UseGadget();
        Run(s, dt, 0.3f, null, () => { Quiet(); Put(); });
        Check(!p2.GadgetOn, "Recall won't switch on in the hub");
        p2.GadgetOn = true; p2.GadgetLeft = 2f;
        typeof(Game).GetMethod("Damage", bf).Invoke(g, new object[] { p2, 1f, "Test" });
        Check(!p2.GadgetOn && p2.GadgetLeft == Game.RecallChannel, "a hit breaks the Recall channel and restarts the 3 s");
        p2.Hp = p2.MaxHp;
        p2.GadgetOn = true; p2.GadgetLeft = 0.05f;
        g.DevSetGod(2, true);
        int bagBefore = p2.BagKind.Count(k => k != GadgetId.None);
        Run(s, dt, 0.3f, null, () => { Quiet(); });
        Check(p2.Gadget != GadgetId.Recall || p2.BagKind.Count(k => k != GadgetId.None) < bagBefore, "a finished Recall is used up");
        Check(p2.Pos.DistanceTo(new V2(Game.Width / 2f, Game.Height / 2f)) < 160f, "and sets you down by the hub's middle");

        // The Energy Drink: switched on, a big speed boost (on the client's screen too) that runs down like the Magnet; off, normal speed.
        p2.Gadget = GadgetId.EnergyDrink; p2.GadgetLeft = Game.EnergyTime; p2.GadgetOn = false;
        float baseSpeed = p2.Speed;
        cg.UseGadget();
        Run(s, dt, 0.5f, null, () => { Quiet(); Put(); });
        Check(p2.GadgetOn && Math.Abs(p2.Speed - baseSpeed * Game.EnergySpeed) < 0.01f && p2.GadgetLeft < Game.EnergyTime, "the Energy Drink switched on: " + Game.EnergySpeed + "x move speed, its minute running down");
        Check(cg.Players[2].GadgetOn && Math.Abs(cg.Players[2].Speed - p2.Speed) < 0.01f, "...and the client moves at the same speed");
        cg.UseGadget();
        Run(s, dt, 0.3f, null, () => { Quiet(); Put(); });
        Check(!p2.GadgetOn && Math.Abs(p2.Speed - baseSpeed) < 0.01f, "switched off: normal speed, the rest saved");
        p2.GadgetOn = true; p2.GadgetLeft = 0.05f;
        Run(s, dt, 0.3f, null, () => { Quiet(); Put(); });
        Check(p2.Gadget != GadgetId.EnergyDrink && Math.Abs(p2.Speed - baseSpeed) < 0.01f, "a finished Energy Drink is used up");

        // Gadget chests: one each, into the hand or the bag; a full one spills at your feet (your own copy).
        foreach (var q in g.Players.Values)
        {
            q.Gadget = GadgetId.None;
            for (int i = 0; i < q.BagKind.Length; i++) q.BagKind[i] = GadgetId.None;
        }
        g.Gadgets.Clear();
        var chestGive = typeof(Game).GetMethod("GiveChestGadget", bf);
        chestGive.Invoke(g, new object[] { g.Players[1], a });
        Check(g.Players[1].Gadget != GadgetId.None && g.Gadgets.Count == 0, "a Gadget chest's gadget goes in your hand");
        for (int i = 0; i < 12; i++) chestGive.Invoke(g, new object[] { g.Players[1], a });
        Check(g.Players[1].BagKind.All(k => k != GadgetId.None) && g.Gadgets.Count > 0 && g.Gadgets.Values.All(x => x.Owner == 1), "with the hand and bag full the rest spills as your own copies");

        // The F1 dock's Spawn-a-gadget-or-relic sheet (Core/DevTools.cs): a full gadget at the player's feet as their own copy; a relic straight into the slot.
        g.Gadgets.Clear();
        Check(!cg.DevSpawnGadget(2, GadgetId.Recall) && !cg.DevSetRelic(2, RelicId.StoneHeart), "only the host can spawn a gadget or set a relic");
        Check(g.DevSpawnGadget(2, GadgetId.EnergyDrink) && g.Gadgets.Count == 1 && g.Gadgets.Values.First().Owner == 2
            && g.Gadgets.Values.First().Kind == GadgetId.EnergyDrink && Math.Abs(g.Gadgets.Values.First().Left - Game.EnergyTime) < 0.01f, "the dev spawn drops a full gadget, the player's own copy");
        Check(!g.DevSpawnGadget(2, GadgetId.None), "no gadget for None");
        float hpBefore = g.Players[2].MaxHp;
        Check(g.DevSetRelic(2, RelicId.StoneHeart) && g.Players[2].Relic == RelicId.StoneHeart && g.Players[2].MaxHp > hpBefore, "the dev relic goes in the slot and its stats apply");
        Check(!g.DevSetRelic(2, RelicId.StoneHeart), "setting the relic held again does nothing");
        Run(s, dt, 0.3f, null, () => { Quiet(); Put(); });
        Check(cg.Players[2].Relic == RelicId.StoneHeart && cg.Gadgets.Values.Any(x => x.Kind == GadgetId.EnergyDrink), "the client sees the dev relic and gadget");
        Check(g.DevSetRelic(2, RelicId.None) && g.Players[2].Relic == RelicId.None && Math.Abs(g.Players[2].MaxHp - hpBefore) < 0.01f, "removing the relic takes its stats away");
        client.Leave(""); host.Leave("");
    }
}
