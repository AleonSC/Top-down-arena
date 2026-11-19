using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Remote chests (Docs/RoundFlow.md step 5, Core/RemoteChests.cs): between waves anyone can collect a chest from the wave box's Chests
// tab for a fee; the chest opens where it stands and everyone gets their share (gold straight into wallets, cards at their own feet).
// Not without the gold, and not during a wave.
static partial class Program
{
    static void RemoteChestsTest(float dt)
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
        V2 a = new V2(500f, 450f), b = new V2(800f, 450f), far = new V2(1300f, 200f);
        Action Put = () => { g.Players[1].Pos = g.Players[1].Disp = a; cg.Players[2].Pos = cg.Players[2].Disp = b; g.Players[2].Pos = b; };
        g.SetMyAutoReady(false); cg.SetMyAutoReady(false);
        Run(s, dt, 60, () => g.State == MatchState.Intermission, () => { Quiet(); Put(); });
        Check(g.State == MatchState.Intermission && g.CanCollectChests, "(a break, where chests can be collected)");
        g.Props.Clear(); g.Loots.Clear(); g.Coins.Clear();

        // A Gold chest far away: the client can't afford it, then can; everyone gets the gold, nothing lands at the chest.
        var gold = g.AddProp(PropKind.Chest, far); gold.Chest = ChestKind.Gold;
        int fee = g.ChestCollectFee, each = Game.ChestGold + Game.ChestGoldPerWave * Math.Max(1, g.Wave);
        g.Players[1].Gold = 50; g.Players[2].Gold = fee - 1;
        Run(s, dt, 0.4f, () => cg.Props.ContainsKey(gold.Id), () => { Quiet(); Put(); });
        var list = cg.UncollectedChests(new List<ChestSpot>());
        Check(list.Count == 1 && list[0].Id == gold.Id && list[0].Kind == ChestKind.Gold, "the client's Chests tab lists the chest with its kind (" + list.Count + ")");
        Check(fee == Game.ChestCollectBase + Game.ChestCollectPerWave * Math.Max(1, g.ShownWave), "the fee is 15 + 2 per wave (" + fee + ")");
        cg.CollectChest(list[0]);
        Run(s, dt, 0.4f, null, () => { Quiet(); Put(); });
        Check(!gold.Open && g.Players[2].Gold == fee - 1, "without the fee nothing happens");
        g.Players[2].Gold = fee + 10;
        cg.CollectChest(list[0]);
        Run(s, dt, 0.4f, null, () => { Quiet(); Put(); });
        Check(gold.Open && g.Players[2].Gold == 10 + each && g.Players[1].Gold == 50 + each && g.Coins.Count == 0,
            "collected: the payer pays the fee, everyone's share goes straight into their wallet, no coins at the chest (" + g.Players[1].Gold + "/" + g.Players[2].Gold + ", " + g.Coins.Count + " coins)");
        Run(s, dt, 0.3f, null, () => { Quiet(); Put(); });
        Check(cg.Players[2].Gold == 10 + each, "the client sees its gold");
        Check(cg.UncollectedChests(new List<ChestSpot>()).Count == 0, "...and the tab has nothing left to collect");

        // An Upgrade chest collected by the host: each player's card lands at their own feet.
        g.Props.Clear();
        var up = g.AddProp(PropKind.Chest, far); up.Chest = ChestKind.Upgrade;
        g.Players[1].Gold = 100;
        g.CollectChest(new ChestSpot { Id = up.Id, Kind = ChestKind.Upgrade, Pos = far });
        Check(up.Open && g.Players[1].Gold == 100 - fee, "the host collects too (" + g.Players[1].Gold + ")");
        Check(g.Loots.Values.All(l => g.Players.ContainsKey(l.Owner) && l.Pos.DistanceTo(g.Players[l.Owner].Pos) < 80f && l.Pos.DistanceTo(far) > 200f),
            "each player's card lands at their own feet, not at the chest (" + g.Loots.Count + " cards)");
        g.Loots.Clear();

        // Not during a wave.
        g.Props.Clear();
        var late = g.AddProp(PropKind.Chest, far); late.Chest = ChestKind.Gold;
        g.SetMyAutoReady(true); cg.SetMyAutoReady(true);
        Run(s, dt, 30, () => g.State == MatchState.Fighting, () => { Quiet(); Put(); });
        g.Players[1].Gold = 100;
        g.CollectChest(new ChestSpot { Id = late.Id, Kind = ChestKind.Gold, Pos = far });
        Check(g.State != MatchState.Intermission && !late.Open && g.Players[1].Gold == 100 && !g.CanCollectChests, "during a wave a chest can't be collected from afar");
        s[0].Leave(""); s[1].Leave("");
    }
}
