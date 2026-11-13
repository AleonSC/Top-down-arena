using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arena;

static partial class Program
{
    /// The Riot Captain (RiotCaptain.cs, FactionMinis.cs): the Warden's faction mini-boss. Its shield blocks the front only (not while
    /// dazed), a charge into a wall dazes it (and dazed it takes more damage), the whistle brings two Taser Guards that go with it, it
    /// fights back (shots, gas), and a mini-boss room keeps its foe over the network.
    static void RiotCaptainTest(float dt)
    {
        Check(FactionMinis.For(EnemyType.Boss) == EnemyType.RiotCaptain && FactionMinis.Is(EnemyType.RiotCaptain) && !FactionMinis.Is(EnemyType.Gatekeeper),
            "the Warden's faction mini-boss is the Riot Captain; the Gatekeeper isn't a faction mini-boss");

        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", NP);
        var blocks = typeof(Game).GetMethod("BlocksShot", NP);
        var clearAdds = typeof(Game).GetMethod("ClearCaptainAdds", NP);
        var queueField = typeof(Game).GetField("spawnQueue", NP);
        var s = StartPlain(dt, 6);
        var g = s[0].Game; var cg = s[1].Game;
        var me = g.Players[1]; var friend = g.Players[2];
        Run(s, dt, 8f, () => g.State == MatchState.Fighting);
        ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
        var anchor = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(1500f, 80f), 0f, 0, false });
        foreach (var e0 in g.Enemies.Values) if (e0 != anchor) e0.Hp = 0f;
        Action hold = () => { anchor.FrozenT = 1f; anchor.FireT = 99f; anchor.Pos = new V2(1500f, 80f); friend.Downed = true; friend.Pos = new V2(150f, 820f); };
        Run(s, dt, 0.2f, null, hold);
        g.Bullets.Clear(); g.DevSetGod(1, true);

        V2 home = new V2(800f, 450f);
        me.Pos = me.Disp = home;
        var c = (Enemy)spawn.Invoke(g, new object[] { EnemyType.RiotCaptain, home + new V2(-300f, 0f), 0f, 0, false });
        c.SpawnT = 0f; c.Facing = 0f; c.T2 = 99f;  // facing you, holding its attacks
        Check(c.MaxHp > 1000f && c.Def.Mini, "it has a mini-boss's health (" + (int)c.MaxHp + ")");
        bool front = (bool)blocks.Invoke(g, new object[] { c, c.Pos + new V2(100f, 0f) });
        bool back = (bool)blocks.Invoke(g, new object[] { c, c.Pos + new V2(-100f, 0f) });
        c.Phase = Game.CapDazed;
        bool dazedFront = (bool)blocks.Invoke(g, new object[] { c, c.Pos + new V2(100f, 0f) });
        Check(front && !back && !dazedFront, "its tower shield blocks the front, not the back, and not while it's dazed");

        // A charge into the arena's wall dazes it.
        c.Pos = new V2(g.ArenaMin.X + 180f, 450f); c.Phase = Game.CapWinding; c.PhaseT = 0.02f; c.Facing = MathX.Pi; c.Hp = c.MaxHp = 1e6f;
        bool dazed = false;
        Run(s, dt, 1.2f, null, () => { hold(); me.Pos = home; dazed |= c.Phase == Game.CapDazed; });
        Check(dazed, "a shield charge into a wall leaves it dazed (phase " + c.Phase + ")");

        // The whistle: two Taser Guards, its own, gone when it is.
        c.Phase = Game.CapWhistle; c.PhaseT = 0.02f; c.T2 = 99f;
        Run(s, dt, 0.3f, null, () => { hold(); me.Pos = home; });
        var men = g.Enemies.Values.Where(e => e.Type == EnemyType.TaserGuard && e.ParentId == c.Id && e.Hp > 0f).ToList();
        Check(men.Count == Game.CaptainAdds, "the whistle brings " + Game.CaptainAdds + " Taser Guards (" + men.Count + ")");
        clearAdds.Invoke(g, new object[] { c.Id });
        Check(men.All(e => e.Hp <= 0f), "its Taser Guards go with it");

        // It fights back: buckshot and tear gas over 20 s.
        c.Pos = home + new V2(-300f, 0f); c.Phase = Game.CapWalk; c.T2 = 0.5f;
        int shots = 0, gas = 0; var seen = new HashSet<int>(); var seenGas = new HashSet<int>();
        Run(s, dt, 20f, null, () =>
        {
            hold(); me.Pos = home;
            foreach (var b in g.Bullets.Values) if (b.Source == EnemyType.RiotCaptain && seen.Add(b.Id)) shots++;
            foreach (var kv in g.Patches) if (kv.Value.Kind == PatchKind.Gas && seenGas.Add(kv.Key)) gas++;
        });
        bool clientSees = cg.Enemies.Values.Any(e => e.Type == EnemyType.RiotCaptain);
        Console.WriteLine("  riot captain 20 s: shots {0}, gas clouds {1}", shots, gas);
        Check(shots >= 14 && gas >= 3 && clientSees, "it fires buckshot and lobs tear gas, and the client sees it");
        c.Hp = 0f; clearAdds.Invoke(g, new object[] { c.Id });
        Run(s, dt, 0.3f);

        // A mini-boss room keeps its foe across the network.
        var write = typeof(Game).GetMethod("WriteRooms", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var read = typeof(Game).GetMethod("ReadRooms", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        write.Invoke(null, new object[] { bw, new List<ExpansionRoom> { new ExpansionRoom { X = 100, Y = 200, W = 800, H = 600, Kind = RoomKind.Gatekeeper, Floating = true, Mini = (byte)EnemyType.RiotCaptain } } });
        bw.Flush(); ms.Position = 0;
        var back2 = (List<ExpansionRoom>)read.Invoke(null, new object[] { new BinaryReader(ms) });
        Check(back2.Count == 1 && back2[0].Mini == (byte)EnemyType.RiotCaptain && back2[0].Kind == RoomKind.Gatekeeper, "a mini-boss room's foe goes over the network");
    }
}
