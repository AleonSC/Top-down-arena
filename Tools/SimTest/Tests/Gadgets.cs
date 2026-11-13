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
            foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; }
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
        Check(max >= 1 && max <= g.Players.Count, "kills drop a Magnet now and then, at most one per player (" + max + ")");
        client.Leave(""); host.Leave("");
    }
}
