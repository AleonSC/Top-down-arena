using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Fuma Shuriken's Orbit stance (Core/FumaOrbit.cs), run from the ninja suite with its host Tank Ninja and joining client.
static partial class Program
{
    static void FumaOrbitTest(Game g, Session client, Player hp, Session[] s, float dt, Action<float> run, Func<V2, Enemy> dummy, Action<int, float> place, HashSet<Enemy> keep)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var dash = typeof(Game).GetMethod("HostWeaponDash", flags);
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var parry = typeof(Game).GetMethod("FumaParry", flags);
        var steal = typeof(Game).GetMethod("FumaSteal", flags);
        Action swap = () => dash.Invoke(g, new object[] { 1, 24, hp.Pos + new V2(100f, 0f) });
        Action fire = () => { hp.FireCd = 0f; volley.Invoke(g, new object[] { hp, 0f, 0 }); };
        Func<Enemy, float> taken = e => e.MaxHp - e.Hp;
        var mine = new List<Enemy>();
        var outer = dummy;
        dummy = p => { var e = outer(p); mine.Add(e); return e; };
        Action drop = () => { foreach (var e in mine) { e.Hp = 0f; keep.Remove(e); } mine.Clear(); };
        var parked = keep.ToList();
        // (the suite's own dummies sit this out in a corner, out of reach)
        var parkedAt = parked.Select(e => e.Pos).ToList();
        foreach (var e in parked) e.Pos = new V2(60f, 60f);

        // The weapon-action key: the star leaves your hand and circles you, cutting what it passes; the client follows the same circle.
        drop(); place(1, 500f); run(0.1f);
        hp.Aim = 0f; hp.FumaSwitchCd = 0f;
        V2 at = hp.Pos;
        var ring = dummy(at + new V2(0f, Game.FumaOrbitR));
        swap();
        float drift = 0f;
        Run(s, dt, 1.2f, null, () =>
        {
            hp.Pos = at; foreach (var e in keep) e.FrozenT = 5f;
            var cq = client.Game.Players[1];
            if (cq.FumaStance == 1 && hp.FumaStance == 1)
                drift = Math.Max(drift, (Game.FumaPose(cq, hp.FumaClock, 0, 1) - Game.FumaPose(hp, hp.FumaClock, 0, 1)).Length);
        });
        float orbitHit = taken(ring);
        bool clientOrbit = client.Game.Players[1].FumaStance == 1;
        Console.WriteLine("  fuma orbit: stance {0}, cut {1:0.0} in 1.2 s, client sees it {2} (pose drift {3:0.0} px)", hp.FumaStance, orbitHit, clientOrbit, drift);
        Check(hp.FumaStance == 1 && orbitHit >= hp.Weapon.Damage * Game.FumaOrbitMult * 2f && clientOrbit && drift < 1f,
            "the weapon-action key sets the Fuma circling you, cutting what it passes, on the same circle on every screen");

        // Fire: a Lash out across your aim, a Lash back, then the Cyclone's wide lap.
        drop();
        var far = dummy(at + new V2(Game.FumaOrbitR + Game.FumaLashReach - 10f, 0f));
        var back = dummy(at + new V2(-(Game.FumaOrbitR + Game.FumaCycloneReach - 15f), 0f));
        fire(); Run(s, dt, 0.45f, null, () => { hp.Pos = at; foreach (var e in keep) e.FrozenT = 5f; });
        float lashHit = taken(far);
        fire(); Run(s, dt, 0.45f, null, () => { hp.Pos = at; foreach (var e in keep) e.FrozenT = 5f; });
        float backBefore = taken(back);
        int moveSeen = hp.FumaMove;
        fire(); int cyclone = hp.FumaMove;
        Run(s, dt, 0.7f, null, () => { hp.Pos = at; foreach (var e in keep) e.FrozenT = 5f; });
        float cycloneHit = taken(back) - backBefore;
        Console.WriteLine("  fuma lash: far enemy took {0:0.0}; third press move {1}; cyclone cut the one behind for {2:0.0}", lashHit, cyclone, cycloneHit);
        Check(lashHit >= hp.Weapon.Damage * Game.FumaLashMult * 0.99f, "a Lash whips the star out past the circle across your aim");
        Check(cyclone == 3 && cycloneHit >= hp.Weapon.Damage * Game.FumaCycloneMult * 0.99f && moveSeen == 0, "every third press is the Cyclone, a wide lap that reaches behind you");

