using System;
using System.Collections.Generic;
using Arena;

static partial class Program
{
    /// The Thug's charged punch (ThugPunch.cs): it plants and winds up only when you're close, the jab lands if you stay in
    /// its wedge, and a step to the side during the wind-up dodges it.
    static void ThugPunchTest(float dt)
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

        // Standing still: it walks in, plants, and its punches land.
        V2 home = new V2(800f, 450f);
        var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Thug, home + new V2(-400f, 0f), 0f, 0, false });
        e.SpawnT = 0f; e.Hp = e.MaxHp = 1e6f; e.Traits = 0;
        int punches = 0, hits = 0; float farthest = 0f, moved = 0f; byte last = 0; V2 lastPos = e.Pos;
        Run(s, dt, 12f, null, () =>
        {
            Alone(g, e, queueField);
            bool hurt = me.Hp < me.MaxHp - Game.PunchDamage + 0.5f;
            me.Pos = home; me.Hp = me.MaxHp; me.InvulnT = 0f;
            friend.Pos = new V2(150f, 820f); friend.Downed = true;
            if (e.LarderStage == Game.LarderWinding && last == Game.LarderIdle)
                farthest = Math.Max(farthest, e.Pos.DistanceTo(me.Pos));
            if (e.LarderStage == Game.LarderActing && last != Game.LarderActing) { punches++; if (hurt) hits++; }
            if (e.LarderStage != Game.LarderIdle && last != Game.LarderIdle) { moved += e.Pos.DistanceTo(lastPos); }
            last = e.LarderStage; lastPos = e.Pos;
        });
        Console.WriteLine("  thug 12 s: punches {0}, that hit {1}, farthest wind-up start {2:0} px, moved while punching {3:0.0} px", punches, hits, farthest, moved);
        Check(punches >= 3, "it punches again and again while you stand in front of it");
        Check(hits >= punches - 1, "its punches land on you when you stay in the wedge");
        Check(farthest <= Game.PunchTrigger + e.Radius + 10f, "it only winds up when you're close");
        Check(moved < 8f, "it plants its feet while it punches");

        // Sidestepping: as soon as it winds up, step out of the wedge; the jab misses.
        e.LarderStage = Game.LarderIdle; e.LarderCd = 0f;
        me.Pos = home; e.Pos = home + new V2(-45f, 0f);
        float prevHp = me.Hp; int dodged = 0, tried = 0; V2 side = home; bool stepping = false;
        Run(s, dt, 6f, null, () =>
        {
            Alone(g, e, queueField); friend.Downed = true; me.InvulnT = 0f;
            if (e.LarderStage == Game.LarderWinding && !stepping) { stepping = true; side = e.Pos + V2.FromAngle(e.Facing + MathX.Pi / 2f) * 90f; }
            if (stepping) me.Pos = side;
            else me.Pos = home;
            if (e.LarderStage == Game.LarderActing && last != Game.LarderActing) { tried++; if (me.Hp >= prevHp - 1f) dodged++; }
            prevHp = me.Hp;
            if (e.LarderStage == Game.LarderIdle && last == Game.LarderActing) { stepping = false; e.Pos = home + new V2(-45f, 0f); }
            if (me.Hp < 60f) { me.Hp = me.MaxHp; prevHp = me.Hp; }
            last = e.LarderStage;
        });
        Console.WriteLine("  sidestep: {0} of {1} punches dodged", dodged, tried);
        Check(tried >= 2 && dodged == tried, "a step to the side during the wind-up dodges the punch");
        e.Hp = 0f; Run(s, dt, 0.3f);
    }

    /// Keeps the Thug the only enemy: the wave's later spawns would hurt the player too.
    static void Alone(Game g, Enemy e, System.Reflection.FieldInfo queueField)
    {
        ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
        foreach (var o in g.Enemies.Values) if (o != e) o.Hp = 0f;
        g.Bullets.Clear();
    }
}
