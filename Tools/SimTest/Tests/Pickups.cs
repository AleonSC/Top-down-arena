using System;
using System.Linq;
using Arena;

// Per-player pickups (Docs/RoundFlow.md step 3, Core/Coins.cs): every coin, vector pickup, health orb and Magnet a drop makes is one
// player's own copy. Only its player can take it (walking over it, or with a Magnet), the owner reaches the other screen, and a
// teammate's copy stays put.
static partial class Program
{
    static void PickupsTest(float dt)
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
        V2 a = new V2(500f, 450f), b = new V2(900f, 450f);
        Action Put = () => { g.Players[1].Pos = g.Players[1].Disp = a; cg.Players[2].Pos = cg.Players[2].Disp = b; g.Players[2].Pos = b; };
        Put();
        Run(s, dt, 0.4f, null, () => { Quiet(); Put(); });
        g.Coins.Clear(); g.Orbs.Clear(); g.Gadgets.Clear();

        // Coins: the host stands on its own coin and on its teammate's; only its own is taken.
        g.Players[1].Gold = g.Players[2].Gold = 0;
        g.Coins[7001] = new Coin { Id = 7001, Value = 5, Owner = 1, Pos = a, Disp = a };
        g.Coins[7002] = new Coin { Id = 7002, Value = 5, Owner = 2, Pos = a + new V2(4f, 0f), Disp = a };
        Run(s, dt, 0.1f, null, () => { Quiet(); Put(); });
        Check(cg.Coins.ContainsKey(7002) && cg.Coins[7002].Owner == 2 && cg.MinePickup(cg.Coins[7002].Owner) && (!cg.Coins.ContainsKey(7001) || !cg.MinePickup(cg.Coins[7001].Owner)),
            "the owner reaches the client, which shows its own coin and hides its teammate's");
        Run(s, dt, 1f, null, () => { Quiet(); Put(); });
        Check(!g.Coins.ContainsKey(7001) && g.Coins.ContainsKey(7002) && g.Players[1].Gold == 5 && g.Players[2].Gold == 0, "standing on both, a player takes only their own coin (" + g.Players[1].Gold + " gold)");
        b = a;
        Run(s, dt, 1f, () => !g.Coins.ContainsKey(7002), () => { Quiet(); Put(); });
        Check(!g.Coins.ContainsKey(7002) && g.Players[2].Gold == 5, "its owner walks over it and takes it");
        b = new V2(900f, 450f);

        // Hover: a coin or vector pickup under the cursor is taken from across the room, each takes only their own copy, and a full stock refuses a vector.
        {
            var far = new V2(1500f, 700f);
            g.Players[1].Gold = 0; g.Players[1].Vectors = 0; g.Players[2].Gold = 0;
            g.Coins[7010] = new Coin { Id = 7010, Value = 5, Owner = 1, Pos = far, Disp = far };
            g.Coins[7011] = new Coin { Id = 7011, Value = 5, Owner = 2, Pos = far + new V2(3f, 0f), Disp = far };
            g.Coins[7012] = new Coin { Id = 7012, Value = 6, Owner = 1, Vec = true, Pos = far + new V2(8f, 0f), Disp = far };
            aimOverride = far;
            Run(s, dt, 0.1f, null, () => { Quiet(); Put(); });
            aimOverride = null;
            Check(!g.Coins.ContainsKey(7010) && !g.Coins.ContainsKey(7012) && g.Players[1].Gold == 5 && g.Players[1].Vectors > 0, "hovering the cursor over your own coin and vector pickup takes them from across the room");
            Check(g.Players[2].Gold >= 5 && g.Players[1].Gold == 5, "each player's hover takes only their own copy (host " + g.Players[1].Gold + ", friend " + g.Players[2].Gold + ")");
            g.Coins.Clear();
        }

