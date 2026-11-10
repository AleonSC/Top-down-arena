using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Arena;

// Challenge rooms (Challenges.cs): a portal once every five waves, the five challenges, the prize, and failing safely.
static partial class Program
{
    static void ChallengesTest(float dt)
    {
        Game.ChallengesByDefault = true;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        // The schedule: exactly one portal in every stretch of five waves, after one of its first three regular waves
        // (never wave 1), each one turning up.
        {
            var maybe = typeof(Game).GetMethod("MaybeOpenPortal", flags);
            var seen = new HashSet<int>();
            bool oncePerBlock = true, neverWave1 = true, regularAfter = true;
            for (int seed = 0; seed < 40; seed++)
            {
                Game.SeedForTests(seed + 1000);
                var solo = new Session { StartWave = 1 };
                solo.HostOffline(Role.Tank, Kit.Soldier, 0, 0);
                var sg = solo.Game;
                var perBlock = new int[10];
                for (int w = 1; w < Game.MaxWave; w++)
                {
                    sg.Wave = w; sg.BossSlot = (w) % 5 == 0 && (w) > 0;
                    sg.State = MatchState.Intermission;
                    maybe.Invoke(sg, null);
                    if (sg.Room == RoomPhase.Portal)
                    {
                        perBlock[(w - 1) / 5]++;
                        seen.Add(w % 5);
                        neverWave1 &= w != 1;
                        regularAfter &= (w + 1) % 5 != 0 && w % 5 != 0;
                        sg.Room = RoomPhase.None;
                    }
                }
                oncePerBlock &= perBlock.All(c => c == 1);
            }
            Check(oncePerBlock && neverWave1 && regularAfter && seen.SetEquals(new[] { 1, 2, 3 }),
                "a portal opens exactly once in every five waves, after the 1st, 2nd or 3rd wave of the stretch (never wave 1, never before a boss)");
        }

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 7 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        var cg = client.Game;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        // Players stand where they're told (the client moves its own player).
        V2 hostAt = new V2(200f, 200f), friendAt = new V2(1400f, 200f);
        Action hold = () => { g.Players[1].Pos = hostAt; cg.Players[2].Pos = friendAt; };
        Action quiet = () =>
        {
            ((System.Collections.IList)typeof(Game).GetField("spawnQueue", flags).GetValue(g)).Clear();
            ((System.Collections.IList)typeof(Game).GetField("squadQueue", flags).GetValue(g)).Clear();
        };
        // Get to a break.
        Run(s, dt, 3f, null, () => { noPicks(); hold(); });
        Run(s, dt, 60f, () => g.State == MatchState.Intermission && g.Room == RoomPhase.None, () => { noPicks(); hold(); quiet(); foreach (var e in g.Enemies.Values) e.Hp = 0f; });
        Run(s, dt, 0.3f, null, () => { noPicks(); hold(); });
        Func<ChallengeKind, bool> enter = kind =>
        {
            g.State = MatchState.Intermission;
            g.StateTimer = 5f;
            g.DevOpenPortal(kind);
            hostAt = friendAt = g.PortalPos;
            Run(s, dt, 3f, () => g.InRoom, () => { noPicks(); hold(); });
            hostAt = new V2(700f, 650f); friendAt = new V2(900f, 650f);
            Run(s, dt, 0.3f, null, hold);
            return g.InRoom && cg.InRoom && cg.RoomKind == kind;
        };

        // The portal: one player in it isn't enough; it waits (and so does the break's countdown); both in, through you go.
        g.State = MatchState.Intermission;
        g.StateTimer = 5f;
        g.DevOpenPortal(ChallengeKind.HoldZones);
        Run(s, dt, 0.3f, null, () => { noPicks(); hold(); });
        bool clientSeesPortal = cg.Room == RoomPhase.Portal && cg.PortalPos.DistanceTo(g.PortalPos) < 1f;
        hostAt = g.PortalPos;
        float timer0 = g.StateTimer;
        Run(s, dt, 3f, null, () => { noPicks(); hold(); });
        bool halfOnly = g.Room == RoomPhase.Portal && g.PortalFill <= 0.951f && g.PortalFill > 0.5f && g.StateTimer == timer0;
        friendAt = g.PortalPos;
        // Everyone's in the gate, but a player still has upgrade cards up: the gate can't be entered until they've picked.
        Run(s, dt, 3f, () => g.InRoom, () => { noPicks(); g.Players[2].PendingPicks = 1; hold(); });
        bool blockedByPicks = !g.InRoom && g.Room == RoomPhase.Portal && g.PortalFill < 1f;
        Check(blockedByPicks, "a Data Gate can't be entered while a player's upgrade picks are unselected");
        Run(s, dt, 3f, () => g.InRoom, () => { noPicks(); hold(); });
        Check(clientSeesPortal && halfOnly && g.InRoom && g.State == MatchState.Fighting,
            "a portal waits (and holds the break) until everyone alive stands in it, then takes the party into the room (seen on the client)");

        // Hold the zones: standing in each fills it, three in turn.
        Run(s, dt, 3.5f, () => g.RoomActive, hold);
        int z0 = g.ZoneIndex;
        Run(s, dt, 40f, () => g.Room != RoomPhase.Active, () =>
        {
            if (g.ZoneIndex < Challenges.ZoneCount) hostAt = friendAt = g.RoomZones[g.ZoneIndex];
            hold();
            foreach (var e in g.Enemies.Values) e.Hp = 0f;
        });
        Run(s, dt, 0.3f, null, hold);  // the client hears about the win a moment after the host
        Check(z0 == 0 && g.Room == RoomPhase.Won && g.ZoneIndex == Challenges.ZoneCount && cg.Room == RoomPhase.Won, "Hold the Zones: standing in each zone fills it, and three won is the challenge");

        // The altar: touch it for the prize, picked from rare or better.
        hostAt = g.AltarPos;
        Run(s, dt, 4f, () => !g.InRoom, () => { hold(); });
        var me = g.Players[1];
        var friend = g.Players[2];
        bool prize = me.OfferPrize && me.Offer != null && me.Offer.All(u => Upgrades.All[u].Rarity >= Rarity.Rare)
            || me.OfferLegendary && me.PrizePicks == 1;
        bool friendPrize = friend.OfferPrize || friend.OfferLegendary && friend.PrizePicks == 1;
        Check(!g.InRoom && g.State == MatchState.Intermission && prize && friendPrize,
            "touching the altar gives everyone a prize pick of rare or better cards, and the break carries on");
        bool prizeOnly = true;
        var rng = new Random(5);
        foreach (Role r in new[] { Role.Tank, Role.Dps, Role.Healer })
            for (int i = 0; i < 30; i++)
                prizeOnly &= Upgrades.PrizeOffer(rng, r, AbilityId.None, new List<int>()).All(u => u >= 0 && Upgrades.All[u].Rarity >= Rarity.Rare && Upgrades.All[u].Rarity <= Rarity.VeryRare);
        Check(prizeOnly, "a prize offer only has rare and very rare cards");
        // Nobody picks for a while: the break waits for the prize (it used to run out after 4 s, start the next wave
        // with the cards still up, and then refuse the pick, leaving the player unable to fire), and the pick still works.
        // (Only the prize left to pick: level-ups from the room's kills would hold the break by themselves.)
        foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.PrizePicks = 1; }
        typeof(Game).GetMethod("NextOffer", flags).Invoke(g, new object[] { me });
        typeof(Game).GetMethod("NextOffer", flags).Invoke(g, new object[] { friend });
        g.StateTimer = 4f;
        Run(s, dt, 6f, null, hold);
        bool waited = g.State == MatchState.Intermission && !g.InRoom && me.Offer != null;
        int before = me.Taken.Count;
        for (int k = 0; k < 10 && me.Offer != null; k++) { g.PickUpgrade(0); Run(s, dt, 0.05f, null, hold); }
        Console.WriteLine("  prize left unpicked for 6 s: still the break {0}, cards up {1}; picked {2}, cards now {3}",
            g.State == MatchState.Intermission, waited, me.Taken.Count - before, me.Offer == null ? "gone" : "up");
        Check(waited && me.Taken.Count > before && me.Offer == null, "the break waits while anyone still has a prize to pick, and the pick goes through");
        noPicks();
        foreach (var p in g.Players.Values) p.PrizePicks = 0;
        Run(s, dt, 0.3f, null, hold);

