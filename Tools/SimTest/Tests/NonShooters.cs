using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

static partial class Program
{
    /// The enemies that don't shoot (NonShooters.cs): the Fuse Runner's fuse and blast, the Hookman's yank, dodge and
    /// open window, the Sapper's Tripwire Pods (arming, stepping on one, a laser, shooting one), and what the client sees.
    static void NonShootersTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);

        var s = StartPlain(dt, 19);
        var g = s[0].Game;
        var me = g.Players[1];
        var friend = g.Players[2];
        Func<EnemyType, V2, Enemy> put = (t, at) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { t, at, 0f, 0, false });
            e.SpawnT = 0f;
            return e;
        };
        Enemy anchor = null;  // a frozen Grunt in a corner holds the wave open, so the enemies keep acting
        Action quiet = () =>
        {
            ((List<EnemyType>)queueField.GetValue(g)).Clear();
            ClearSquads(g);
            if (anchor == null) { anchor = put(EnemyType.Grunt, new V2(1500f, 80f)); anchor.Hp = anchor.MaxHp = 1e7f; }
            foreach (var e0 in g.Enemies.Values) if (e0 != anchor) e0.Hp = 0f;
            Run(s, dt, 0.2f, null, () => { anchor.FrozenT = 1f; anchor.FireT = 99f; anchor.Pos = new V2(1500f, 80f); });
            g.Bullets.Clear();
        };
        Run(s, dt, 8f, () => g.State == MatchState.Fighting);
        Action<Player, V2> stand = (p, at) => { p.Pos = at; p.Hp = p.MaxHp = 5000f; };
        quiet();
        Check(g.State == MatchState.Fighting, "the test arena stays in the fight (an anchor enemy holds the wave)");
        g.DevSetGod(2, true);
        stand(friend, new V2(150f, 820f));

        // --- Fuse Runner ---------------------------------------------------------------------------------------------
        V2 home = new V2(800f, 450f);
        stand(me, home);
        var grunt = put(EnemyType.Grunt, home + new V2(-40f, 25f));
        grunt.Hp = grunt.MaxHp = 1e5f; grunt.FireT = 99f;
        var fr = put(EnemyType.FuseRunner, home + new V2(-400f, 0f));
        int frId = fr.Id;
        bool planted = false, clientSawFuse = false;
        float hp0 = me.Hp, gruntHp0 = grunt.Hp;
        Run(s, dt, 4f, () => !g.Enemies.ContainsKey(frId), () =>
        {
            me.Pos = home; grunt.Pos = home + new V2(-40f, 25f); grunt.FrozenT = 1f;
            planted |= fr.Phase == 1;
            Enemy ce; if (s[1].Game.Enemies.TryGetValue(frId, out ce) && ce.Phase == 1 && ce.PhaseT > 0f && ce.PhaseT < Game.FuseCountdown) clientSawFuse = true;
        });
        Console.WriteLine("  Fuse Runner: planted {0}, blew up {1}; you lost {2:0}, the Grunt beside you {3:0}", planted, !g.Enemies.ContainsKey(frId), hp0 - me.Hp, gruntHp0 - grunt.Hp);
        Check(planted && !g.Enemies.ContainsKey(frId) && me.Hp < hp0 && grunt.Hp < gruntHp0, "a Fuse Runner plants near you and its blast hurts you and the enemy next to you");
        Check(clientSawFuse, "the client sees the fuse burning down (its timer is in the snapshot)");

        // Shot during the fuse: it blows up where it stands, at once.
        fr = put(EnemyType.FuseRunner, home + new V2(-100f, 0f));
        frId = fr.Id;
        Run(s, dt, 1f, () => fr.Phase == 1, () => { me.Pos = home; });
        V2 fat = fr.Pos;
        hurt.Invoke(g, new object[] { fr, 1000f, 1 });
        gruntHp0 = grunt.Hp;
        grunt.Pos = fat + new V2(20f, 0f);
        Run(s, dt, 0.1f, null, () => { me.Pos = home + new V2(300f, 0f); grunt.Pos = fat + new V2(20f, 0f); grunt.FrozenT = 1f; });
        Check(!g.Enemies.ContainsKey(frId) && grunt.Hp < gruntHp0, "shot during its fuse, a Fuse Runner blows up early and hurts the enemies by it");
        grunt.Hp = 0f;
        quiet();

        // --- Hookman -------------------------------------------------------------------------------------------------
        stand(me, home);
        var hk = put(EnemyType.Hookman, home + new V2(280f, 0f));
        hk.PhaseT = 0.2f;
        int hkId = hk.Id;
        bool clientSawHarpoon = false;
        Run(s, dt, 1.2f, () => hk.Phase == 3, () =>
        {
            if (hk.Phase < 2) me.Pos = home;
            Enemy ce; if (s[1].Game.Enemies.TryGetValue(hkId, out ce) && ce.Phase == 2 && ce.T2 > 20f) clientSawHarpoon = true;
        });
        Run(s, dt, 0.6f, null, () => { hk.Pos = home + new V2(280f, 0f); });
        float pulled = me.Pos.X - home.X;
        Console.WriteLine("  Hookman: hooked {0}, pulled {1:0} px toward it", hk.Phase == 3 || hk.Phase == 0, pulled);
        Check(pulled > 120f, "a Hookman's harpoon yanks you toward it");
        Check(clientSawHarpoon, "the client sees the harpoon fly out (its reach is in the snapshot)");

        // A dodge's invulnerability lets it pass: it misses and is open, taking 50% more.
        stand(me, home);
        hk.Phase = 0; hk.PhaseT = 0.1f; hk.T2 = 0f; hk.Pos = home + new V2(280f, 0f);
        Run(s, dt, 1.2f, () => hk.Phase >= 3, () => { me.Pos = home; hk.Pos = home + new V2(280f, 0f); if (hk.Phase == 2) me.InvulnT = 0.2f; });
        bool open = hk.Phase == 4;
        hk.Hp = hk.MaxHp = 1000f;
        hurt.Invoke(g, new object[] { hk, 100f, 0 });
        Console.WriteLine("  Hookman dodged: open {0}, a 100 hit took {1:0}", open, 1000f - hk.Hp);
        Check(open && Math.Abs((1000f - hk.Hp) - 100f * Game.HookOpenMult) < 1f, "a dodged harpoon misses and leaves the Hookman open (+50% damage)");
        hk.Hp = 0f;
        quiet();

        // --- Sapper --------------------------------------------------------------------------------------------------
        stand(me, new V2(300f, 200f));
        var sp = put(EnemyType.Sapper, new V2(1200f, 600f));
        int spId = sp.Id;
        Run(s, dt, 9f, null, () => { me.Pos = new V2(300f, 200f); sp.Pos = new V2(1200f, 600f) + new V2((float)Math.Cos(g.AnimT) * 150f, (float)Math.Sin(g.AnimT) * 150f); });
        var pods = g.Enemies.Values.Where(e => e.Type == EnemyType.SapperMine).ToList();
        Console.WriteLine("  Sapper: {0} pods out after 9 s, {1} armed; foes counted {2}", pods.Count, pods.Count(p => p.Phase == 1), g.Foes());
        Check(pods.Count == Game.SapperMax && pods.Count(p => p.Phase == 1) >= 3, "a Sapper keeps up to four pods out, and they arm");
        Check(g.Foes() == 2, "pods don't count as enemies left in the wave (only the Sapper and the anchor do)");
        sp.Hp = 0f;
        Run(s, dt, 0.1f, null, () => { me.Pos = new V2(300f, 200f); });
        foreach (var pd in g.Enemies.Values.Where(e => e.Type == EnemyType.SapperMine).ToList()) g.Enemies.Remove(pd.Id);

        // Stepping on an armed pod: a beep, then a blast.
        Func<V2, Enemy> pod = p0 => { var e = put(EnemyType.SapperMine, p0); e.Hp = e.MaxHp = 1f; e.T2 = 2f; e.Phase = 1; return e; };
        var a = pod(new V2(600f, 450f));
        hp0 = me.Hp;
        stand(me, new V2(600f, 470f)); hp0 = me.Hp;
        bool beeped = false;
        Run(s, dt, 0.6f, () => !g.Enemies.ContainsKey(a.Id), () => { me.Pos = new V2(600f, 470f); beeped |= a.Phase == 2; });
        Run(s, dt, 0.05f);
        Check(beeped && !g.Enemies.ContainsKey(a.Id) && me.Hp < hp0, "stepping by an armed pod: it beeps, then blows up and hurts you");

        // A laser between two armed pods: crossing it sets both off.
        var b1 = pod(new V2(500f, 300f));
        var b2 = pod(new V2(640f, 300f));
        stand(me, new V2(570f, 200f));
        Run(s, dt, 0.1f, null, () => { me.Pos = new V2(570f, 200f); });
        Check(b1.Phase == 1 && b2.Phase == 1, "two armed pods 140 px apart stay quiet while you're away from both and the laser");
        Run(s, dt, 0.2f, () => b1.Phase == 2, () => { me.Pos = new V2(570f, 302f); });
        Check(b1.Phase == 2 && b2.Phase == 2, "crossing the laser between two pods sets both off");
        Run(s, dt, 0.5f, null, () => { me.Pos = new V2(570f, 120f); });

        // Shooting a pod blows it up at once, hurting the enemy by it, with the credit to the shooter.
        var c = pod(new V2(900f, 600f));
        var g2 = put(EnemyType.Grunt, new V2(930f, 600f));
        g2.Hp = g2.MaxHp = 1e5f; g2.FireT = 99f;
        stand(me, new V2(300f, 200f));
        hurt.Invoke(g, new object[] { c, 5f, 1 });
        Run(s, dt, 0.1f, null, () => { me.Pos = new V2(300f, 200f); g2.Pos = new V2(930f, 600f); g2.FrozenT = 1f; });
        Check(!g.Enemies.ContainsKey(c.Id) && g2.Hp < g2.MaxHp && g2.LastHitBy == 1, "a shot pod blows up at once and hurts the enemy by it (credited to the shooter)");
        s[0].Leave(""); s[1].Leave("");
    }
}
