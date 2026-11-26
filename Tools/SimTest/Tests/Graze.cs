using System;
using Arena;

// Graze (Core/Graze.cs): an enemy shot skimming past a player charges their ultimate. A near miss counts once (its count goes
// up and the ultimate's recharge drops by Game.GrazeUltCut), a far miss and a hit don't, a shot grazes once, two at once count
// once (the gap), not while invulnerable, and a client's own game takes the time off its ultimate (Msg.Graze).
static partial class Program
{
    static void GrazeTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Host", Role.Dps, 0, 0, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        var me = g.Players[1];
        var friend = g.Players[2];
        var friendOwn = client.Game.Me;
        V2 mePos = new V2(400f, 300f), friendPos = new V2(400f, 700f);

        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var e in g.Enemies.Values) { e.FireT = 99f; e.Pos = new V2(1500f, 950f); }
                me.Pos = mePos; friend.Pos = friendPos; friendOwn.Pos = friendPos;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = mePos + new V2(100f, 0f) });
                client.Game.FixedStep(dt, new PlayerInput { AimAt = friendPos + new V2(100f, 0f) });
                if (f % 2 == 0) Nap(1);
            }
            for (int k = 0; k < 5; k++) { Nap(2); client.Poll(); }
        };
        // A shot flying left to right past `at`, `miss` px from its middle (0: straight through).
        Func<V2, float, Bullet> pass = (at, miss) =>
            (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, at + new V2(-120f, miss), new V2(400f, 0f), 0f, 3f, 5f, 0, false, 0f });
        Func<Player, float> ult = p => p.Slots[Upgrades.UltSlot].Timer;
        Action resetUlt = () =>
        {
            foreach (var p in new[] { me, friend, friendOwn })
            {
                p.Slots[Upgrades.UltSlot].Charges = 0;
                p.Slots[Upgrades.UltSlot].Timer = 40f;
                p.InvulnT = 0f;
            }
            g.Bullets.Clear();
        };
        step(0.2f);

        // A near miss: counted once, the ultimate comes GrazeUltCut sooner.
        resetUlt();
        var b = pass(mePos, 0f); float r = me.Def.HitR + b.Radius; g.Bullets.Remove(b.Id);
        b = pass(mePos, r + Game.GrazeReach * 0.5f);
        float before = ult(me); int n0 = me.Grazes;
        step(0.6f);
        float spent = before - ult(me);
        Check(me.Grazes == n0 + 1, "a shot passing within the graze reach counts one graze (" + (me.Grazes - n0) + ")");
        Check(Math.Abs(spent - (0.6f + Game.GrazeUltCut)) < 0.06f, "a graze takes " + Game.GrazeUltCut + " s off the ultimate (" + spent.ToString("0.00") + " s over 0.6 s)");
        Check(b.Grazed, "the shot is marked as grazed");

        // A far miss: nothing.
        resetUlt(); n0 = me.Grazes;
        pass(mePos, r + Game.GrazeReach + 8f);
        step(0.6f);
        Check(me.Grazes == n0, "a shot passing outside the graze reach doesn't count");

        // Two shots side by side: the gap lets only one count.
        resetUlt(); n0 = me.Grazes;
        pass(mePos, r + 4f); pass(mePos, -(r + 4f));
        step(0.6f);
        Check(me.Grazes == n0 + 1, "two grazes at once count once (" + (me.Grazes - n0) + ")");

        // Invulnerable (a dodge, the grace after a hit): nothing.
        resetUlt(); n0 = me.Grazes;
        pass(mePos, r + 6f);
        for (int f = 0; f < 0.6f / dt; f++) { me.InvulnT = 1f; step(dt); }
        Check(me.Grazes == n0, "no graze while invulnerable");

        // A ready ultimate stays ready, and the count still goes up.
        resetUlt(); n0 = me.Grazes;
        me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
        pass(mePos, r + 6f);
        step(0.6f);
        Check(me.Grazes == n0 + 1 && me.Slots[Upgrades.UltSlot].Charges == 1 && ult(me) == 0f, "a ready ultimate is left alone");

        // The client's player: its own game takes the time off (Msg.Graze), and the host's copy too.
        resetUlt(); n0 = friendOwn.Grazes; int h0 = friend.Grazes;
        before = ult(friendOwn); float hostBefore = ult(friend);
        pass(friendPos, r + 6f);
        step(0.6f);
        Check(friendOwn.Grazes == n0 + 1 && friend.Grazes == h0 + 1, "a client's graze reaches its own game (" + (friendOwn.Grazes - n0) + ") and the host's copy (" + (friend.Grazes - h0) + ")");
        Check(Math.Abs(before - ult(friendOwn) - (0.6f + Game.GrazeUltCut)) < 0.1f, "the client's own ultimate comes " + Game.GrazeUltCut + " s sooner (" + (before - ult(friendOwn)).ToString("0.00") + ")");
        Check(Math.Abs(hostBefore - ult(friend) - (0.6f + Game.GrazeUltCut)) < 0.1f, "the host's copy of it too (" + (hostBefore - ult(friend)).ToString("0.00") + ")");

        host.Leave(""); client.Leave("");
    }
}
