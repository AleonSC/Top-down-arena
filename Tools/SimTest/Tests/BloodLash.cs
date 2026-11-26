using System;
using System.Collections.Generic;
using Arena;

// The tank Hemomancer's Blood Lash: the chain combo (Core/BloodLash.cs).
static partial class Program
{
    static void BloodLashTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var fire = typeof(Game).GetMethod("FireVolley", flags);
        var weaponDash = typeof(Game).GetMethod("HostWeaponDash", flags);

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Tank", Role.Tank, 0, 0, Kit.Hemomancer);
        var client = JoinAs(port, false, "Healer", Role.Healer, 0, 0, Kit.Hemomancer);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var hp = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(hp.Weapon.Id == WeaponId.BloodLash, "the tank Hemomancer starts with the Blood Lash");

        var keep = new HashSet<Enemy>();
        Action clear = () => { foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f; };
        Func<V2, Enemy> dummy = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e6f; keep.Add(e); return e; };
        Action<float> run = secs => Run(s, dt, secs, null, () => { clear(); foreach (var e in keep) if (e.StunT <= 0f) e.FrozenT = 5f; });
        Action lash = () => fire.Invoke(g, new object[] { hp, 0f, 0 });
        hp.Pos = hp.Disp = new V2(300f, 450f);
        clear(); run(0.3f);
        hp.Hp = hp.MaxHp; hp.Clot = 0; hp.Combo = 0; hp.ComboT = 0f;

        // Crack: the tip doubles and taunts, closer hits aren't flung any more, and every hit hooks a chain (+2 from the tip).
        var close = dummy(new V2(360f, 450f));
        var far = dummy(new V2(415f, 450f));
        close.TargetId = far.TargetId = 2;
        close.RetargetT = far.RetargetT = 10f;
        float n0 = close.Hp, f0 = far.Hp;
        lash(); run(0.1f);
        float nearDmg = n0 - close.Hp, tipDmg = f0 - far.Hp;
        var cp = client.Game.Players[1];
        Console.WriteLine("  crack: close {0:0.0}, tip {1:0.0}; links {2}/{3}; client sees {4} chains; step {5}", nearDmg, tipDmg, Game.LinksOn(hp, close.Id), Game.LinksOn(hp, far.Id), cp.Chains.Count, Game.LashStepOf(hp.Combo));
        Check(nearDmg > 0f && Math.Abs(tipDmg - nearDmg * Game.LashCrack) < 0.5f && far.TargetId == 1 && close.TargetId == 2 && close.Pos.DistanceTo(hp.Pos) + close.Radius < Game.MeleeReach(hp, hp.Weapon.Reach) * Game.LashTipFrom,
            "Crack: the tip cracks for double and taunts; a closer hit isn't flung out to the tip any more");
        Check(Game.LinksOn(hp, close.Id) == 1 && Game.LinksOn(hp, far.Id) == 2 && cp.Chains.Count == 2 && Game.LinksOn(cp, far.Id) == 2 && hp.Clot == 0,
            "each lash hooks a chain: +1 link, +2 from the tip (clients see the chains); hits no longer build Clot");

        // Backhand, then Snap: the string moves on, links stack to the cap, the Snap taunts.
        lash(); run(0.1f);
        Check(Game.LashStepOf(hp.Combo) == 2 && Game.LinksOn(hp, close.Id) == 2 && Game.LinksOn(hp, far.Id) == Game.LashMaxLinks,
            "Backhand is step two and adds links, up to 4 on one enemy");
        close.RetargetT = 0f; close.TargetId = 2; close.RetargetT = 10f;
        float c1 = close.Hp;
        lash(); run(0.25f);
        Console.WriteLine("  snap: close took {0:0.0} (crack lash {1:0.0}), links {2}, target {3}", c1 - close.Hp, nearDmg, Game.LinksOn(hp, close.Id), close.TargetId);
        Check(Game.LashStepOf(hp.Combo) == 3 && Math.Abs((c1 - close.Hp) - nearDmg * Game.LashSnapMult) < 0.5f && Game.LinksOn(hp, close.Id) == 4 && close.TargetId == 1,
            "Snap is step three: 1.5x, +2 links, and it taunts");

        // Reap: everything chained is yanked in, hit for 20 + 12 a link, and the links become Clot.
        float rc = close.Hp, rf = far.Hp;
        lash(); run(Game.LashReapTime + 0.1f);
        float touch = close.Radius + hp.Def.BodyR + 10f;
        Console.WriteLine("  reap: close {0:0.0} at {1:0} px, far {2:0.0} at {3:0} px; clot {4}; chains {5}", rc - close.Hp, close.Pos.DistanceTo(hp.Pos), rf - far.Hp, far.Pos.DistanceTo(hp.Pos), hp.Clot, hp.Chains.Count);
        Check(Game.LashStepOf(hp.Combo) == 4 && close.Pos.DistanceTo(hp.Pos) < touch && far.Pos.DistanceTo(hp.Pos) < touch && hp.Chains.Count == 0 && hp.Clot == 5,
            "Reap is step four: every chained enemy is yanked in to touching you, the chains spent as Clot (capped at 5)");
        Check(rc - close.Hp > Game.LashReapBase && rf - far.Hp > Game.LashReapBase, "Reap hits each enemy for 20 + 12 a link");

        // After Reap the string starts over; a pause longer than the window starts it over too.
        lash(); run(0.1f);
        Check(Game.LashStepOf(hp.Combo) == 1, "after Reap the next press is Crack again");
        run(Game.LashCrackTime + Game.LashChainWindow + 0.1f);
        lash(); run(0.1f);
        Check(Game.LashStepOf(hp.Combo) == 1, "a pause longer than the window starts the string over");

        // Chains hold 3 s after the last hit, then drop a link every 0.5 s.
        int before = Game.LinksOut(hp);
        run(Game.LashLinkHold + Game.LashLinkFade + 0.1f);
        int after = Game.LinksOut(hp);
        Console.WriteLine("  fade: links {0} -> {1}", before, after);
        Check(before > 0 && after < before, "chains hold for 3 s, then drop a link every 0.5 s");

        // Martyr's Leash: held, it sends 40% of a hit down the chains (3x over), taunts and holds them; it drains you.
        hp.Combo = 0; hp.ComboT = 0f;
        close.Pos = new V2(360f, 450f); far.Pos = new V2(415f, 450f);
        lash(); run(0.1f);
        weaponDash.Invoke(g, new object[] { 1, 19, far.Pos });  // WeaponAlt.PullOn
        run(0.1f);
        bool cSees = cp.LeashOn;
        hp.Hp = hp.MaxHp;
        float lc = close.Hp, lf = far.Hp, h0 = hp.Hp;
        hp.DevGod = false; hp.InvulnT = 0f;
        damage.Invoke(g, new object[] { hp, 100f / g.EnemyDamageScale(g.Wave), "test" });
        hp.DevGod = true;
        float taken = h0 - hp.Hp, back = (lc - close.Hp) + (lf - far.Hp);
        Console.WriteLine("  leash: on {0} (client {1}); took {2:0.0} of 100 (clot {3}); chains took {4:0.0}", hp.LeashOn, cSees, taken, hp.Clot, back);
        float expect = 100f * (1f - Game.ClotCut(hp));
        Check(hp.LeashOn && cSees && Math.Abs(taken - expect * (1f - Game.LeashShare)) < 1f,
            "Martyr's Leash: held, 40% of each hit you take is taken off (clients see it up)");
        Check(back > expect * Game.LeashShare * Game.LeashReturn * 0.9f, "and it runs down the chains into the chained enemies, 3x over");
        float hl = hp.Hp;
        run(1f);
        Check(hp.Hp < hl && close.TargetId == 1 && far.TargetId == 1, "the Leash drains 2% a second and taunts everything chained");
        run(Game.LeashMax);
        Check(!hp.LeashOn && hp.LeashCd > 0f, "the Leash lets go after 3 s and goes on a 3 s cooldown");
        weaponDash.Invoke(g, new object[] { 1, 19, far.Pos });
        Check(!hp.LeashOn, "it can't start again while cooling down");
        weaponDash.Invoke(g, new object[] { 1, 20, far.Pos });

        // Putting the Blood Lash away drops the chains.
        hp.Weapon = Defs.Weapon(Role.Tank, Kit.Hemomancer, 1);
        run(0.1f);
        Check(hp.Chains.Count == 0, "chains drop with the weapon");
        hp.Weapon = Defs.Weapon(Role.Tank, Kit.Hemomancer, 0);

        s[0].Leave(""); s[1].Leave("");
    }
}
