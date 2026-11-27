using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Hundred Paper Cranes (HundredPaperCranes.cs): key 2 of the DPS Ninja's Ultimate row reaching the run; 100 cranes in a ring
// that follows you for the first second (none leave); then one released every 0.03 s, the first ones for the healthiest
// enemy and later ones spreading out; all 100 hitting for 10 (1000 in all) by about 4 s; cranes waiting in the ring with no
// enemy in reach; a crane whose enemy dies picking another; the end on time and on going down; the client seeing the ring
// (HpcT, HpcN) and the cranes in flight; mastered (130); Thousand Folds; the bot's rule.
static partial class Program
{
    static void HundredPaperCranesTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        // --- The lobby: key 2 of the DPS Ninja's Ultimate row ---------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.HundredPaperCranes);
        Check(def.Name == "Hundred Paper Cranes" && def.Cooldown == 75f && def.Choice == AbilityDef.Ult && def.UltKey == 2 && def.Role == Role.Dps && def.Kit == Kit.Ninja && def.Capstone != null,
            "Hundred Paper Cranes is the DPS Ninja's ultimate on key 2, 75 s");
        Check(Upgrades.Ultimate(Role.Dps, Kit.Ninja, 2) == AbilityId.HundredPaperCranes && Upgrades.Ultimate(Role.Dps, Kit.Ninja, 1) == AbilityId.ForbiddenPath
            && Upgrades.Ultimate(Role.Dps, Kit.Ninja) == AbilityId.DeathLotus && Upgrades.ClampUlt(Role.Dps, Kit.Ninja, 2) == 2,
            "key 2 is Hundred Paper Cranes; Forbidden Path stays on key 1 and Death Lotus the default");
        var card = Upgrades.All.FirstOrDefault(u => u.Name == "Thousand Folds");
        Check(card != null && card.RequiresAbility == AbilityId.HundredPaperCranes && card.Rarity == Rarity.Rare, "its Rare card is Thousand Folds");
        float h0;
        V2 s0 = Game.HpcSlot(5, 0.01f, out h0), s1 = Game.HpcSlot(5, 1.5f, out h0);
        Check(s0.Length < 15f && s1.Length > Game.HpcRingMin - 8f && s1.Length < Game.HpcRingMin + Game.HpcRingSpan + 8f,
            "a crane starts at your body and swirls out into the ring, 60 to 130 px round you");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Cranes", Role.Dps, 0, 0, Kit.Ninja, 0, 2);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps, 0, 0, Kit.Ninja, 0, 2);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2 && !host.Game.Running);
        int cid = host.Lobby.Values.First(e => e.Id != 1).Id;
        Run(s, dt, 2, () => host.Lobby[cid].Ult == 2 && client.Lobby[1].Ult == 2);
        Check(host.Lobby[1].UltId == AbilityId.HundredPaperCranes, "the pick reaches the host");
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        var cg = client.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.HundredPaperCranes && cg.Players[cid].Slots[Upgrades.UltSlot].Id == AbilityId.HundredPaperCranes,
            "in the run both players' ultimate is Hundred Paper Cranes, on both games");

        var me = g.Players[1];
        var ally = g.Players[cid];
        V2 home = new V2(900f, 700f);
        var keep = new HashSet<Enemy>();
        bool clearShots = false;
        Action<float> step = secs =>
        {
            int n = Math.Max(1, (int)Math.Round(secs / dt));
            for (int f = 0; f < n; f++)
            {
                foreach (var id in g.Enemies.Keys.ToList())
                    if (!keep.Contains(g.Enemies[id]))
                        g.Enemies.Remove(id);
                foreach (var e in keep)
                    if (g.Enemies.ContainsKey(e.Id))
                    {
                        e.FireT = 99f;
                        e.FrozenT = 99f;  // (they stand still)
                    }
                foreach (var id in g.Bullets.Keys.ToList())
                    if (g.Bullets[id].Kind != Defs.PaperCrane || clearShots)
                        g.Bullets.Remove(id);  // (only the cranes fly: no enemy shots, no weapon)
                me.Pos = home;
                ally.Pos = home + new V2(0f, 1500f);
                me.Hp = me.MaxHp;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = home + new V2(300f, 0f) });
            }
        };
        Action reset = () =>
        {
            clearShots = true; step(dt); clearShots = false;
            me.Hp = me.MaxHp; me.Downed = false; me.UltT = 0f; me.HpcT = 0f; me.HpcN = 0; me.InvulnT = 0f;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
            me.MasteryXp.Remove(AbilityId.HundredPaperCranes);
            me.HpcHits = 0; me.HpcDealt = 0f;
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);  // (the wave never runs out: its end would end it)
            g.State = MatchState.Fighting;
            foreach (var e in keep) g.Enemies.Remove(e.Id);
            keep.Clear();
        };
        Func<V2, float, Enemy> foe = (at, hp) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.Hp = e.MaxHp = hp; e.SpawnT = 0f; e.Pos = e.Disp = at; e.FireT = 99f; e.FrozenT = 99f;
            keep.Add(e);
            return e;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, home + new V2(300f, 0f), false });
        Func<Enemy, int> coming = e => g.Bullets.Values.Count(b => b.Kind == Defs.PaperCrane && b.Crane && b.HomeOn == e.Id);

        // --- The ring, then the release: the healthiest first, then spread out ----------------------------------------------
        reset();
        var big = foe(home + new V2(320f, -120f), 200f);
        var mid = foe(home + new V2(360f, 60f), 120f);
        var small = foe(home + new V2(300f, 200f), 60f);
        step(0.05f);
        cast();
        Check(Game.HpcOn(me) && me.HpcN == Game.HpcCount && Math.Abs(me.UltT - Game.HpcEnd) < 1e-3f, "casting folds 100 cranes into the ring, for 4 s");
        step(0.9f);
        Check(me.HpcN == Game.HpcCount && g.Bullets.Values.All(b => b.Kind != Defs.PaperCrane), "none leave in the first second (the gather)");
        step(0.25f);
        int gone = Game.HpcCount - me.HpcN;
        Console.WriteLine("  cranes: 0.15 s into the release {0} gone; coming at big {1}, mid {2}, small {3}", gone, coming(big), coming(mid), coming(small));
        Check(gone >= 4 && gone <= 8 && coming(big) == gone, "one crane leaves every 0.03 s, and the first ones all go for the healthiest enemy");
        step(0.6f);
        Check(coming(mid) > 0 && coming(small) > 0, "once the healthiest is spoken for they spread to the others");
        step(Game.HpcEnd);
        Console.WriteLine("  cranes: hits {0}, dealt {1:0}, big {2:0}/200, mid {3:0}/120, small {4:0}/60", me.HpcHits, me.HpcDealt, big.Hp, mid.Hp, small.Hp);
        Check(!Game.HpcOn(me) && me.HpcT == 0f && me.UltT == 0f && me.HpcN == 0, "it ends after 4 s");
        Check(me.HpcHits == 38 && Math.Abs(me.HpcDealt - 380f) < 1f && big.Hp <= 0f && mid.Hp <= 0f && small.Hp <= 0f,
            "each crane hits for 10, and with all three enemies dead (380 health) no more fly");

        // All 100 land on a crowd that can take them: 1000 damage.
        reset();
        for (int i = 0; i < 5; i++) foe(home + new V2(280f + 40f * i, -160f + 80f * i), 1e5f);
        step(0.05f);
        cast();
        step(Game.HpcEnd + 1.5f);
        Check(me.HpcHits == Game.HpcCount && Math.Abs(me.HpcDealt - Game.HpcCount * Game.HpcDmg) < 1f, "all 100 cranes land: 1000 damage");

        // No enemy in reach: they wait in the ring, then scatter.
        reset();
        var far = foe(home + new V2(Game.HpcSeek + 200f, 0f), 1e5f);
        step(0.05f);
        cast();
        step(2f);
        Check(me.HpcN == Game.HpcCount && coming(far) == 0, "with no enemy within 700 px the cranes wait in the ring");
        far.Pos = far.Disp = home + new V2(400f, 0f);
        step(0.2f);
        Check(me.HpcN < Game.HpcCount && coming(far) > 0, "and leave once one comes in reach");

        // A crane whose enemy dies picks another.
        reset();
        var a = foe(home + new V2(350f, 0f), 1e5f);
        var b2 = foe(home + new V2(350f, 250f), 50f);
        step(0.05f);
        cast();
        step(1.15f);
        Check(coming(a) > 0 && coming(b2) == 0, "the first cranes all go for the healthiest");
        a.Hp = 0f; g.Enemies.Remove(a.Id); keep.Remove(a);
        step(0.05f);
        Check(coming(b2) > 0, "with it dead, the cranes on the way to it pick another");
        step(Game.HpcEnd);

        // Going down ends it.
        reset();
        foe(home + new V2(350f, 0f), 1e5f);
        cast();
        step(0.5f);
        me.Downed = true;
        step(0.1f);
        Check(me.HpcT == 0f && me.HpcN == 0, "going down ends it");
        me.Downed = false;

        // Mastered: 130 cranes. Thousand Folds: 20 more, 15% harder.
        reset();
        me.MasteryXp[AbilityId.HundredPaperCranes] = Game.MasteryXpFor[Game.MasteryMax - 1];
        for (int i = 0; i < 5; i++) foe(home + new V2(280f + 40f * i, -160f + 80f * i), 1e5f);
        cast();
        Check(me.HpcN == Game.HpcCountMastered, "mastered: 130 cranes");
        step(Game.HpcEnd + 1.5f);
        Check(me.HpcHits == Game.HpcCountMastered, "mastered: all 130 are released in the same 3 s and land");
        reset();
        me.Stats.HpcMore += Game.HpcFoldsMore; me.Stats.HpcDmgMult += Game.HpcFoldsDmg;
        for (int i = 0; i < 5; i++) foe(home + new V2(280f + 40f * i, -160f + 80f * i), 1e5f);
        cast();
        Check(me.HpcN == Game.HpcCount + Game.HpcFoldsMore, "Thousand Folds: 20 more cranes");
        step(Game.HpcEnd + 1.5f);
        Check(Math.Abs(me.HpcDealt - me.HpcHits * Game.HpcDmg * me.Stats.DmgMult * 1.15f) < 1f, "Thousand Folds: they hit 15% harder");
        me.Stats.HpcMore -= Game.HpcFoldsMore; me.Stats.HpcDmgMult -= Game.HpcFoldsDmg;

        // The client sees the ring (HpcT, HpcN) and the cranes in flight.
        reset();
        var cally = cg.Players[1];
        foe(home + new V2(350f, 0f), 1e5f);
        step(0.05f);
        cast();
        step(0.5f);
        Run(s, dt, 1, () => cally.HpcT > 0.4f);
        Check(Game.HpcOn(cally) && cally.HpcN == Game.HpcCount, "the client's game sees the ring (HpcT, HpcN)");
        step(1f);
        Run(s, dt, 1, () => cg.Bullets.Values.Any(b => b.Kind == Defs.PaperCrane));
        Check(cally.HpcN < Game.HpcCount && cg.Bullets.Values.Any(b => b.Kind == Defs.PaperCrane), "and the cranes in flight");
        step(Game.HpcEnd);

        // The bot's rule.
        Check(def.Bot(new BotSense { Me = me, NearEnemies = 5 }) && !def.Bot(new BotSense { Me = me, NearEnemies = 1 }), "a bot casts it in a crowd");

        host.Leave(""); client.Leave("");
    }
}
