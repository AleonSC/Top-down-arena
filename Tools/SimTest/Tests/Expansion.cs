using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Expanding arena's rooms and offers (Expansion.cs, Docs/ExpandingArena.md step 2): the same offers from the same
// seed, rooms that never overlap or touch except through their own door, a connected map with open doors and steering
// spots either side of them, a size cap, and a mix of shapes and kinds.
static partial class Program
{
    static void ExpansionTest()
    {
        var none = new List<ExpansionRoom>();
        var a = Expansion.Offers(11, 5, none);
        var b = Expansion.Offers(11, 5, none);
        Check(a.Count == 3 && a.SequenceEqual(b), "the same seed, round and map give the same three offers");
        Check(Enumerable.Range(1, 40).Any(s => !Expansion.Offers(s, 5, none).SequenceEqual(a)) && !Expansion.Offers(11, 10, none).SequenceEqual(a), "other seeds and rounds give other offers");
        Check(a.All(o => o.Parent == 0 && o.Price > 0 && o.Kind != RoomKind.Shop), "the first offers all join the hub, cost something and none is a shop");

        var hubOnly = Expansion.Build(none);
        Check(hubOnly.Areas.Length == 1 && hubOnly.Doors.Length == 0 && hubOnly.WallsFor(false).Length == 4 && hubOnly.Max.X == Game.Width && hubOnly.Max.Y == Game.Height,
            "with nothing bought the world is the hub alone, walled in");

        int badFit = 0, badTree = 0, badDoor = 0, badWall = 0, badSteer = 0, badCap = 0, worlds = 0, offersSeen = 0;
        var shapes = new HashSet<string>(); var kinds = new HashSet<RoomKind>(); var floors = new HashSet<PathId>();
        int maxShops = 0; int prevPriceBad = 0;
        for (int seed = 1; seed <= 60; seed++)
        {
            var built = new List<ExpansionRoom>();
            var rng = new Random(seed);
            for (int round = 5; ; round += 5)
            {
                var offers = Expansion.Offers(seed, round, built);
                if (built.Count >= Expansion.MaxRooms) { badCap += offers.Count; break; }
                if (offers.Count == 0) break;
                offersSeen += offers.Count;
                foreach (var o in offers) { shapes.Add(o.Shape); kinds.Add(o.Kind); if (o.Kind == RoomKind.Floor) floors.Add(o.Floor); }
                var pick = offers[rng.Next(offers.Count)];
                if (built.Count > 0 && pick.Price <= Expansion.PriceFor(pick.Kind, pick.W, pick.H, 0)) prevPriceBad++;
                built.Add(pick);
            }
            worlds++;
            var w = Expansion.Build(built);
            maxShops = Math.Max(maxShops, built.Count(r => r.Kind == RoomKind.Shop));
            // no overlaps, and only the parent shares a wall
            for (int i = 0; i < w.Areas.Length; i++)
                for (int j = i + 1; j < w.Areas.Length; j++)
                {
                    var p = w.Areas[i]; var q = w.Areas[j];
                    bool overlap = p.Min.X < q.Max.X && q.Min.X < p.Max.X && p.Min.Y < q.Max.Y && q.Min.Y < p.Max.Y;
                    bool sharesSide = ((p.Max.X == q.Min.X || q.Max.X == p.Min.X) && Math.Min(p.Max.Y, q.Max.Y) > Math.Max(p.Min.Y, q.Min.Y))
                                   || ((p.Max.Y == q.Min.Y || q.Max.Y == p.Min.Y) && Math.Min(p.Max.X, q.Max.X) > Math.Max(p.Min.X, q.Min.X));
                    bool doored = w.Doors.Any(d => d.Joins(i) && d.Joins(j));
                    if (overlap || (sharesSide && !doored)) badFit++;
                }
            if (w.Max.X - w.Min.X > Expansion.MaxWidth || w.Max.Y - w.Min.Y > Expansion.MaxHeight) badCap++;
            // connected: every room reachable from the hub through doors (a tree: one door per room)
            var seen = new HashSet<int> { 0 }; var queue = new Queue<int>(); queue.Enqueue(0);
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                foreach (var d in w.Doors.Where(d => d.Joins(cur)))
                    if (seen.Add(d.Other(cur))) queue.Enqueue(d.Other(cur));
            }
            if (seen.Count != w.Areas.Length || w.Doors.Length != w.Areas.Length - 1) badTree++;
            // each door sits on both rooms' shared wall, DoorW inside it, and is open (no solid) in the walls and the floor plan
            var plan = w.With(LayoutId.Open);
            foreach (var d in w.Doors)
            {
                var pa = w.Areas[d.A]; var pb = w.Areas[d.B];
                bool onBoth = pa.Contains(d.Pos, 1f) && pb.Contains(d.Pos, 1f);
                float run = d.Vertical ? Math.Min(pa.Max.Y, pb.Max.Y) - Math.Max(pa.Min.Y, pb.Min.Y) : Math.Min(pa.Max.X, pb.Max.X) - Math.Max(pa.Min.X, pb.Min.X);
                if (!onBoth || run < Expansion.MinDoorRun || plan.Solids.Any(sd => Layouts.Inside(sd, d.Pos, 30f))) badDoor++;
                if (!plan.Waypoints.Any(wp => wp.DistanceTo(d.Pos + d.Across * Rooms.DoorPad) < 1f) || !plan.Waypoints.Any(wp => wp.DistanceTo(d.Pos - d.Across * Rooms.DoorPad) < 1f)) badSteer++;
            }
            // every room's centre is in the open and in its own area
            for (int i = 0; i < w.Areas.Length; i++)
                if (plan.Solids.Any(sd => Layouts.Inside(sd, w.Areas[i].Mid, 0f)) || w.AreaAt(w.Areas[i].Mid) != i) badWall++;
        }
        Check(offersSeen > 600, "offers keep coming until the cap (" + offersSeen + " seen over " + worlds + " maps)");
        Check(badFit == 0, "rooms never overlap and share a wall only through their own door (" + badFit + " bad)");
        Check(badTree == 0, "every map is connected, one door per bought room (" + badTree + " bad)");
        Check(badDoor == 0 && badSteer == 0, "every door sits on both rooms' shared wall, is open, and has a steering spot each side (" + badDoor + "/" + badSteer + " bad)");
        Check(badWall == 0, "every room's middle is open ground in its own area (" + badWall + " bad)");
        Check(badCap == 0, "the map stops at " + Expansion.MaxRooms + " rooms and its size cap (" + badCap + " over)");
        Check(shapes.Count == Expansion.Shapes.Length && kinds.Count == 3 && floors.Count >= 4, "the offers use every shape (" + shapes.Count + "), every kind and several floors (" + floors.Count + ")");
        Check(maxShops >= 1 && maxShops <= 3, "shops turn up, a few at most (" + maxShops + ")");
        Check(prevPriceBad == 0, "each room costs more than the same room would have at the start");
    }

    // The Expanding arena in a run (ExpansionGame.cs): the Arena setting, the purse, the vote on the offer, the bought
    // room coming up on both screens, the plain box until then and on boss waves, spawns inside rooms, a bigger crowd.
    static void ExpandingGameTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var pickRooms = typeof(Game).GetMethod("PickRooms", flags);
        var offerExpansion = typeof(Game).GetMethod("OfferExpansion", flags);
        var addPurse = typeof(Game).GetMethod("AddPurse", flags);
        var spawnEnemyAt = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var randomSpot = typeof(Game).GetMethod("RoomsRandomSpot", flags);

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Action NoPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Action Quiet = () =>
        {
            ((IList)typeof(Game).GetField("spawnQueue", flags).GetValue(g)).Clear();
            ((IList)typeof(Game).GetField("squadQueue", flags).GetValue(g)).Clear();
            foreach (var e in g.Enemies.Values) e.Hp = 0f;
            foreach (var b in g.Bullets.Values.Where(b => Defs.IsEnemyShot(b.Kind)).ToList()) g.Bullets.Remove(b.Id);
        };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, NoPicks);
        Quiet();

        // The setting: Expanding wins over Rooms, and until a room is bought the arena is the plain Classic box.
        g.Diff.Set(DiffKnob.ArenaExpanding, 1f); cg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
        g.Diff.Set(DiffKnob.ArenaRooms, 1f);
        Check(g.ArenaKind == ArenaLayout.Expanding && g.ExpandingOn, "the Expanding setting wins over Rooms");
        g.Diff.Set(DiffKnob.ArenaRooms, 0f);
        pickRooms.Invoke(g, null);
        Run(s, dt, 0.3f);
        Check(!g.RoomsOn && !cg.RoomsOn && g.CurrentWorld == World.Single && g.ArenaMin.X == 0f && g.ExpansionScale == 1f, "before the first room the Expanding arena is the plain box");

        // The purse: a kill pays the team.
        g.Purse = 0;
        var foe = (Enemy)spawnEnemyAt.Invoke(g, new object[] { EnemyType.Grunt, new V2(800f, 450f), 0.9f, 0, false });
        addPurse.Invoke(g, new object[] { foe });
        Quiet();
        Check(g.Purse >= 1, "a kill pays the team purse (" + g.Purse + ")");

        // The offer: three rooms, the same on the client, the purse synced; nothing offered when nothing is affordable.
        g.Purse = 0;
        offerExpansion.Invoke(g, null);
        Check(g.ExpandOffer == null, "with an empty purse nothing is offered");
        g.Purse = 500;
        offerExpansion.Invoke(g, null);
        Run(s, dt, 0.4f);
        Check(g.ExpandOffer != null && g.ExpandOffer.Count == 3 && cg.ExpandOffer != null && cg.ExpandOffer.SequenceEqual(g.ExpandOffer) && cg.Purse == g.Purse && g.Purse >= 500, "the host offers three rooms, the client sees the same ones and the purse");

        // The vote: one vote isn't enough; both counted, the winner is bought and paid for from the purse on both screens.
        var first = g.ExpandOffer[0];
        g.CastExpandVote(0);
        Run(s, dt, 0.3f);
        Check(g.ExpandOffer != null && cg.ExpandVotes.ContainsKey(1), "one vote waits for the others (and the client sees who voted)");
        int purseBefore = g.Purse;
        cg.CastExpandVote(0);
        Run(s, dt, 0.4f);
        Check(g.ExpandOffer == null && cg.ExpandOffer == null && g.Expanded.Count == 1 && cg.Expanded.Count == 1 && cg.Expanded[0].Equals(first) && g.Purse >= purseBefore - first.Price && g.Purse <= purseBefore - first.Price + 60 && cg.Purse == g.Purse,
            "both vote for a room: it is bought, paid from the purse, and both screens have it");

        // The room comes up on the next regular wave, with its walls and a bigger crowd; boss waves stay in the middle.
        pickRooms.Invoke(g, null);
        Run(s, dt, 0.4f);
        Check(g.RoomsOn && cg.RoomsOn && g.CurrentWorld.Areas.Length == 2 && g.ArenaMax.DistanceTo(g.CurrentWorld.Max) < 0.01f && cg.CurrentWorld.Areas.Length == 2 && g.Walled,
            "the bought room is up on a regular wave: two areas, walled, the same on the client");
        Check(g.LayoutOn && g.ActiveLayout.Name.Contains("Expanding") && g.ActiveLayout.Solids.Length >= g.CurrentWorld.WallsFor(false).Length, "its walls are part of the floor plan (" + g.ActiveLayout.Name + ")");
        Check(g.ExpansionScale > 1f && g.ExpansionScale <= Game.MaxExpandScale, "a bigger map means a bigger crowd (x" + g.ExpansionScale.ToString("0.00") + ")");
        bool inRooms = true, inNew = false;
        for (int i = 0; i < 300; i++)
        {
            V2 at = (V2)randomSpot.Invoke(g, new object[] { 20f });
            var a = g.CurrentWorld.Areas[g.CurrentWorld.AreaAt(at)];
            inRooms &= a.Contains(at);
            inNew |= !a.Hub;
        }
        Check(inRooms && inNew, "enemies come in inside a room, never in the gaps between them, and the new room gets some");
        int w0 = g.Wave;
        g.Wave = 10;
        pickRooms.Invoke(g, null);
        Check(g.IsBossWave(g.Wave) && !g.RoomsUp, "a boss wave is fought in the hub alone");
        g.Wave = w0;
        pickRooms.Invoke(g, null);

        // A tie goes to the room, not to buying nothing; buying nothing keeps the purse.
        g.Purse = 500;
        int before = g.Expanded.Count;
        offerExpansion.Invoke(g, null);
        Run(s, dt, 0.3f);
        int skip = g.ExpandOffer.Count;
        g.CastExpandVote(skip);
        cg.CastExpandVote(0);
        Run(s, dt, 0.4f);
        Check(g.Expanded.Count == before + 1, "a tie between a room and buying nothing buys the room");
        g.Purse = 500;
        offerExpansion.Invoke(g, null);
        Run(s, dt, 0.3f);
        int n2 = g.ExpandOffer.Count;
        g.CastExpandVote(n2); cg.CastExpandVote(n2);
        Run(s, dt, 0.4f);
        Check(g.Expanded.Count == before + 1 && g.Purse >= 500 && cg.Purse == g.Purse && g.ExpandOffer == null, "everyone buying nothing keeps the purse and the map");

        // The map stops growing at the cap.
        var full = new List<ExpansionRoom>();
        for (int i = 0; i < Expansion.MaxRooms; i++) full.Add(Expansion.Offers(3, 5 + 5 * i, full)[0]);
        Check(Expansion.Offers(3, 100, full).Count == 0, "a full map offers nothing more");
    }
}

static partial class Program
{
    // Bots play from wave 1 on the Expanding arena: the purse at the first milestone buys something, and bot players vote on their own.
    static void ExpandingEconomyTest(float dt)
    {
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 1 }, port, true, "Host", Role.Tank);
        var client = JoinAs(port, true, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        g.Diff.Set(DiffKnob.ArenaExpanding, 1f); cg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
        Run(s, dt, 400f, () => g.ExpandOffer != null || g.Wave >= 7);
        Console.WriteLine("  purse at the first milestone (wave " + g.Wave + "): " + g.Purse + (g.ExpandOffer != null ? ", offers " + string.Join(", ", g.ExpandOffer.Select(o => o.Shape + " " + o.Kind + " " + o.Price).ToArray()) : ", no offer"));
        Check(g.ExpandOffer != null, "the first boss clears with enough in the purse for a room");
        Run(s, dt, 20f, () => g.ExpandOffer == null);
        Check(g.ExpandOffer == null && cg.ExpandOffer == null, "bot players vote on their own and the offer closes");
        Console.WriteLine("  rooms bought: " + g.Expanded.Count + ", purse " + g.Purse);
    }
}