        // Defend the crystal: enemies go for it; enough hits break it and the challenge is lost, safely.
        Check(enter(ChallengeKind.Defend), "Defend the Crystal: in the room (seen on the client)");
        var defendSeen = new Dictionary<int, EnemyType>();
        Action holdSee = () => { hold(); foreach (var e in g.Enemies.Values) defendSeen[e.Id] = e.Type; };
        Run(s, dt, 6f, null, holdSee);
        int forCrystal = g.Enemies.Values.Count(e => e.Id % 3 != 0);
        float hp0 = g.CrystalMax;
        Run(s, dt, 15f, () => g.CrystalHp < hp0 - 1f, holdSee);
        int meleeSeen = defendSeen.Values.Count(ty => Defs.Get(ty).Melee);
        Console.WriteLine("  Defend the Crystal's enemies: {0} melee of {1} ({2})", meleeSeen, defendSeen.Count,
            string.Join(", ", defendSeen.Values.GroupBy(ty => ty).Select(gr => gr.Key + " " + gr.Count())));
        Check(defendSeen.Count >= 4 && meleeSeen >= defendSeen.Count * 0.7f, "Defend the Crystal's enemies are mostly melee (bodies to intercept, not guns)");
        Check(forCrystal > 0 && g.CrystalHp < hp0 && cg.CrystalHp < hp0 + 0.01f, "the room's enemies go for the crystal and wear it down (its health on the client too)");
        g.CrystalHp = 0.5f;
        Run(s, dt, 6f, () => !g.InRoom, hold);
        Check(!g.InRoom && g.State == MatchState.Intermission && g.Players.Values.All(p => !p.Downed), "a broken crystal loses the challenge: back to the break, no prize");
        noPicks();

