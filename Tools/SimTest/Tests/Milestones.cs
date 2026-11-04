using System.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Arena;

// Milestone upgrades: the pool, the every-10-levels pick, the Milestone Compass and the stacks' effects.
static partial class Program
{
    static void MilestoneTest(float dt)
    {
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        var none = new List<int>();
        var pool = Enumerable.Range(0, Upgrades.All.Length).Where(i => Upgrades.All[i].Rarity == Rarity.Milestone).ToList();
        Check(pool.Count == 7, "the milestone pool has 7 cards (" + pool.Count + ")");
        Check(pool.All(i => Upgrades.All[i].MaxStacks <= 5), "every milestone card has a stack limit");
        var offer = Upgrades.MilestoneOffer(new Random(1), Role.Dps, AbilityId.Dash, none);
        Check(offer != null && offer.Length == 3 && offer.Distinct().Count() == 3 && offer.All(i => Upgrades.All[i].Rarity == Rarity.Milestone), "a milestone offer is three different milestone cards");

        // Normal offers never contain milestone cards (the Compass is a normal very rare card).
        var rng = new Random(5);
        int milestoneRolled = 0, compass = 0, rolls = 0;
        for (int n = 0; n < 3000; n++)
            foreach (int id in Upgrades.RollOffer(rng, Role.Dps, AbilityId.Dash, none, 20))
            {
                rolls++;
                if (Upgrades.All[id].Rarity == Rarity.Milestone) milestoneRolled++;
                if (id == U("Milestone Compass")) compass++;
            }
        Console.WriteLine("  {0} normal cards rolled: {1} milestone, {2} Milestone Compass", rolls, milestoneRolled, compass);
        Check(milestoneRolled == 0, "milestone cards never turn up in normal level-up offers");
        Check(compass > 0 && Upgrades.All[U("Milestone Compass")].Rarity == Rarity.VeryRare && Upgrades.All[U("Milestone Compass")].MaxStacks == 3, "the Milestone Compass is a very rare normal card that stacks to 3 and does show up");

        // Maxed cards leave the pool; with everything maxed there is no milestone pick.
        var maxed = new List<int>();
        foreach (int id in pool) for (int k = 0; k < Upgrades.All[id].MaxStacks; k++) maxed.Add(id);
        Check(Upgrades.MilestoneOffer(new Random(1), Role.Dps, AbilityId.Dash, maxed) == null, "with every milestone card maxed out no milestone pick is offered");
        var one = new List<int>(maxed); one.Remove(U("Scholar"));
        var last = Upgrades.MilestoneOffer(new Random(1), Role.Dps, AbilityId.Dash, one);
        Check(last != null && last.Length == 1 && last[0] == U("Scholar"), "a pool with one card left offers just that card");

        // Stack effects.
        var st = Upgrades.Compute(new List<int> { U("Scholar"), U("Scholar"), U("Second Helping"), U("Wide Choice"), U("Reroll Cache"), U("Fortune"), U("Iron Constitution"), U("Quickening"), U("Milestone Compass") });
        Check(Math.Abs(st.XpMult - 1.3f) < 0.001f && Math.Abs(st.ExtraPickChance - 0.1f) < 0.001f && st.OfferExtra == 1 && st.BossRerolls == 1 && Math.Abs(st.RarityLuck - 0.08f) < 0.001f && st.BonusHp == 25f && Math.Abs(st.CdMult - 0.96f) < 0.001f && Math.Abs(st.MilestoneChance - 0.05f) < 0.001f,
            "each milestone card adds its stats");
        for (int extra = 0; extra <= 2; extra++)
        {
            var taken = Enumerable.Repeat(U("Wide Choice"), extra).ToList();
            var cards = Upgrades.RollOffer(new Random(3), Role.Dps, AbilityId.Dash, taken, 8);
            Check(cards.Length == 3 + extra && cards.Distinct().Count() == cards.Length && cards.All(i => i >= 0), "Wide Choice x" + extra + " offers " + (3 + extra) + " different cards");
        }
        int rare0 = 0, rare5 = 0;
        var rA = new Random(9); var rB = new Random(9);
        var luck = Enumerable.Repeat(U("Fortune"), 5).ToList();
        for (int n = 0; n < 4000; n++)
        {
            foreach (int id in Upgrades.RollOffer(rA, Role.Dps, AbilityId.Dash, none, 6)) if (Upgrades.All[id].Rarity >= Rarity.Rare) rare0++;
            foreach (int id in Upgrades.RollOffer(rB, Role.Dps, AbilityId.Dash, luck, 6)) if (Upgrades.All[id].Rarity >= Rarity.Rare) rare5++;
        }
        Console.WriteLine("  rare or better cards over 4000 offers: {0} without Fortune, {1} with 5 stacks", rare0, rare5);
        Check(rare5 > rare0 * 1.1, "Fortune makes rare cards turn up more often");

        // Level-ups on a real host game.
        var s = StartBuffed(dt, 1, 0);
        var g = s[0].Game;
        var me = g.Players[1];
        var grant = typeof(Game).GetMethod("GrantXpTo", BindingFlags.NonPublic | BindingFlags.Instance);
        Action<int> levelUp = lvl => { me.Level = lvl; me.Xp = 0f; grant.Invoke(g, new object[] { me, Defs.XpToNext(lvl) }); };
        me.PendingPicks = me.PendingMilestones = 0;
        levelUp(9);
        Check(me.Level == 10 && me.PendingMilestones == 1 && me.PendingPicks == 1, "reaching level 10 gives a milestone pick and the normal pick (level " + me.Level + ", milestones " + me.PendingMilestones + ", picks " + me.PendingPicks + ")");
        me.PendingPicks = me.PendingMilestones = 0;
        levelUp(7);
        Check(me.PendingMilestones == 0 && me.PendingPicks == 1, "level 8 is a normal level-up");
        me.PendingPicks = me.PendingMilestones = 0;
        levelUp(19);
        Check(me.Level == 20 && me.PendingMilestones == 1, "level 20 is a milestone too");
        // A big XP gain that crosses level 10 gives it once.
        me.PendingPicks = me.PendingMilestones = 0; me.Level = 8; me.Xp = 0f;
        grant.Invoke(g, new object[] { me, Defs.XpToNext(8) + Defs.XpToNext(9) + Defs.XpToNext(10) });
        Check(me.Level == 11 && me.PendingMilestones == 1 && me.PendingPicks == 3, "three level-ups across level 10: one milestone, three normal picks");

        // Scholar: more XP.
        me.Taken.Clear(); me.Taken.AddRange(Enumerable.Repeat(U("Scholar"), 2)); me.Stats = Upgrades.Compute(me.Taken);
        me.Level = 3; me.Xp = 0f; me.PendingPicks = 0;
        grant.Invoke(g, new object[] { me, 10f });
        Check(Math.Abs(me.Xp - 13f) < 0.01f, "Scholar x2 turns 10 XP into 13 (" + me.Xp + ")");

        // The Compass: a lucky milestone pick on non-milestone levels (15% with 3 stacks); Second Helping: extra normal picks.
        me.Taken.Clear(); me.Taken.AddRange(Enumerable.Repeat(U("Milestone Compass"), 3)); me.Stats = Upgrades.Compute(me.Taken);
        int ups = 0, lucky = 0;
        for (int n = 0; n < 3000; n++)
        {
            me.PendingMilestones = 0; me.PendingPicks = 0;
            levelUp(1 + n % 8);  // levels 2..9: never a milestone level
            ups++; lucky += me.PendingMilestones;
        }
        Console.WriteLine("  Compass x3: {0} lucky milestone picks over {1} level-ups ({2:0.0%}, expect 15%)", lucky, ups, lucky / (float)ups);
        Check(lucky / (float)ups > 0.12f && lucky / (float)ups < 0.18f, "the Milestone Compass x3 gives about 15% lucky milestone picks");
        me.Taken.Clear(); me.Taken.AddRange(Enumerable.Repeat(U("Second Helping"), 5)); me.Stats = Upgrades.Compute(me.Taken);
        int picks = 0;
        for (int n = 0; n < 3000; n++) { me.PendingPicks = 0; levelUp(2); picks += me.PendingPicks; }
        Console.WriteLine("  Second Helping x5: {0:0.00} picks per level-up (expect 1.50)", picks / 3000f);
        Check(picks / 3000f > 1.4f && picks / 3000f < 1.6f, "Second Helping x5 gives an extra pick about half the time");

        // The milestone pick comes up before the normal pick, can't be rerolled, and Reroll Cache pays out on the spot.
        me.Taken.Clear(); me.Stats = Upgrades.Compute(me.Taken);
        me.PendingMilestones = 1; me.PendingPicks = 1; me.PendingLegendary = me.PendingMods = me.PrizePicks = 0; me.LegendaryQueue.Clear(); me.Rerolls = 2;
        var next = typeof(Game).GetMethod("NextOffer", BindingFlags.NonPublic | BindingFlags.Instance);
        next.Invoke(g, new object[] { me });
        Check(me.OfferMilestone && me.Offer != null && me.Offer.All(i => Upgrades.All[i].Rarity == Rarity.Milestone), "the milestone offer comes before the normal pick");
        g.RerollOffer();
        Check(me.Rerolls == 2 && me.OfferMilestone, "a milestone offer can't be rerolled");
        int cache = U("Reroll Cache");
        me.Offer = new[] { cache, U("Scholar"), U("Fortune") };
        g.State = MatchState.Intermission;
        var pick = typeof(Game).GetMethod("HostPick", BindingFlags.NonPublic | BindingFlags.Instance);
        pick.Invoke(g, new object[] { 1, 0, 0 });
        Check(me.Rerolls == 5 && me.PendingMilestones == 0 && me.Stats.BossRerolls == 1, "Reroll Cache gives 3 rerolls at once and 1 more per boss (rerolls " + me.Rerolls + ")");
        Check(!me.OfferMilestone && me.Offer != null && me.PendingPicks == 1, "after the milestone pick the normal offer follows");

        // The friend's screen learns about it (protocol 141).
        me.PendingMilestones = 2; me.OfferMilestone = true; me.Offer = new[] { U("Scholar"), U("Fortune"), U("Quickening") };
        g.Bot = false;  // (the bot would pick the offer before it was sent)
        typeof(Game).GetMethod("SendSnapshot", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(g, null);
        for (int k = 0; k < 40; k++) { Thread.Sleep(5); s[1].Poll(); }
        var seen = s[1].Game.Players.ContainsKey(1) ? s[1].Game.Players[1] : null;
        Check(seen != null && seen.PendingMilestones == 2 && seen.OfferMilestone, "a friend sees your milestone picks and that the offer is a milestone one (client sees " + (seen == null ? "nothing" : seen.PendingMilestones + " / " + seen.OfferMilestone) + ", host has " + me.PendingMilestones + " / " + me.OfferMilestone + ", state " + g.State + ")");
        s[0].Leave(""); s[1].Leave("");
    }
}