        // A Magnet pulls only its holder's own coins and vector pickups.
        g.Players[1].Gadget = GadgetId.Magnet; g.Players[1].GadgetLeft = 30f; g.Players[1].GadgetOn = true;
        g.Coins[7003] = new Coin { Id = 7003, Value = 5, Owner = 1, Pos = new V2(1300f, 200f), Disp = new V2(1300f, 200f) };
        g.Coins[7004] = new Coin { Id = 7004, Value = 5, Owner = 2, Pos = new V2(1300f, 260f), Disp = new V2(1300f, 260f) };
        Run(s, dt, 3f, () => !g.Coins.ContainsKey(7003), () => { Quiet(); Put(); });
        Check(!g.Coins.ContainsKey(7003) && g.Coins.ContainsKey(7004) && g.Coins[7004].Pos.DistanceTo(new V2(1300f, 260f)) < 1f, "the Magnet pulls its holder's coin and leaves the teammate's where it lies");
        g.Players[1].Gadget = GadgetId.None; g.Players[1].GadgetOn = false;
        g.Coins.Clear();

        // Health orbs: a hurt player only drinks their own.
        g.DevSetGod(1, false);
        var h = g.Players[1];
        h.Hp = h.MaxHp * 0.5f;
        g.Orbs[7101] = new Orb { Id = 7101, Owner = 2, Pos = a, Disp = a, Life = 10f };
        Run(s, dt, 0.5f, null, () => { Quiet(); Put(); });
        Check(g.Orbs.ContainsKey(7101) && h.Hp < h.MaxHp * 0.5f + 3f, "a hurt player leaves their teammate's orb alone (" + g.Orbs.ContainsKey(7101) + ", " + h.Hp.ToString("0.0") + "/" + (h.MaxHp * 0.5f).ToString("0.0") + ")");
        g.Orbs[7102] = new Orb { Id = 7102, Owner = 1, Pos = a + new V2(20f, 0f), Disp = a, Life = 10f };
        Run(s, dt, 1f, () => !g.Orbs.ContainsKey(7102), () => { Quiet(); Put(); });
        Check(!g.Orbs.ContainsKey(7102) && h.Hp > h.MaxHp * 0.5f + 3f, "...and drinks their own");
        g.Orbs.Clear();
        g.DevSetGod(1, true);

        // Magnets: a teammate's Magnet lies where it is; its owner picks it up; once carried and dropped it's anyone's.
        var mag = g.SpawnGadget(GadgetId.Magnet, a, Game.MagnetTime);
        mag.Owner = 2;
        Run(s, dt, 0.5f, null, () => { Quiet(); Put(); });
        Check(g.Gadgets.ContainsKey(mag.Id) && g.Players[1].Gadget == GadgetId.None && cg.Gadgets.ContainsKey(mag.Id) && cg.Gadgets[mag.Id].Owner == 2, "a player walks over their teammate's Magnet and leaves it (the owner reaches the client)");
        b = a;
        Run(s, dt, 1f, () => g.Players[2].Gadget == GadgetId.Magnet, () => { Quiet(); Put(); });
        Check(g.Players[2].Gadget == GadgetId.Magnet && !g.Gadgets.ContainsKey(mag.Id), "its owner picks it up");
        b = a + new V2(200f, 0f);  // (inside the throw range)
        Run(s, dt, 0.3f, null, () => { Quiet(); Put(); });
        cg.ThrowGadget(a);
        Run(s, dt, 2f, () => g.Players[1].Gadget == GadgetId.Magnet, () => { Quiet(); Put(); });
        Check(g.Players[1].Gadget == GadgetId.Magnet, "thrown to the teammate, they catch it: a carried Magnet is anyone's");

