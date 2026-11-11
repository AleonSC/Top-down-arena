using System;
using System.Collections.Generic;
using Arena;

// The DPS role passive, Momentum: kills stack speed and fire rate (+3% each, up to 5), a stack run ages out after 4 s without
// a kill, going down drops it, and only a DPS (not a Tank or a Wildcard) gets it.
static partial class Program
{
    static void MomentumTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var kill = typeof(Game).GetMethod("MomentumKill", flags);
        var tick = typeof(Game).GetMethod("TickMomentum", flags);
        var fireRate = typeof(Game).GetMethod("FireRate", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        Func<Role, Kit, Session> Make = (role, kit) =>
        {
            var host = new Session { AutoStart = 1 };
            host.Host(nextPort++, "Mom", role, 0, 0, kit);
            host.Game.Bot = false;
            host.StartMatch();
            for (int f = 0; f < 360 && host.Game.State != MatchState.Fighting; f++) Step(host, dt);
            return host;
        };

        var dps = Make(Role.Dps, Kit.Soldier);
        var g = dps.Game; var p = g.Players[1];
        var foe = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, new V2(1400f, 800f), 0f, 0, false });
        float speed0 = p.Speed, rate0 = (float)fireRate.Invoke(g, new object[] { p });
        for (int i = 0; i < 7; i++) kill.Invoke(g, new object[] { p, foe });
        Check(p.MomentumStacks == Game.MomentumMax, "kills stack up to the cap of " + Game.MomentumMax);
        float want = 1f + Game.MomentumPer * Game.MomentumMax;
        Check(Math.Abs(p.Speed / speed0 - want) < 0.001f, "full Momentum is +" + (int)Math.Round((want - 1f) * 100f) + "% move speed");
        Check(Math.Abs((float)fireRate.Invoke(g, new object[] { p }) / rate0 - want) < 0.001f, "and the same on fire rate");

        for (int f = 0; f < (int)((Game.MomentumHold - 0.5f) / dt); f++) tick.Invoke(g, new object[] { p, dt });
        Check(p.MomentumStacks == Game.MomentumMax, "the stacks hold just under " + Game.MomentumHold + " s");
        kill.Invoke(g, new object[] { p, foe });
        for (int f = 0; f < (int)((Game.MomentumHold - 0.5f) / dt); f++) tick.Invoke(g, new object[] { p, dt });
        Check(p.MomentumStacks > 0, "a kill refreshes the timer");
        for (int f = 0; f < (int)(1f / dt); f++) tick.Invoke(g, new object[] { p, dt });
        Check(p.MomentumStacks == 0 && Math.Abs(p.Speed - speed0) < 0.001f, "without a kill they age out and speed returns to normal");

        kill.Invoke(g, new object[] { p, foe });
        p.Downed = true; tick.Invoke(g, new object[] { p, dt }); p.Downed = false;
        Check(p.MomentumStacks == 0, "going down drops the stacks");
        kill.Invoke(g, new object[] { null, foe });  // no killer: no crash

        // Round-trip over the snapshot (the client moves itself, so it needs the stacks).
        p.MomentumStacks = 3; p.MomentumT = 2.5f;
        var sync = typeof(Game).GetMethod("SyncSnapshot", flags);
        var ms = new System.IO.MemoryStream();
        sync.Invoke(g, new object[] { new NetSync(new System.IO.BinaryWriter(ms)) });
        p.MomentumStacks = 0; p.MomentumT = 0f;
        ms.Position = 0;
        sync.Invoke(g, new object[] { new NetSync(new System.IO.BinaryReader(ms)) });
        Check(p.MomentumStacks == 3 && Math.Abs(p.MomentumT - 2.5f) < 0.001f, "the stacks travel in the snapshot");

        var tank = Make(Role.Tank, Kit.Soldier); var tg = tank.Game; var tp = tg.Players[1];
        var tfoe = (Enemy)spawn.Invoke(tg, new object[] { EnemyType.Brute, new V2(1400f, 800f), 0f, 0, false });
        typeof(Game).GetMethod("MomentumKill", flags).Invoke(tg, new object[] { tp, tfoe });
        Check(tp.MomentumStacks == 0, "a Tank gets no Momentum");
    }
}