        // Raising the parry: Reversal flips the orbit, speeds it up and cuts everything in its ring.
        drop();
        var inRing = dummy(at + new V2(0f, -(Game.FumaOrbitR + 30f)));
        hp.FumaRevCd = 0f;
        float spin0 = hp.FumaSpin;
        parry.Invoke(g, new object[] { hp });
        float revHit = taken(inRing), spin1 = hp.FumaSpin;
        Run(s, dt, Game.FumaRevTime + 0.2f, null, () => { hp.Pos = at; foreach (var e in keep) e.FrozenT = 5f; });
        Console.WriteLine("  fuma reversal: spin {0:0.0} -> {1:0.0} -> {2:0.0} rad/s, the ring took {3:0.0}", spin0, spin1, hp.FumaSpin, revHit);
        Check(Math.Sign(spin1) == -Math.Sign(spin0) && Math.Abs(spin1) > Game.FumaOmega * 1.4f && Math.Abs(Math.Abs(hp.FumaSpin) - Game.FumaOmega) < 0.01f
            && revHit >= hp.Weapon.Damage * Game.FumaRevMult * 0.99f, "parrying in the Orbit stance reverses the circle, faster for a while, cutting its ring");

        // A perfect parry keeps the shot as a small star, which flies back at its shooter.
        drop();
        var shooter = dummy(at + new V2(400f, 0f));
        bool kept = (bool)steal.Invoke(g, new object[] { hp, new Bullet { Kind = Defs.SmallShot, Shooter = shooter.Id } });
        int minis = hp.FumaMinis;
        Run(s, dt, Game.FumaStealHold + 0.8f, null, () => { hp.Pos = at; foreach (var e in keep) e.FrozenT = 5f; });
        Console.WriteLine("  fuma steal: kept {0}, {1} circling, then {2} left; the shooter took {3:0.0}", kept, minis, hp.FumaMinis, taken(shooter));
        Check(kept && minis == 1 && hp.FumaMinis == 0 && taken(shooter) >= hp.Weapon.Damage * Game.FumaStealMult * 0.99f,
            "a perfect parry in the Orbit stance steals the shot, which circles you and then flies back at whoever fired it");

        // Back to the Throw stance; a parry while a thrown star is out catches it into orbit from where it was.
        drop();
        hp.FumaSwitchCd = 0f; swap();
        bool throwStance = hp.FumaStance == 0;
        fire();
        Run(s, dt, 0.3f, null, () => { hp.Pos = at; });
        var flying = g.Bullets.Values.FirstOrDefault(b => b.Owner == 1 && b.Kind == Defs.FumaStar);
        float out0 = flying != null ? flying.Pos.DistanceTo(at) : 0f;
        parry.Invoke(g, new object[] { hp });
        bool gone = !g.Bullets.Values.Any(b => b.Owner == 1 && b.Kind == Defs.FumaStar);
        V2 startPose = Game.FumaPose(hp, 0f, 0, 1);
        Console.WriteLine("  fuma catch: throw stance {0}, star {1:0} px out, caught {2} (starts {3:0} px out)", throwStance, out0, gone && hp.FumaStance == 1, startPose.Length);
        Check(throwStance && flying != null && gone && hp.FumaStance == 1 && Math.Abs(startPose.Length - out0) < 1f,
            "parrying with a Fuma in flight catches it into the Orbit stance from where it was");
        hp.FumaSwitchCd = 0f; swap();
        drop();
        for (int i = 0; i < parked.Count; i++) parked[i].Pos = parkedAt[i];
    }
}