        // The sweep as the next wave starts (Docs/RoundFlow.md step 4): each player's own coins and vector pickups go to them; a vector
        // pickup waits while its player's stock is full; orbs, Magnets and anyone's coins stay.
        g.Players[1].Gadget = g.Players[2].Gadget = GadgetId.None;
        g.Gadgets.Clear();
        Run(s, dt, 60, () => g.State == MatchState.Intermission, () => { Quiet(); Put(); });
        Check(g.State == MatchState.Intermission, "(a break to sweep at)");
        g.Coins.Clear(); g.Orbs.Clear();
        foreach (var p in g.Players.Values) { p.Gold = 100; p.AutoReady = true; }
        g.Players[2].Vectors = 10;
        g.Players[1].Vectors = g.Players[1].VectorCap;
        g.Coins[7201] = new Coin { Id = 7201, Value = 5, Owner = 1, Pos = new V2(1300f, 150f), Disp = new V2(1300f, 150f) };
        g.Coins[7202] = new Coin { Id = 7202, Value = 20, Owner = 2, Pos = new V2(1300f, 200f), Disp = new V2(1300f, 200f) };
        g.Coins[7203] = new Coin { Id = 7203, Vec = true, Value = 6, Owner = 2, Pos = new V2(1300f, 250f), Disp = new V2(1300f, 250f) };
        g.Coins[7204] = new Coin { Id = 7204, Vec = true, Value = 6, Owner = 1, Pos = new V2(1300f, 300f), Disp = new V2(1300f, 300f) };
        g.Coins[7205] = new Coin { Id = 7205, Value = 5, Owner = 0, Pos = new V2(1300f, 350f), Disp = new V2(1300f, 350f) };
        g.Orbs[7206] = new Orb { Id = 7206, Owner = 1, Pos = new V2(1300f, 400f), Disp = new V2(1300f, 400f), Life = 60f };
        g.Loots[7301] = new Loot { Id = 7301, Owner = 2, Upgrade = 0, Pos = new V2(1300f, 450f) };  // an unclaimed chest card
        int vec2 = g.Players[2].Vectors;
        Run(s, dt, 30, () => g.State == MatchState.Fighting, () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.Offer = null; } Put(); });
        Check(g.State == MatchState.Fighting, "(the next wave started)");
        Check(g.Players[1].Gold == 105 && g.Players[2].Gold == 120 && !g.Coins.ContainsKey(7201) && !g.Coins.ContainsKey(7202),
            "as the wave starts each player's own coins go to them (" + g.Players[1].Gold + "/" + g.Players[2].Gold + ")");
        Check(!g.Coins.ContainsKey(7203) && g.Players[2].Vectors > vec2, "...and their own vector pickups (" + vec2 + " -> " + g.Players[2].Vectors + ")");
        Check(g.Coins.ContainsKey(7204), "a vector pickup waits while its player's stock is full");
        Check(g.Coins.ContainsKey(7205) && g.Orbs.ContainsKey(7206), "anyone's coins and health orbs stay where they are");
        Check(!g.Loots.ContainsKey(7301), "an unclaimed chest card is dealt with as the wave starts (taken if it still fits)");
        Run(s, dt, 0.3f);
        Check(!cg.Coins.ContainsKey(7202) && cg.Players[2].Gold == 120, "the client's screen agrees (its coins gone, its gold up)");

        // Coins in the snapshot (Coins.cs SyncCoins): resting coins are sent once, not every snapshot; the client still sees every
        // coin dropped, moved and taken, and a client that lost track is put right by the next full list.
        var sync = typeof(Game).GetMethod("SyncSnapshot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Func<int> Bytes = () => { var ms = new System.IO.MemoryStream(); sync.Invoke(g, new object[] { new NetSync(new System.IO.BinaryWriter(ms)) }); return (int)ms.Length; };
        g.Coins.Clear();
        Run(s, dt, 0.2f, null, () => { Quiet(); Put(); });
        int none = Bytes();
        for (int i = 0; i < 1000; i++)
        {
            var at = new V2(200f + (i % 40) * 30f, 600f + (i / 40) * 12f);
            g.Coins[8000 + i] = new Coin { Id = 8000 + i, Value = 1, Owner = 1 + i % 2, Pos = at, Disp = at };
        }
        Run(s, dt, 0.2f, null, () => { Quiet(); Put(); });
        int rest = Bytes();
        Check(rest - none < 200, "1000 resting coins add almost nothing to a snapshot (" + none + " -> " + rest + " bytes)");
        Check(cg.Coins.Count == 1000 && cg.Coins.ContainsKey(8999), "...yet the client has every one (" + cg.Coins.Count + ")");
        g.Coins.Remove(8001);
        g.Coins[8002].Pos += new V2(40f, 0f);
        Run(s, dt, 0.2f, null, () => { Quiet(); Put(); });
        Check(!cg.Coins.ContainsKey(8001) && cg.Coins[8002].Pos.DistanceTo(g.Coins[8002].Pos) < 0.01f, "a coin taken goes and a coin pulled moves on the client too");
        cg.Coins.Clear();  // (the client lost track)
        Run(s, dt, Game.CoinFullEvery * 3 * dt + 0.3f, null, () => { Quiet(); Put(); });
        Check(cg.Coins.Count == g.Coins.Count, "a full list puts a client that lost track right within " + (Game.CoinFullEvery * 3 * dt).ToString("0.0") + " s (" + cg.Coins.Count + "/" + g.Coins.Count + ")");
        g.Coins.Clear();
        g.Bot = true;  // (the smart bot plays the host and aims where it likes)
        BloodOrbChecks(g, cg, s, dt, Quiet, Put);
        Run(s, dt, 0.1f, null, () => { Quiet(); Put(); });
        Check(g.Players[1].IsBot, "a bot's input marks its player as a bot");
        g.Bot = false;
        Run(s, dt, 0.1f, null, () => { Quiet(); Put(); });
        Check(!g.Players[1].IsBot, "a human's input does not");
        s[0].Leave(""); s[1].Leave("");
    }

    /// Blood orbs (Combat.cs DropBloodOrb): only a Hemomancer's kills drop them, one orb for the team that anyone can drink.
    static void BloodOrbChecks(Game g, Game cg, Session[] s, float dt, Action Quiet, Action Put)
    {
        var host = g.Players[1];
        Kit was = host.Kit;
        Func<Kit, int> Kills = kit =>
        {
            host.Kit = kit;
            g.Orbs.Clear();
            int made = 0; bool shared = true;
            for (int k = 0; k < 6; k++)
            {
                g.DevSpawnEnemy(1, EnemyType.Grunt, 10, false);
                foreach (var e in g.Enemies.Values) { e.LastHitBy = 1; e.Hp = 0f; }
                Run(s, dt, dt * 2f, null, null);
                made += g.Orbs.Values.Count(o => o.Blood);
                foreach (var o in g.Orbs.Values.Where(o => o.Blood).ToList()) { shared &= o.Owner == 0 && !o.Big && o.Life > Game.OrbLifetime; g.Orbs.Remove(o.Id); }
            }
            if (made > 0)
                Check(shared, "each blood orb is one shared orb that lasts longer");
            return made;
        };
        int hemo = Kills(Kit.Hemomancer), other = Kills(Kit.Soldier);
        Check(hemo > 0 && hemo < 30, "60 Hemomancer kills drop some blood orbs (" + hemo + ")");
        Check(other == 0, "kills by another class drop none (" + other + ")");
        host.Kit = was;

        // Anyone can drink one: it reaches the client, then drifts across the room to the hurt teammate.
        g.Orbs.Clear();
        var friend = g.Players[2];
        friend.Hp = friend.HealCap - 40f; host.Hp = host.HealCap;
        var at = friend.Pos + new V2(-400f, 0f);
        g.Orbs[7300] = new Orb { Id = 7300, Blood = true, Pos = at, Disp = at, Life = Game.BloodOrbLife };
        Run(s, dt, 0.1f, null, () => { Quiet(); Put(); });
        Check(cg.Orbs.ContainsKey(7300) && cg.Orbs[7300].Blood && cg.MinePickup(cg.Orbs[7300].Owner), "the client sees the blood orb as one it can take");
        float before = friend.Hp;
        Run(s, dt, 3f, () => !g.Orbs.ContainsKey(7300), () => { Quiet(); Put(); });
        Check(!g.Orbs.ContainsKey(7300) && friend.Hp >= before + Game.BloodOrbHeal - 0.5f, "it drifts 400 px to the hurt teammate and heals them (" + before + " -> " + friend.Hp + ")");
        friend.Hp = friend.HealCap;
    }
}
