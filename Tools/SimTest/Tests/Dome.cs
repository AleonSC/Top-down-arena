using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Fortress's dome: enemies and their shots stay out, player shots and players cross.
static partial class Program
{
    static void DomeTest(float dt)
    {
        domesAllowed = true;
        var s = StartBuffed(dt, 3, 0);
        Run(s, dt, 20, () => s[0].Game.Enemies.Values.Any(e => !e.Def.IsBoss && e.SpawnT <= 0f));
        var g = s[0].Game;
        var me = g.Players[1];
        s[0].Game.Bot = false;
        foreach (var q in g.Players.Values) if (q != me) q.Pos = new V2(40f, 40f);
        V2 c = new V2(Game.Width / 2f, Game.Height / 2f);
        me.Pos = c;
        me.DomePos = c;
        me.DomeT = 6f;
        Check(g.InDome(c) && !g.InDome(c + new V2(Game.DomeRadius + 5f, 0f)), "the dome covers its radius");
        // An enemy shot dropped inside is erased; one of the player's flying across survives.
        int enemyId = 900001, mineId = 900002;
        g.Bullets[enemyId] = new Bullet { Id = enemyId, Kind = 10, Pos = c + new V2(100f, 0f), Vel = new V2(10f, 0f), Life = 3f, Radius = 4f, Dmg = 5f };
        g.Bullets[mineId] = new Bullet { Id = mineId, Kind = Defs.DpsShot, Pos = c + new V2(100f, 20f), Vel = new V2(10f, 0f), Life = 30f, Radius = 4f, Dmg = 5f, Owner = 1 };
        // An enemy placed inside is put out at the edge and stays out while pushed toward the middle.
        var foe = g.Enemies.Values.FirstOrDefault(e => !e.Def.IsBoss);
        Check(foe != null, "there is an enemy to test with");
        foe.Pos = c + new V2(30f, 0f);
        float worst = 9999f;
        Run(s, dt, 3f, null, () =>
        {
            me.DomeT = Math.Max(me.DomeT, 5f);
            me.Pos = c;
            if (foe.Hp > 0f)
                worst = Math.Min(worst, foe.Pos.DistanceTo(c));
        });
        Check(!g.Bullets.ContainsKey(enemyId), "an enemy shot inside the dome is erased");
        Check(g.Bullets.ContainsKey(mineId), "player shots aren't erased by the dome");
        Check(worst >= Game.DomeRadius + foe.Radius, "enemies are held outside the dome (closest " + worst + ")");
        // It runs out.
        me.DomeT = 0.2f;
        Run(s, dt, 0.5f, null);
        Check(!g.InDome(c), "the dome ends when its time is up");
        domesAllowed = false;
        s[0].Leave(""); s[1].Leave("");
    }
}