        // Kill rush: the kills count, the goal wins it.
        Check(enter(ChallengeKind.KillRush), "Kill Rush: in the room");
        Run(s, dt, 3.5f, () => g.RoomActive, hold);
        int goal = g.RoomGoal;
        Run(s, dt, 40f, () => g.Room != RoomPhase.Active, () => { hold(); foreach (var e in g.Enemies.Values) if (e.SpawnT <= 0f) e.Hp = 0f; });
        Check(goal == Challenges.RushGoal(2) && g.RoomKills >= goal && g.Room == RoomPhase.Won && cg.RoomKills >= goal - 2,
            "Kill Rush: " + goal + " kills before the clock runs out wins it (" + g.RoomKills + ")");
        Run(s, dt, 16f, () => !g.InRoom, hold);  // nobody touches the altar: it gives the prize by itself
        Check(!g.InRoom && g.Players.Values.All(p => p.PrizePicks > 0 || p.OfferPrize || p.LegendaryQueue.Count > 0), "an altar nobody touches still gives the prize in the end");
        noPicks();
        foreach (var p in g.Players.Values) p.PrizePicks = 0;

        // The Gatekeeper: it fights (shots, and its charge), it can be beaten.
        Check(enter(ChallengeKind.MiniBoss), "The Gatekeeper: in the room");
        Run(s, dt, 3.5f, () => g.RoomActive, hold);
        Run(s, dt, 10f, null, hold);
        var gate = g.Enemies.Values.FirstOrDefault(e => e.Type == EnemyType.Gatekeeper);
        bool shooting = g.Bullets.Values.Any(b => b.Source == EnemyType.Gatekeeper);
        bool tough = gate != null && gate.MaxHp > g.SpawnHp(EnemyType.Brute, g.Wave) * 5f;
        bool clientSees = cg.Enemies.Values.Any(e => e.Type == EnemyType.Gatekeeper);
        if (gate != null) gate.Hp = 0f;
        Run(s, dt, 2f, () => g.Room == RoomPhase.Won, hold);
        Check(gate != null && shooting && tough && clientSees && g.Room == RoomPhase.Won, "The Gatekeeper: a tough mini-boss that fights back, and beating it wins the challenge");
        hostAt = g.AltarPos;
        Run(s, dt, 4f, () => !g.InRoom, hold);
        noPicks();
        foreach (var p in g.Players.Values) p.PrizePicks = 0;

