using System.Linq;
using System;
using System.Reflection;
using Arena;

// Aeroaegis: the vortex goes to the player the cursor is nearest, and enemies are held out of it.
static partial class Program
{
    static void AegisTest(float dt)
    {
        var s = StartBuffed(dt, 3, 0);
        Run(s, dt, 20, () => s[0].Game.Enemies.Values.Any(e => !e.Def.IsBoss && e.SpawnT <= 0f));
        var g = s[0].Game;
        var me = g.Players[1];
        var ally = g.Players.Values.First(p => p != me);
        g.Bot = false;
        foreach (var q in g.Players.Values) if (q != me && q != ally) q.Pos = new V2(40f, 40f);
        V2 c = new V2(Game.Width / 2f, Game.Height / 2f);
        me.Pos = c;
        ally.Pos = c + new V2(300f, 0f);
        var pick = typeof(Game).GetMethod("AegisTarget", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(pick.Invoke(g, new object[] { me, ally.Pos + new V2(20f, 0f) }) == ally, "a cursor near an ally sends the vortex to them");
        Check(pick.Invoke(g, new object[] { me, me.Pos + new V2(20f, 0f) }) == me, "a cursor near you keeps it on you");
        ally.Downed = true;
        Check(pick.Invoke(g, new object[] { me, ally.Pos }) == me, "a downed ally isn't picked");
        ally.Downed = false;
        // An enemy placed inside is put out at the rim and stays out while it presses in.
        var foe = g.Enemies.Values.First(e => !e.Def.IsBoss && e.SpawnT <= 0f);
        ally.AegisT = 4f;
        foe.Pos = ally.Pos + new V2(20f, 0f);
        float worst = 9999f;
        Run(s, dt, 2.5f, null, () =>
        {
            ally.AegisT = Math.Max(ally.AegisT, 2f);
            if (foe.Hp > 0f)
                worst = Math.Min(worst, foe.Pos.DistanceTo(ally.Pos));
        });
        Check(worst >= Game.AegisRadius + foe.Radius - 1f, "enemies are held outside the vortex (closest " + worst + ")");
        s[0].Leave(""); s[1].Leave("");
    }
}
