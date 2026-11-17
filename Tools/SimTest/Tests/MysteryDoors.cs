using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Arena;

// Mystery doors (Docs/MysteryDoors.md step 1): sealed doors on the outer wall, a hidden room rolled behind each; pay to open, save one each.
static partial class Program
{
    static void MysteryDoorsTest(float dt)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        Action<Game> roll = gg => typeof(Game).GetMethod("RollMysteryDoors", flags).Invoke(gg, null);
        Action<Game> tick = gg => typeof(Game).GetMethod("TickMysteryDoors", flags).Invoke(gg, null);

        // The roll: 1 or 2 new doors, both counts seen.
        var counts = Enumerable.Range(1, 200).Select(sd => Mystery.NewDoors(sd, 7)).ToList();
        Check(counts.All(c => c == 1 || c == 2) && counts.Contains(1) && counts.Contains(2), "a cleared wave rolls 1 or 2 new doors (seeded)");
        Check(Mystery.PriceFor(100) == 60 && Mystery.PriceFor(1) == 1, "a door costs 60% of drawing the same room, at least 1");

        string why;
        var list = new List<ExpansionRoom>();
        list.Add(Expansion.Custom(list, 1600, 100, 800, 600, out why).Value);

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Expanded.Clear(); gg.Expanded.Add(list[0]); gg.RoomsUp = true; }
        typeof(Game).GetField("expandSeed", flags).SetValue(g, 4242);
        var hp = g.Players[g.MyId]; var fp = g.Players[cg.MyId]; var cme = cg.Players[cg.MyId];
        Action<Player, Player, V2> place = (onHost, onClient, at) => { onHost.Pos = onHost.Disp = at; if (onClient != null) onClient.Pos = onClient.Disp = at; };

        // Rolled at a cleared wave: on the map's outer wall, no room behind yet, and the friend sees them.
        roll(g);
        PollBoth(host, client);
        var w0 = g.CurrentWorld;
        int n0 = g.MysteryDoors.Count;
        Check(n0 >= 1 && n0 <= 2, "the first roll puts 1-2 sealed doors on the map (" + n0 + ")");
        bool onWall = g.MysteryDoors.All(d => w0.Areas.Any(ar => ar.Contains(d.Pos, 2f)) && !w0.Doors.Any(x => x.Pos.CloserThan(d.Pos, 2f)));
        Check(onWall, "each sits on an existing room's wall, where no door is yet");
        Check(cg.MysteryDoors.Count == n0 && cg.MysteryDoors.Zip(g.MysteryDoors, (u, v) => u.X == v.X && u.Y == v.Y && u.Price == v.Price).All(x => x), "the friend gets the same doors and prices");
        Check(g.MysteryDoors.All(d => d.Price >= 1) && g.MysteryDoors.Select(d => d.Pos).Distinct().Count() == n0, "every door has a price and its own spot");

        // Too poor: nothing happens.
        var d0 = g.MysteryDoors[0];
        place(fp, cme, d0.Pos + new V2(0f, 0f));
        fp.Vectors = cme.Vectors = d0.Price - 1;
        int rooms0 = g.Expanded.Count;
        cg.UseMysteryDoor(false);
        PollBoth(host, client);
        Check(g.Expanded.Count == rooms0 && g.MysteryDoors.Count == n0 && fp.Vectors == d0.Price - 1, "without the vectors the door stays shut and nothing is charged");

        // Paying opens it: the room is built, the payer charged, the door gone, the friend told; the new room's door is at that spot.
        fp.Vectors = cme.Vectors = d0.Price + 5;
        cg.UseMysteryDoor(false);
        PollBoth(host, client);
        var w1 = g.CurrentWorld;
        Check(g.Expanded.Count > rooms0 && fp.Vectors == 5 && g.MysteryDoors.Count == n0 - 1, "paying " + d0.Price + " opens it: a room is built and the payer charged (" + fp.Vectors + " left)");
        Check(w1.Doors.Any(x => x.Pos.CloserThan(d0.Pos, 2f)), "the room's door is where the sealed door was");
        Check(g.Expanded.Skip(rooms0).All(r => r.Surprise == 0 && r.Owner == cg.MyId) && g.Expanded[rooms0].Price == d0.Price, "it is a bought room: the payer's, its price on it, not a gift");
        Check(cg.Expanded.Count == g.Expanded.Count && cg.MysteryDoors.Count == g.MysteryDoors.Count, "the friend gets the room and loses the door");

        // Saving: one door per player; a saved door outlives the next roll, the others expire.
        g.Wave = 7;
        roll(g);
        PollBoth(host, client);
        Check(g.MysteryDoors.Count >= 2 && g.MysteryDoors.Count <= Mystery.Cap, "the next roll tops the map up (" + g.MysteryDoors.Count + ", cap " + Mystery.Cap + ")");
        var a = g.MysteryDoors[0]; var b = g.MysteryDoors[1];
        place(hp, null, a.Pos);
        g.UseMysteryDoor(true);
        Check(g.MysterySavedBy(g.MyId) == 0, "the host saves a door");
        place(hp, null, b.Pos);
        g.UseMysteryDoor(true);
        Check(g.MysterySavedBy(g.MyId) == 1 && g.MysteryDoors.Count(x => x.Saver == g.MyId) == 1, "saving another moves the save: one per player");
        g.UseMysteryDoor(true);
        Check(g.MysterySavedBy(g.MyId) < 0, "saving your own again lets it go");
        g.UseMysteryDoor(true);
        place(fp, cme, a.Pos);
        cg.UseMysteryDoor(true);
        PollBoth(host, client);
        Check(g.MysteryDoors[0].Saver == cg.MyId && cg.MysteryDoors[0].Saver == cg.MyId, "the friend saves another, and sees it saved");
        place(hp, null, a.Pos);
        g.UseMysteryDoor(true);
        Check(g.MysteryDoors[0].Saver == cg.MyId, "a door someone else saved can't be taken over");
        var kept = new[] { a, b }.Select(x => x.Pos).ToList();
        g.Wave = 8;
        roll(g);
        PollBoth(host, client);
        Check(kept.All(k => g.MysteryDoors.Any(x => x.Pos.CloserThan(k, 1f) && x.Saver != 0)) && g.MysteryDoors.Count <= Mystery.Cap, "the saved doors outlive the roll, the rest expire (" + g.MysteryDoors.Count + " now)");
        Check(g.MysteryDoors.Count(x => x.Saver == 0) == Mystery.Cap - 2, "new doors only fill up to the cap");

        // A drawn room taking a door's space: the door is re-planned behind the same spot, or goes; never left over a room.
        for (int t = 0; t < 6; t++)
        {
            var free = g.MysteryDoors.FindIndex(x => x.Saver == 0);
            if (free < 0) break;
            var plans = (System.Collections.IList)typeof(Game).GetField("mysteryPlans", flags).GetValue(g);
            var plan = (SurpriseOption)plans[free];
            g.Expanded.AddRange(plan.Rooms.Select(r => { r.Surprise = 0; return r; }));
            typeof(Game).GetField("expWorld", flags).SetValue(g, null);
            tick(g);
        }
        var w2 = g.CurrentWorld;
        Check(g.MysteryDoors.All(x => !w2.Doors.Any(o => o.Pos.CloserThan(x.Pos, 2f))), "a door whose room was built over is re-planned or gone, never left in a doorway");

        // Mid-wave: a door opens during a fight too.
        g.Wave = 9;
        roll(g);
        g.State = MatchState.Fighting;
        if (g.MysteryDoors.Count > 0)
        {
            var m = g.MysteryDoors[0];
            place(hp, null, m.Pos);
            hp.Vectors = 500; hp.VectorCap = 500;
            int before = g.Expanded.Count;
            g.UseMysteryDoor(false);
            Check(g.Expanded.Count > before && hp.Vectors == 500 - m.Price, "a door opens mid-wave too, for its price");
        }
        else
            Check(false, "a door to open mid-wave");
    }
}

