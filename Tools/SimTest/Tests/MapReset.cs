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
        var made = new RunSave { Id = "m1", Wave = 12, LongRun = true, HasExpansion = true, ExpandSeed = 77, FloatCycle = 2, MapNumber = 3, KeepsakePending = true };
        made.Seats.Add(new SeatSave { Name = "A", Vectors = 30, VectorCap = 70, MapSpent = 44 });
        var back = RunSave.Parse(made.ToText(), "m1");
        var old = RunSave.Parse(made.ToText().Replace("expand 77 2 3 1 10 0", "expand 77 2").Replace("vectors 30 70 44", "vectors 30 70"), "m0");
        Check(back != null && back.MapNumber == 3 && back.Seats[0].MapSpent == 44 && back.Seats[0].Vectors == 30 && back.KeepsakePending, "a save keeps the map number, what each seat spent on it and a keepsake vote left open");
        Check(old != null && old.MapNumber == 1 && old.Seats[0].MapSpent == 0 && old.Seats[0].Vectors == 30 && !old.KeepsakePending, "an older save is the first map, nothing spent, no vote");

        // A kept room: its boxes, its tiles, and rebuilt whole against a fresh core with its kind, owner and look.
        {
            var core = Expansion.GridCore();
            var map = new List<ExpansionRoom>(core);
            string why = null;
            var spot = Expansion.PlacementsFor(map, PieceShape.TetL).First(o => o.Side == 3);
            var ell = Expansion.PieceShapeRooms(map, PieceShape.TetL, spot.Orient, spot.X, spot.Y, out why);
            Check(ell != null, "an L of four cells east of the core (" + why + ")");
            for (int i = 0; i < ell.Count; i++) { var q = ell[i]; q.Kind = RoomKind.Shop; q.Owner = 2; q.Surprise = 3; ell[i] = q; }
            int ellAt = map.Count;
            map.AddRange(ell);
            var mono = Expansion.PlacementsFor(map, PieceShape.Mono).First(o => o.Side == 1);
            int monoAt = map.Count;
            map.AddRange(mono.Rooms);
            Check(Expansion.KeepsakeChoices(map).SequenceEqual(new[] { ellAt, monoAt }) && Expansion.RoomGroup(map, ellAt).Count == ell.Count && Expansion.RoomGroup(map, monoAt).Count == 1,
                "the choices are each built room's first box (never the core); the L's boxes belong to it");
            Check(Expansion.RoomTiles(map, ellAt) == 144 && Expansion.RoomTiles(map, monoAt) == 36 && Expansion.BiggestRoom(map, Expansion.KeepsakeChoices(map)) == ellAt, "a room's tiles count all its boxes");
            var kept = Expansion.PlaceKeepsake(map, ellAt, core);
            int tiles = kept.Sum(q => q.W / 100 * (q.H / 100));
            bool clear = kept.All(q => core.All(c => !(q.X < c.X1 && c.X < q.X1 && q.Y < c.Y1 && c.Y < q.Y1)));
            bool lattice = kept.All(q => (q.X - Expansion.GridX0) % Expansion.GridCell == 0 && (q.Y - Expansion.GridY0) % Expansion.GridCell == 0);
            bool touches = Expansion.PlacementsFor(core, PieceShape.Mono).Any(o => kept.Any(q => q.X <= o.X && o.X < q.X1 && q.Y <= o.Y && o.Y < q.Y1));
            Check(kept.Count == ell.Count && tiles == 144 && clear && lattice && touches && kept.All(q => q.Kind == RoomKind.Shop && q.Owner == 2 && q.Surprise == 3 && q.Price == 0 && !q.Floating),
                "kept, the L is rebuilt whole on the lattice against a fresh core: " + tiles + " tiles, the same kind, owner and look, free");
            var floater = map[monoAt]; floater.Floating = true; map[monoAt] = floater;
            Check(!Expansion.KeepsakeChoices(map).Contains(monoAt), "the game's floating rooms can't be kept");
        }

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
            Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
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

            // The team's map before the boss dies: the core and two rooms (a domino, then a single), a node, a kit, a coin left in a room, vectors spent.
            g.Expanded.Clear(); g.Nodes.Clear();
            g.Expanded.AddRange(Expansion.GridCore());
            string why = null;
            var dom = Expansion.PlacementsFor(g.Expanded, PieceShape.Domino).First(o => o.Side == 3);
            var room = Expansion.PieceShapeRooms(g.Expanded, PieceShape.Domino, dom.Orient, dom.X, dom.Y, out why);
            Check(room != null, "a domino east of the core (" + why + ")");
            g.Expanded.AddRange(room);
            var southOpt = Expansion.PlacementsFor(g.Expanded, PieceShape.Mono).First(o => o.Side == 1);
            g.Expanded.AddRange(southOpt.Rooms);
            var roomBoxes = new List<ExpansionRoom>(g.Expanded);
            int domAt = 4, monoAt = 4 + room.Count;  // (the two rooms' first boxes: what the keepsake vote names)
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
            Run(s, dt, 1f, () => cg.Expanded.Count == roomBoxes.Count, quiet);
            Check(cg.Expanded.Count == roomBoxes.Count, "the client has the rooms");

            var coreBefore = g.CoreLayout;
            // The boss dies: the keepsake vote opens on every game and holds the break.
            killBoss = true;
            Run(s, dt, 10f, () => g.State == MatchState.Intermission, clear);
            Run(s, dt, 1f, () => cg.KeepsakeOpen, noPicks);
            Check(g.KeepsakeOpen && cg.KeepsakeOpen && g.MapNumber == 1 && cg.KeepsakeChoices.Count == 2 && !g.ExpandOpen, "the boss's death opens the keepsake vote on every game (the build round waits)");
            foreach (var p in g.Players.Values) p.Ready = true;
            Run(s, dt, 4f, () => g.State != MatchState.Intermission, noPicks);
            Check(g.State == MatchState.Intermission && g.KeepsakeOpen && cg.KeepsakeLeft < Game.KeepsakeTime - 2f && cg.KeepsakeLeft > 0f, "the break waits for the vote, its clock running on the client (" + cg.KeepsakeLeft.ToString("0.0") + " s)");
            client.Game.CastKeepsakeVote(domAt);
            Run(s, dt, 1f, () => g.KeepsakeVotes.Count == 1, noPicks);
            Check(g.KeepsakeVotes.TryGetValue(2, out int cv) && cv == domAt && g.KeepsakeOpen, "the client's vote reaches the host; the vote stays open for the host");
            g.CastKeepsakeVote(monoAt);  // a tie: the host's pick, the smaller room
            Run(s, dt, 1f, () => cg.MapNumber == 2, noPicks);
            int cap = a.VectorCap + Game.VectorBossBonus;
            Console.WriteLine("  after the boss: map {0} (client {1}), rooms {2}/{3}, nodes {4}, kits {5}, vectors {6} {7} (refill {8}), refund {9}",
                g.MapNumber, cg.MapNumber, g.Expanded.Count, cg.Expanded.Count, g.Nodes.Count, g.RoomTokens.Count, a.Vectors, b.Vectors, cap, g.MapRefund);
            Check(g.MapNumber == 2 && cg.MapNumber == 2 && cg.MapResetAge < 2f, "the boss's death starts map 2 on every game");
            Check(coreBefore != CoreLayoutId.None && g.CoreLayout != coreBefore && cg.CoreLayout == g.CoreLayout, "a new faction's map gets a new core layout on every game (" + CoreLayouts.Name(coreBefore) + " to " + CoreLayouts.Name(g.CoreLayout) + ")");
            Check(cg.FoldRooms.Count == roomBoxes.Count && g.FoldRooms.Count == roomBoxes.Count, "every game keeps the old map's boxes to fold away");
            Check(cg.MapRefunds.TryGetValue(2, out int cback) && cback == 7 && cg.MapRefunds.TryGetValue(1, out int hback) && hback == 20, "the client knows what each player got back");
            Check(g.MapsInRun == 10, "a Campaign plays ten maps (" + g.MapsInRun + ")");
            Check(!g.KeepsakeOpen && !cg.KeepsakeOpen && g.ExpandOpen && cg.ExpandOpen, "the vote is over once everyone voted; the build round opens on the new map");
            Check(g.Expanded.Count == 5 && cg.Expanded.Count == 5 && g.Expanded.Take(4).All(Expansion.IsGridCore) && g.Expanded[4].W * g.Expanded[4].H == 600 * 600 && g.KeptHead == 4 && cg.KeptHead == 4,
                "a tie keeps the host's pick: the single cell, rebuilt against the fresh core on every game");
            Check(g.Nodes.Count == 0 && cg.Nodes.Count == 0, "the old map's other rooms and nodes are gone on every game");
            Check(g.RoomTokens.Count >= 1 && g.RoomTokens.Exists(t => t.Kind == RoomKind.Shop) && cg.RoomTokens.Count == g.RoomTokens.Count, "the team's room kits are kept");
            Check(a.Vectors == cap + 20 && b.Vectors == cap + 7 && a.MapSpent == 0 && b.MapSpent == 0 && g.MapRefund == 27,
                "each player gets half their spending back on top of the boss refill (" + a.Vectors + ", " + b.Vectors + ")");
            Check(!g.Coins.ContainsKey(coinId) && g.Players.Values.Sum(p => p.Gold) >= gold0 + 7, "a coin left in an old room goes to a player");
            Check(g.Players.Values.All(p => p.Pos.X >= 0 && p.Pos.X <= Game.Width && p.Pos.Y >= 0 && p.Pos.Y <= Game.Height), "everyone is in the hub");

            // Building goes on: the fresh map takes a room, and its spending counts for the next reset.
            Check(g.RoomsUp, "the kept room is up at once");

            // The winner: most votes; no votes, the biggest room.
            g.Expanded.Clear(); g.Expanded.AddRange(roomBoxes); g.KeepsakeVotes.Clear();
            Check(g.KeepsakeWinner() == domAt, "no votes keep the biggest room");
            g.KeepsakeVotes[1] = domAt; g.KeepsakeVotes[2] = monoAt; g.KeepsakeVotes[3] = monoAt;
            Check(g.KeepsakeWinner() == monoAt, "the most votes win over the host's");
            g.KeepsakeVotes.Clear();
            pay.Invoke(g, new object[] { a, 10 });
            Check(a.MapSpent == 10, "spending on the new map counts again");
        }
        finally
        {
            Game.LongRunDefault = false;
        }
    }
}