        // Mirror match: a shadow for each player, hunting its own player, firing; the client knows whose is whose.
        Check(enter(ChallengeKind.Mirror), "Mirror Match: in the room");
        Run(s, dt, 3.5f, () => g.RoomActive, hold);
        Run(s, dt, 4f, null, hold);
        var shadows = g.Enemies.Values.Where(e => e.Type == EnemyType.Shadow).ToList();
        bool ownPlayers = shadows.Count == 2 && shadows.Select(e => g.ShadowOf[e.Id]).OrderBy(x => x).SequenceEqual(new[] { 1, 2 });
        bool clientKnows = cg.ShadowOf.Count == 2 && shadows.All(e => cg.ShadowOf.ContainsKey(e.Id) && cg.ShadowOf[e.Id] == g.ShadowOf[e.Id]);
        bool fired = g.Bullets.Values.Any(b => b.Source == EnemyType.Shadow);
        bool hardy = shadows.All(e => e.MaxHp >= g.Players[g.ShadowOf[e.Id]].MaxHp * 2f);
        Check(ownPlayers && clientKnows && fired && hardy, "Mirror Match: a shadow of each player, fighting back, and every screen knows whose is whose");
        // One on one: a duo's shadows are sized for a solo player (Normal: 85%), not for the party.
        float party = g.BossEquivalentHp(g.Wave) * Challenges.ShadowShare;
        Check(shadows.All(e => e.MaxHp < party * 0.9f && e.MaxHp > party * 0.8f), $"Mirror Match: each shadow has a solo player's share ({shadows.FirstOrDefault()?.MaxHp:0} of {party:0})");

        // Everyone down in the room: the challenge is lost, not the run; the downed get back up.
        g.DevSetGod(1, false); g.DevSetGod(2, false);
        foreach (var p in g.Players.Values) { p.Hp = 0f; p.Downed = true; }
        Run(s, dt, 0.5f, null, hold);
        bool lostNotOver = g.Room == RoomPhase.Lost && g.State != MatchState.GameOver;
        Run(s, dt, 5f, () => !g.InRoom, hold);
        Check(lostNotOver && !g.InRoom && g.State == MatchState.Intermission && g.Players.Values.All(p => !p.Downed && p.Hp > 0f && p.Hp <= p.MaxHp * 0.31f),
            "everyone going down in a room loses the challenge, not the run: back to the break, the downed up at low health");
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        // A portal nobody enters closes, and the next wave comes as usual.
        g.State = MatchState.Intermission;
        g.StateTimer = 3f;
        int wave = g.Wave;
        g.DevOpenPortal(ChallengeKind.KillRush);
        hostAt = new V2(100f, 100f); friendAt = new V2(1500f, 800f);
        Run(s, dt, Challenges.PortalLife + 1f, () => g.Room == RoomPhase.None, () => { noPicks(); hold(); });
        bool closed = g.Room == RoomPhase.None && g.State == MatchState.Intermission;
        Run(s, dt, 6f, () => g.State == MatchState.Fighting, () => { noPicks(); hold(); });
        Check(closed && g.State == MatchState.Fighting && g.Wave == wave + 1, "a portal nobody enters closes after a while, and the next wave comes as usual");

        // Records: each room played and won is kept in the profile, and survives saving.
        var prof = new Profile();
        prof.RoomEnded(ChallengeKind.Mirror, true);
        prof.RoomEnded(ChallengeKind.Mirror, false);
        prof.RoomEnded(ChallengeKind.Defend, false);
        var back = Profile.Parse(prof.ToText());
        Check(back.RoomPlayed(ChallengeKind.Mirror) == 2 && back.RoomWon(ChallengeKind.Mirror) == 1 && back.RoomPlayed(ChallengeKind.Defend) == 1 && back.RoomWon(ChallengeKind.Defend) == 0,
            "the Records keep each challenge room played and won, through saving");

