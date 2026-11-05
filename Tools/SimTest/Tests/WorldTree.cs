using System;
using System.Linq;
using System.Reflection;
using Arena;

// World Tree (Core/WorldTree.cs): a 100 px zone for 15 s that sends a wave of healing butterflies to each ally every 1.9 s.
static partial class Program
{
    static void WorldTreeTest(float dt)
    {
        var s = StartBuffed(dt, 3, 0);
        Run(s, dt, 2, null);
        var g = s[0].Game;
        g.Bot = false; s[1].Game.Bot = false;
        var me = g.Players[1];
        var ally = g.Players.Values.First(p => p != me);
        var other = g.Players.Values.FirstOrDefault(p => p != me && p != ally);
        V2 c = new V2(Game.Width / 2f, Game.Height / 2f);
        foreach (var q in g.Players.Values) { g.DevSetGod(q.Id, true); q.MaxHp = 1000f; q.Hp = 100f; q.Downed = false; }
        V2 allyAt = c + new V2(300f, 0f), otherAt = c + new V2(-300f, 0f);  // both well outside the tree's 100 px
        me.Pos = c; ally.Pos = allyAt; if (other != null) other.Pos = otherAt;
        var add = typeof(Game).GetMethod("AddZone", BindingFlags.NonPublic | BindingFlags.Instance);
        var tree = (Zone)add.Invoke(g, new object[] { ZoneKind.Grove, me, c, Game.WorldTreeRadius, Game.WorldTreeTime });
        float allyHp = ally.Hp, gain = 0f, maxFly = 0f, firstFly = -1f;
        int hits = 0, zoneGone = -1;
        bool aimed = true, onClient = false;
        Run(s, dt, 16f, null, () =>
        {
            me.Pos = c; ally.Pos = allyAt; if (other != null) other.Pos = otherAt;
            foreach (var q in s[1].Game.Players.Values) if (q.Id == ally.Id) q.Pos = allyAt;
            var fl = g.Minions.Values.Where(m => m.Kind == MinionKind.Butterfly).ToList();
            maxFly = Math.Max(maxFly, fl.Count);
            if (fl.Count > 0 && firstFly < 0f) firstFly = tree.Duration - tree.T;
            aimed &= fl.All(m => m.LinkTo == ally.Id || (other != null && m.LinkTo == other.Id));
            onClient |= s[1].Game.Minions.Values.Any(m => m.Kind == MinionKind.Butterfly);
            if (ally.Hp - allyHp > 5f) { hits++; gain += ally.Hp - allyHp; }
            allyHp = ally.Hp;
            if (zoneGone < 0 && !g.Zones.ContainsKey(tree.Id)) zoneGone = 1;
        });
        float expected = 7 * Game.WorldTreeHeal;
        Console.WriteLine("  world tree: first butterfly at {0:0.00} s, up to {1} at once, {2} heals on the ally (gain {3:0}, expected {4:0}), seen on the friend's screen {5}", firstFly, maxFly, hits, gain, expected, onClient);
        Check(Game.WorldTreeRadius == 100f && Game.WorldTreeTime == 15f, "the tree is 100 px wide and lasts 15 s");
        Check(firstFly > 0.8f && firstFly < 1.1f, "the first wave leaves at 0.9 s");
        Check(maxFly >= g.Players.Count - 1, "a wave is one butterfly per ally (" + (g.Players.Count - 1) + " here)");
        Check(aimed, "every butterfly flies to an ally, not the caster");
        Check(onClient, "butterflies are seen on the friend's screen too");
        Check(hits == 7 && Math.Abs(gain - expected) < 10f, "seven waves of 14 each reach an ally standing out in the open");
        Check(zoneGone == 1 && !g.Minions.Values.Any(m => m.Kind == MinionKind.Butterfly), "the tree and its butterflies are gone after 15 s");
        Check(Game.WorldTreeIsMastered(new Zone { Duration = Game.WorldTreeTimeMastered }) && !Game.WorldTreeIsMastered(tree), "the capstone lasts 20 s");
        s[0].Leave(""); s[1].Leave("");
    }
}
