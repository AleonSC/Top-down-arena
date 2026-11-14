using System;
using System.Collections.Generic;
using Arena;

static partial class Program
{
    /// The riot Guard's baton (GuardSwing.cs): close to you it swings without stopping, forehand then backhand, each swing
    /// landing BatonDamage in its arc, and it never swings at someone out of reach.
    static void GuardSwingTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        var s = StartPlain(dt, 23);
        var g = s[0].Game;
        var me = g.Players[1]; var friend = g.Players[2];
        Run(s, dt, 8f, () => g.State == MatchState.Fighting);
        ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
        foreach (var e0 in g.Enemies.Values) e0.Hp = 0f;
        Run(s, dt, 0.2f);
        g.Bullets.Clear();

        // Far away: it walks in and doesn't swing until it's close.
        V2 home = new V2(800f, 450f);
        var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Guard, home + new V2(-400f, 0f), 0f, 0, false });
        e.SpawnT = 0f; e.Hp = e.MaxHp = 1e6f;
        float farthestSwing = 0f, still = 0f;
        int fore = 0, back = 0, hits = 0; byte last = 0; V2 lastPos = e.Pos;
        Run(s, dt, 8f, null, () =>
        {
            me.Pos = home; me.Hp = me.MaxHp; me.InvulnT = 0f;
            friend.Pos = new V2(150f, 820f); friend.Downed = true;
            if (e.LarderStage != last && (e.LarderStage == Game.BatonActing || e.LarderStage == Game.BatonActingBack))
            {
                if (e.LarderStage == Game.BatonActing) fore++; else back++;
                farthestSwing = Math.Max(farthestSwing, e.Pos.DistanceTo(me.Pos));
                if (me.Hp < me.MaxHp) hits++;
            }
            if (e.LarderStage != Game.LarderIdle && e.Pos.DistanceTo(lastPos) < 0.01f) still += dt;
            last = e.LarderStage; lastPos = e.Pos;
        });
        Console.WriteLine("  guard 8 s: forehands {0}, backhands {1}, swings that hit {2}, farthest swing start {3:0} px, still while swinging {4:0.00} s",
            fore, back, hits, farthestSwing, still);
        Check(fore >= 4 && back >= 4 && Math.Abs(fore - back) <= 1, "it swings forehand and backhand in turn");
        Check(hits >= (fore + back) / 2, "its swings land on you when you're in the arc");
        Check(farthestSwing <= Game.BatonTrigger + e.Radius + 30f, "it only swings when you're close");
        Check(still < 0.5f, "it keeps walking while it swings (no plant)");
        e.Hp = 0f; Run(s, dt, 0.3f);
    }
}