        host.Leave(""); client.Leave("");
        Game.ChallengesByDefault = false;
    }
}

// Balance: a duo of Good bots plays a run with the challenge rooms on and reports how each room went
// (run.sh 1 1 rooms [last wave] [seed]).
static partial class Program
{
    static void RoomsReport(float dt, int lastWave, int seed)
    {
        Game.ChallengesByDefault = true;
        Game.SeedForTests(seed);
        var host = new Session { AutoStart = 2, StartWave = 1 };
        host.Game.Bot = true; host.Game.BotLevel = BotSkill.Good; host.Game.BotRerolls = false;
        int port = nextPort++;
        host.Host(port, "A", Role.Tank, 0);
        var c = new Session(); c.Game.Bot = true; c.Game.BotLevel = BotSkill.Good; c.Game.BotRerolls = false;
        c.Join("127.0.0.1", port, "B", Role.Dps, 0);
        WaitForGame(host, c, dt);
        var g = host.Game;
        var last = RoomPhase.None;
        float clock = 0f, crystal = 0f;
        int kills = 0, zone = 0;
        ChallengeKind kind = ChallengeKind.None;
        float bossStart = -1f, bossHp = 0f, roomHp = 0f;
        for (int f = 0; f < 60 * 60 * 60 && g.State != MatchState.GameOver && g.Wave <= lastWave; f++)
        {
            Step(host, dt); Step(c, dt);
            var boss = g.Enemies.Values.FirstOrDefault(e => e.Def.IsBoss);
            if (boss != null && bossStart < 0f) { bossStart = g.RunTime; bossHp = boss.MaxHp; }
            if (boss == null && bossStart >= 0f && g.State == MatchState.Intermission && g.Wave % 5 == 0)
            {
                Console.WriteLine("  wave {0,2}: boss, {1:0} hp, {2:0} s", g.Wave, bossHp, g.RunTime - bossStart);
                bossStart = -1f;
            }
            foreach (var e in g.Enemies.Values)
                if (Challenges.Own(e.Type) && e.MaxHp > roomHp) roomHp = e.MaxHp;
            if (g.Room == last)
            {
                if (g.RoomActive) { clock = g.RoomClock; crystal = g.CrystalHp / Math.Max(1f, g.CrystalMax); kills = g.RoomKills; zone = g.ZoneIndex; }
                continue;
            }
            if (g.Room == RoomPhase.Intro) { kind = g.RoomKind; }
            if (last == RoomPhase.Portal && g.Room == RoomPhase.None)
                Console.WriteLine("  wave {0,2}: the portal closed unused", g.Wave);
            if (g.Room == RoomPhase.Won || g.Room == RoomPhase.Lost)
            {
                string detail = kind == ChallengeKind.Defend ? "crystal " + (crystal * 100f).ToString("0") + "% left"
                    : kind == ChallengeKind.KillRush ? kills + "/" + g.RoomGoal + " kills, " + clock.ToString("0") + " s left"
                    : kind == ChallengeKind.HoldZones ? zone + "/3 zones, " + clock.ToString("0") + " s left"
                    : clock.ToString("0") + " s, " + roomHp.ToString("0") + " hp each";
                roomHp = 0f;
                Console.WriteLine("  wave {0,2}: {1,-18} {2,-4} {3}; downs so far {4}", g.Wave, Challenges.Name(kind), g.Room == RoomPhase.Won ? "WON" : "lost", detail,
                    string.Join("/", g.Players.Values.Select(p => p.Run.Downs)));
            }
            last = g.Room;
        }
        Console.WriteLine("  ended: {0} at wave {1}; rooms won {2} of {3}", g.State, g.Wave, g.RoomsWon, g.RoomsPlayed);
        host.Leave(""); c.Leave("");
        Game.ChallengesByDefault = false;
    }
}
