using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

static partial class Program
{
    /// The Swordsman and the Shield Swordsman (Swordsman.cs): the wind-up and the cut in the cone, a dodge out of the cone,
    /// the shield's guard (up, dropped for the cut, back up late in the recovery), its block flash, and the wave unlocks.
    static void SwordsmanTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var blocks = typeof(Game).GetMethod("BlocksShot", flags);
        var wear = typeof(Game).GetMethod("WearShield", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);

        var s = StartPlain(dt, 19);
        var g = s[0].Game;
        var me = g.Players[1];
        var friend = g.Players[2];
        Func<EnemyType, V2, Enemy> put = (t, at) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { t, at, 0f, 0, false });
            e.SpawnT = 0f;
            e.Hp = e.MaxHp = 1e6f;
            return e;
        };
        Enemy anchor = null;  // a frozen Grunt in a corner holds the wave open
        Action quiet = () =>
        {
            ((List<EnemyType>)queueField.GetValue(g)).Clear();
            ClearSquads(g);
            if (anchor == null) { anchor = put(EnemyType.Grunt, new V2(1500f, 80f)); }
            foreach (var e0 in g.Enemies.Values) if (e0 != anchor) e0.Hp = 0f;
            Run(s, dt, 0.2f, null, () => { anchor.FrozenT = 1f; anchor.FireT = 99f; anchor.Pos = new V2(1500f, 80f); });
            g.Bullets.Clear();
        };
        Run(s, dt, 8f, () => g.State == MatchState.Fighting);
        quiet();
        g.DevSetGod(2, true);
        friend.Pos = new V2(150f, 820f);
        V2 home = new V2(800f, 450f), far = new V2(150f, 820f);  // the friend is kept far away and downed so a Swordsman targets you
        Action stand = () => { me.Pos = home; me.Hp = me.MaxHp = 5000f; me.InvulnT = 0f; };

        // --- the cut: wind-up, then everyone in the cone takes it once -----------------------------------------------
        stand();
        var sw = put(EnemyType.Swordsman, home + new V2(-100f, 0f));
        int swId = sw.Id;
        bool wound = false, cut = false, recovered = false, clientSawWind = false;
        float hp0 = me.Run.DamageTaken;
        Run(s, dt, 4f, () => recovered, () =>
        {
            me.Pos = home; me.InvulnT = 0f; friend.Pos = far; friend.Downed = true;
            wound |= sw.Phase == 1;
            cut |= sw.Phase == 2;
            recovered |= cut && sw.Phase == 3;
            Enemy ce; if (s[1].Game.Enemies.TryGetValue(swId, out ce) && ce.Phase == 1 && ce.PhaseT > 0f && ce.PhaseT < Game.SwordWind) clientSawWind = true;
        });
        float lost = me.Run.DamageTaken - hp0;
        Console.WriteLine("  Swordsman: wound up {0}, cut {1}, recovered {2}; you lost {3:0} (one cut is {4})", wound, cut, recovered, lost, Game.SwordDamage);
        Check(wound && cut && recovered, "a Swordsman winds up, cuts and recovers");
        Check(lost > 0f && lost < Game.SwordDamage * 4f, "the cut hurts a player in the cone once");
        Check(clientSawWind, "the client sees the wind-up's timer (it is in the snapshot)");
        sw.Hp = 0f; quiet();

        // --- a dodge: step out of the cone while it winds up ---------------------------------------------------------
        stand();
        sw = put(EnemyType.Swordsman, home + new V2(-100f, 0f));
        Run(s, dt, 2f, () => sw.Phase == 1, () => { me.Pos = home; me.InvulnT = 0f; friend.Pos = far; friend.Downed = true; });
        hp0 = me.Run.DamageTaken;
        bool dodged = false;
        Run(s, dt, 1f, () => sw.Phase == 3, () =>
        {
            me.InvulnT = 0f; friend.Pos = far; friend.Downed = true;
            me.Pos = sw.Pos + new V2(0f, 100f);  // square to where it faces: well out of the 90 degree cone
            dodged |= sw.Phase == 2;
        });
        Console.WriteLine("  stepped out of the cone: it cut {0}, you lost {1:0}", dodged, me.Run.DamageTaken - hp0);
        Check(dodged && me.Run.DamageTaken == hp0, "stepping out of the cone before the cut takes no damage");
        sw.Hp = 0f; quiet();

        // --- the Shield Swordsman: the guard -------------------------------------------------------------------------
        var sh = put(EnemyType.ShieldSwordsman, home);
        sh.Facing = 0f; sh.Phase = 0;
        Func<V2, bool> stops = from => (bool)blocks.Invoke(g, new object[] { sh, from });
        V2 front = sh.Pos + new V2(200f, 0f), behind = sh.Pos + new V2(-200f, 0f), beside = sh.Pos + new V2(0f, 200f);
        Check(stops(front) && !stops(behind) && !stops(beside), "its shield stops a shot from the front, not from behind or the side");
        sh.Phase = 1; sh.PhaseT = 0.3f;
        Check(stops(front), "the shield stays up while it winds up");
        sh.Phase = 2; sh.PhaseT = Game.SwordCut - 0.1f;
        Check(!stops(front), "the shield is down for the cut");
        sh.Phase = 3; sh.PhaseT = Game.ShieldRecover - 0.3f;
        Check(!stops(front), "the shield is still down early in the recovery");
        sh.Phase = 3; sh.PhaseT = 0.05f;
        Check(stops(front), "the shield is back up at the end of the recovery");
        var plain = put(EnemyType.Swordsman, home + new V2(0f, 120f));
        plain.Facing = 0f; plain.Phase = 0;
        Check(!(bool)blocks.Invoke(g, new object[] { plain, plain.Pos + new V2(200f, 0f) }), "the plain Swordsman has no shield");

        // --- the flash when it blocks --------------------------------------------------------------------------------
        sh.Phase = 0; sh.T2 = 0f;
        float shHp = sh.Hp;
        wear.Invoke(g, new object[] { sh, 10f });
        Check(Math.Abs(sh.T2 - Game.SwordBlockFlash) < 0.001f && sh.Hp == shHp, "a blocked hit flashes the plate and doesn't wear it");
        Run(s, dt, 0.1f, null, () => { me.Pos = new V2(100f, 100f); });
        Enemy cl; bool clientFlash = s[1].Game.Enemies.TryGetValue(sh.Id, out cl) && cl.T2 > 0f;
        Check(clientFlash, "the client sees the flash (it is in the snapshot)");
        sh.Hp = 0f; plain.Hp = 0f; quiet();

        // --- waves ---------------------------------------------------------------------------------------------------
        Check(Game.IsRetired(EnemyType.Swordsman) && !Game.NewTypesAt(Game.SwordsmanFrom).Contains(EnemyType.Swordsman), "the Swordsman is retired from the waves (BasicFaction.cs), so it gets no new-enemy chip");
        Check(Game.IsRetired(EnemyType.ShieldSwordsman) && !Game.NewTypesAt(Game.ShieldSwordsmanFrom).Contains(EnemyType.ShieldSwordsman), "nor does the Shield Swordsman");
        Check(Game.EnemyNote(EnemyType.Swordsman) != "" && Game.EnemyNote(EnemyType.ShieldSwordsman) != "", "both have a one-line note for the chip");
        friend.Downed = false;
        g.DevSetGod(2, false);
    }
}