// Mystery doors step 2: what is behind a door (Stash, chest, shop, vendor, mini-boss, ambush, or a plain combat room).
static partial class Program
{
    static void MysteryContentsTest(float dt)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;

        // The roll: every kind turns up, near the weights; no risk without it.
        var tally = new int[7];
        bool noRisk = true;
        for (int sd = 1; sd <= 4000; sd++)
        {
            tally[(int)Mystery.KindFor(sd, 9, 0, true)]++;
            noRisk &= !Mystery.IsRisk(Mystery.KindFor(sd, 9, 0, false));
        }
        Check(tally.All(c => c > 0), "every kind turns up (" + string.Join(" ", tally) + ")");
        Check(Math.Abs(tally[0] / 4000.0 - 0.5) < 0.05 && Math.Abs((tally[5] + tally[6]) / 4000.0 - 0.15) < 0.03, "about half are combat rooms and 15% risk");
        Check(noRisk, "early on (and round a boss) no door hides a mini-boss or an ambush");
        Check(Mystery.AmbushCount(5) == 5 && Mystery.AmbushCount(100) == Mystery.AmbushMax, "an ambush grows with the waves, up to " + Mystery.AmbushMax);

        string why;
        var list = new List<ExpansionRoom>();
        list.Add(Expansion.Custom(list, 1600, 100, 800, 600, out why).Value);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 8 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Expanded.Clear(); gg.Expanded.Add(list[0]); gg.RoomsUp = true; }
        typeof(Game).GetField("expandSeed", flags).SetValue(g, 777);
        var hp = g.Players[g.MyId];
        hp.Vectors = hp.VectorCap = 999;

        // Puts a door of this kind on the map (its room the best spot for its shape) and opens it. Returns the rooms it built.
        Func<MysteryKind, List<ExpansionRoom>> open = kind =>
        {
            typeof(Game).GetMethod("ResetMysteryDoors", flags).Invoke(g, null);
            var opts = Expansion.SurpriseOptions(g.Expanded, Mystery.ShapeOf(kind, SurpriseShape.Chamfer));
            foreach (var o in opts)
            {
                var args = new object[] { o, null, null };
                if (!(bool)typeof(Game).GetMethod("MysteryDoorOf", flags).Invoke(g, args)) continue;
                var d = (MysteryDoor)args[1];
                g.MysteryDoors.Add(d);
                ((List<SurpriseOption>)typeof(Game).GetField("mysteryPlans", flags).GetValue(g)).Add(o);
                ((List<V2>)typeof(Game).GetField("mysteryBehind", flags).GetValue(g)).Add((V2)args[2]);
                ((List<MysteryKind>)typeof(Game).GetField("mysteryKinds", flags).GetValue(g)).Add(kind);
                hp.Pos = hp.Disp = d.Pos;
                int before = g.Expanded.Count;
                g.UseMysteryDoor(false);
                PollBoth(host, client);
                return g.Expanded.Skip(before).ToList();
            }
            return new List<ExpansionRoom>();
        };
        Func<ExpansionRoom, V2> mid = r => new V2((r.X + r.X1) / 2f, (r.Y + r.Y1) / 2f);

        var stash = open(MysteryKind.Stash);
        Check(stash.Count == 1 && stash[0].Surprise == 1 + (int)SurpriseShape.Stash && !stash[0].Claimed && stash[0].Price > 0, "a loot door can hide a Stash (5 x 3, its cache waiting, paid for)");
        hp.Vectors = 100; int vec = hp.Vectors;  // (well under the cap)
        hp.Pos = hp.Disp = mid(stash[0]);
        typeof(Game).GetMethod("TickStashClaims", flags).Invoke(g, null);
        int got = hp.Vectors - vec;
        hp.Vectors = 999;
        Check(got == Game.StashVectors && g.Expanded.Any(r => r.Surprise == 1 + (int)SurpriseShape.Stash && r.Claimed), "walking in takes its +" + Game.StashVectors + " vectors");

        int nodes0 = g.Nodes.Count;
        var chest = open(MysteryKind.Chest);
        Check(chest.Count == 1 && chest[0].W == 400 && chest[0].H == 400 && g.Nodes.Count == nodes0 + 1 && g.Nodes.Last().Kind == NodeKind.Chest && Expansion.NodeClaimed(g.Expanded, g.Nodes.Last()), "a loot door can hide a 4 x 4 chest room (a chest node inside it)");
        Check(g.Props.Values.Any(pr => pr.Kind == PropKind.Chest && pr.Pos.DistanceTo(g.Nodes.Last().Pos) < 2f), "the chest is there at once");

        var shop = open(MysteryKind.Shop);
        Check(shop.Count == 1 && shop[0].Kind == RoomKind.Shop && g.HasShopRoom, "a door can hide a shop room (4 x 4), open at once");
        var vendor = open(MysteryKind.Vendor);
        Check(vendor.Count == 1 && vendor[0].Kind == RoomKind.Vendor && g.HasVendorRoom, "or a vendor room");
        PollBoth(host, client);
        Check(cg.HasShopRoom && cg.HasVendorRoom && cg.Nodes.Count == g.Nodes.Count, "the friend gets them too");

        var mini = open(MysteryKind.MiniBoss);
        Check(mini.Count == 1 && mini[0].Kind == RoomKind.Gatekeeper && FactionMinis.Is((EnemyType)mini[0].Mini) && mini[0].W == 800 && mini[0].H == 600, "a risk door can hide a mini-boss room (8 x 6, its faction's foe)");

        // An ambush: mid-wave its enemies appear in the room at once; between waves they wait for the wave to start.
        g.State = MatchState.Fighting;
        int e0 = g.Enemies.Count;
        var amb = open(MysteryKind.Ambush);
        var aw = g.CurrentWorld;
        int inRoom = g.Enemies.Values.Count(e => amb.Any(r => e.Pos.X >= r.X - 20 && e.Pos.X <= r.X1 + 20 && e.Pos.Y >= r.Y - 20 && e.Pos.Y <= r.Y1 + 20));
        Check(amb.Count > 0 && amb.All(r => r.Kind == RoomKind.Combat && r.Surprise == 0) && g.Enemies.Count - e0 == Mystery.AmbushCount(g.Wave) && inRoom >= Mystery.AmbushCount(g.Wave), "mid-wave an ambush room's " + Mystery.AmbushCount(g.Wave) + " enemies appear in it (" + (g.Enemies.Count - e0) + " new, " + inRoom + " in the room)");
        g.State = MatchState.Intermission;
        e0 = g.Enemies.Count;
        var amb2 = open(MysteryKind.Ambush);
        var pend = (List<V2>)typeof(Game).GetField("ambushPending", flags).GetValue(g);
        Check(amb2.Count > 0 && g.Enemies.Count == e0 && pend.Count == 1, "between waves the ambush waits");
        typeof(Game).GetMethod("SpringPendingAmbushes", flags).Invoke(g, null);
        Check(g.Enemies.Count - e0 == Mystery.AmbushCount(g.Wave) && pend.Count == 0, "and springs as the wave starts");

        var plain = open(MysteryKind.Combat);
        Check(plain.Count > 0 && plain.All(r => r.Kind == RoomKind.Combat && r.Surprise == 0), "most doors hide a plain combat room");
    }
}
