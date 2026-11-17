using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Map reset (Docs/MapReset.md step 1, Core/MapReset.cs): in the Campaign the Expanding map goes back to the bare hub after
// every boss; room kits stay and each player gets half the vectors they spent on the old map back.
static partial class Program
{
    static void MapResetTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        Check(ModeDef.For(GameMode.Campaign).MapResets && !ModeDef.For(GameMode.Quick).MapResets && !ModeDef.For(GameMode.Survival).MapResets
              && !ModeDef.For(GameMode.Hunt).MapResets, "only the Campaign resets its map");
        Check(Game.RefundFor(0) == 0 && Game.RefundFor(41) == 20 && Game.RefundFor(100) == 50, "the refund is half the vectors spent, rounded down");

        // A save keeps the map number and each seat's spending; an older save is the first map with nothing spent.
        var made = new RunSave { Id = "m1", Wave = 12, LongRun = true, HasExpansion = true, ExpandSeed = 77, FloatCycle = 2, MapNumber = 3 };
        made.Seats.Add(new SeatSave { Name = "A", Vectors = 30, VectorCap = 70, MapSpent = 44 });
        var back = RunSave.Parse(made.ToText(), "m1");
        var old = RunSave.Parse(made.ToText().Replace("expand 77 2 3", "expand 77 2").Replace("vectors 30 70 44", "vectors 30 70"), "m0");
        Check(back != null && back.MapNumber == 3 && back.Seats[0].MapSpent == 44 && back.Seats[0].Vectors == 30, "a save keeps the map number and what each seat spent on it");
        Check(old != null && old.MapNumber == 1 && old.Seats[0].MapSpent == 0 && old.Seats[0].Vectors == 30, "an older save is the first map, nothing spent");

        Game.LongRunDefault = true;
        try
        {
            int port = nextPort++;
            var host = HostAs(new Session { AutoStart = 2, StartWave = 5 }, port, false, "Host", Role.Tank);
            var client = JoinAs(port, false, "Friend", Role.Dps);
            var s = new[] { host, client };
            WaitForGame(host, client, dt);
            var g = host.Game; var cg = client.Game;
            g.DevSetGod(1, true); g.DevSetGod(2, true);
            Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
            bool killBoss = false;
            Action clear = () =>
            {
                noPicks();
                if (!g.BossSlot) ((IList)typeof(Game).GetField("spawnQueue", flags).GetValue(g)).Clear();
                if (!g.BossSlot) ((IList)typeof(Game).GetField("squadQueue", flags).GetValue(g)).Clear();
                foreach (var e in g.Enemies.Values) if (killBoss || !e.Def.IsBoss) e.Hp = 0f;
            };
            Check(g.MapResetOn && cg.MapResetOn && g.MapNumber == 1 && cg.MapNumber == 1, "a Campaign starts on map 1 and resets its map");
            Run(s, dt, 200f, () => g.BossSlot, clear);
            Action quiet = () => { noPicks(); foreach (var e in g.Enemies.Values) if (!e.Def.IsBoss) e.Hp = 0f; };
            Run(s, dt, 30f, () => g.Enemies.Values.Any(e => e.Def.IsBoss && e.Hp > 0f), quiet);
            Check(g.BossSlot && g.State == MatchState.Fighting && g.Enemies.Values.Any(e => e.Def.IsBoss), "the first boss is up (track wave " + g.Wave + ")");

            // The team's map before the boss dies: a room, a node, a kit, a coin left in the room, vectors spent.
            g.Expanded.Clear(); g.Nodes.Clear();
            string why;
            var room = Expansion.Custom(g.Expanded, (int)Game.Width, 200, 1000, 500, out why);
            Check(room.HasValue, "a room east of the hub (" + why + ")");
            g.Expanded.Add(room.Value);
            g.Nodes.Add(new MapNode { X = (short)(Game.Width + 2000), Y = 450, Kind = NodeKind.Chest });
            g.RoomTokens.Clear();
            g.RoomTokens.Add(new RoomToken { Kind = RoomKind.Shop });
            typeof(Game).GetField("expWorld", flags).SetValue(g, null);
            var pay = typeof(Game).GetMethod("PayVectors", flags);
            var a = g.Players[1]; var b = g.Players[2];
            a.Vectors = b.Vectors = 60; a.MapSpent = b.MapSpent = 0;
            pay.Invoke(g, new object[] { a, 40 });
            pay.Invoke(g, new object[] { b, 15 });
            Check(a.Vectors == 20 && a.MapSpent == 40 && b.MapSpent == 15, "spending vectors counts towards the map's refund (" + a.MapSpent + ", " + b.MapSpent + ")");
            int coinId = 990001;
            g.Coins[coinId] = new Coin { Id = coinId, Value = 7, Pos = new V2(Game.Width + 500, 450), Disp = new V2(Game.Width + 500, 450) };
            int gold0 = g.Players.Values.Sum(p => p.Gold);
            typeof(Game).GetMethod("BroadcastExpansion", flags).Invoke(g, null);
            Run(s, dt, 1f, () => cg.Expanded.Count == 1, quiet);
            Check(cg.Expanded.Count == 1, "the client has the room");

            // The boss dies: a fresh map, the kit and half the spending back.
            killBoss = true;
            Run(s, dt, 10f, () => g.State == MatchState.Intermission, clear);
            Run(s, dt, 1f, () => cg.MapNumber == 2, noPicks);
            int cap = a.VectorCap + Game.VectorBossBonus;
            Console.WriteLine("  after the boss: map {0} (client {1}), rooms {2}/{3}, nodes {4}, kits {5}, vectors {6} {7} (refill {8}), refund {9}",
                g.MapNumber, cg.MapNumber, g.Expanded.Count, cg.Expanded.Count, g.Nodes.Count, g.RoomTokens.Count, a.Vectors, b.Vectors, cap, g.MapRefund);
            Check(g.MapNumber == 2 && cg.MapNumber == 2 && cg.MapResetAge < 2f, "the boss's death starts map 2 on every game");
            Check(g.Expanded.Count == 0 && cg.Expanded.Count == 0 && g.Nodes.Count == 0 && cg.Nodes.Count == 0, "the old map's rooms and nodes are gone on every game");
            Check(g.RoomTokens.Count >= 1 && g.RoomTokens.Exists(t => t.Kind == RoomKind.Shop) && cg.RoomTokens.Count == g.RoomTokens.Count, "the team's room kits are kept");
            Check(a.Vectors == cap + 20 && b.Vectors == cap + 7 && a.MapSpent == 0 && b.MapSpent == 0 && g.MapRefund == 27,
                "each player gets half their spending back on top of the boss refill (" + a.Vectors + ", " + b.Vectors + ")");
            Check(!g.Coins.ContainsKey(coinId) && g.Players.Values.Sum(p => p.Gold) >= gold0 + 7, "a coin left in an old room goes to a player");
            Check(g.Players.Values.All(p => p.Pos.X >= 0 && p.Pos.X <= Game.Width && p.Pos.Y >= 0 && p.Pos.Y <= Game.Height), "everyone is in the hub");

            // Building goes on: the fresh map takes a room, and its spending counts for the next reset.
            Check(!g.RoomsUp, "the bare hub has no rooms up");
            pay.Invoke(g, new object[] { a, 10 });
            Check(a.MapSpent == 10, "spending on the new map counts again");
        }
        finally
        {
            Game.LongRunDefault = false;
        }
    }
}
