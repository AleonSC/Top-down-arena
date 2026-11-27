using System;
using System.Collections.Generic;
using Arena;

// The tank Hemomancer's Blood Blast: the six-step chain, its health costs, blood stacks and the kill heal (Core/BloodBlast.cs).
static partial class Program
{
    static void BloodBlastTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var fire = typeof(Game).GetMethod("FireVolley", flags);
        var weaponDash = typeof(Game).GetMethod("HostWeaponDash", flags);

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Tank", Role.Tank, 3, 0, Kit.Hemomancer);
        var client = JoinAs(port, false, "Healer", Role.Healer, 0, 0, Kit.Hemomancer);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var hp = g.Players[1];
        g.DevSetGod(2, true);
        Check(hp.Weapon.Id == WeaponId.BloodBlast && hp.Weapon.Melee, "the tank Hemomancer's 4th weapon is the Blood Blast, a melee weapon");

        var keep = new HashSet<Enemy>();
        Action clear = () => { foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f; };
        Func<V2, Enemy> dummy = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e6f; keep.Add(e); return e; };
        Action<float> run = secs => Run(s, dt, secs, null, () => { clear(); hp.InvulnT = 1f; foreach (var e in keep) { e.FrozenT = 5f; } });
        Action press = () => fire.Invoke(g, new object[] { hp, 0f, 0 });
        hp.Pos = hp.Disp = new V2(300f, 450f);
        clear(); run(0.3f);
        var front = dummy(new V2(375f, 450f));
        var back = dummy(new V2(245f, 450f));
        V2 fp = front.Pos, bp = back.Pos;
        Action home = () => { front.Pos = front.Disp = fp; back.Pos = back.Disp = bp; hp.Pos = hp.Disp = new V2(300f, 450f); };

        // The chain: six steps in order, each costing its share of max health; every press stacks what it hits once.
        var steps = new List<int>();
        bool costs = true;
        for (int i = 1; i <= Game.BlastSteps; i++)
        {
            home();
            hp.Hp = hp.MaxHp;
            float h0 = hp.Hp;
            press();
            float paid = h0 - hp.Hp;
            costs &= Math.Abs(paid - hp.MaxHp * Game.BlastCostFrac[i - 1]) < 0.01f;
            run(0.6f);
            steps.Add(Game.BlastStepOf(hp.Combo));
        }
        Console.WriteLine("  steps {0}; front {1} stacks, back {2}", string.Join(",", steps), front.BlastStacks, back.BlastStacks);
        Check(string.Join(",", steps) == "1,2,3,4,5,6", "Fire runs the chain: Slash, Backcut, Flurry, Whirl, Cleave, Flight");
        Check(costs, "each step costs its share of max health (1.3, 1.3, 1.7, 2, 3, 2.3%)");
        Check(front.BlastStacks == 6 && front.BlastOwner == hp.Id, "every press stacks an enemy it hits once (Thorn Flurry's three cuts add one)");
        Check(back.BlastStacks == 1, "only Vein Whirl's spin reaches behind you");
        var ce = client.Game.Enemies[front.Id];
        Check(ce.BlastStacks == 6 && ce.BlastOwner == hp.Id, "clients see the stacks and whose they are");

        // It never times out: after a long pause the next press is the next step (1 after 6).
        run(3f);
        home(); press(); run(0.6f);
        Check(Game.BlastStepOf(hp.Combo) == 1, "after Crescent Flight it goes back to Crimson Slash");
        home(); press(); run(3f);
        home(); press(); run(0.6f);
        Check(Game.BlastStepOf(hp.Combo) == 3, "the chain never times out: a long pause still plays the next step");

        // Stacks cap at 10.
        for (int i = 0; i < 6; i++) { home(); press(); run(0.6f); }
        Check(front.BlastStacks == Game.BlastStackMax, "an enemy holds at most 10 stacks");

        // A kill sends the blood home: the heal lands as it arrives, 2 a stack.
        hp.Hp = 100f;
        keep.Remove(front);
        front.Hp = 0f;
        run(0.4f);
        float early = hp.Hp;
        run(Game.BlastReturnTime);
        float healed = hp.Hp - 100f;
        Console.WriteLine("  kill: {0:0.0} at 0.4 s, {1:0.0} on arrival (10 stacks)", early - 100f, healed);
        Check(early - 100f < 1f && Math.Abs(healed - Game.BlastStackMax * Game.BlastHealPerStack) < 2f, "a stacked kill's blood heals 2 a stack, when it reaches you");

        // Crimson Tide: 12% of max health, a 0.65 s gather, then a 400 px crescent that hits, shoves and stacks once each.
        var near = dummy(new V2(380f, 450f));
        var far = dummy(new V2(640f, 450f));
        var side = dummy(new V2(300f, 300f));
        hp.Pos = hp.Disp = new V2(300f, 450f);
        hp.Hp = hp.MaxHp;
        float tn = near.Hp, tf = far.Hp, ts = side.Hp;
        weaponDash.Invoke(g, new object[] { 1, 46, new V2(700f, 450f) });
        float tidePaid = hp.MaxHp - hp.Hp;
        run(0.4f);
        bool gathering = near.Hp == tn;
        run(Game.TideCharge + Game.TideTravel);
        float tideHit = tn - near.Hp;
        Console.WriteLine("  tide: paid {0:0.0}; near {1:0.0}, far {2:0.0}, side {3:0.0}; stacks {4}/{5}/{6}", tidePaid, tideHit, tf - far.Hp, ts - side.Hp, near.BlastStacks, far.BlastStacks, side.BlastStacks);
        Check(Math.Abs(tidePaid - hp.MaxHp * Game.TideCost) < 0.01f, "Crimson Tide costs 12% of max health");
        Check(gathering && tideHit > 0f && Math.Abs((tf - far.Hp) - tideHit) < 0.5f && ts == side.Hp, "it gathers, then rolls out 400 px along the aim, hitting each enemy once (not off to the side)");
        Check(Math.Abs(tideHit / (hp.Weapon.Damage * hp.Stats.DmgMult * Might * Game.TideMult) - 1f) < 0.1f, "it hits for about 60 (x your damage; the Hemomancer's low-health bonus on top)");
        Check(near.BlastStacks == 1 && far.BlastStacks == 1 && side.BlastStacks == 0, "every enemy the Tide hits gets a blood stack");
        float cdHp = hp.Hp;
        weaponDash.Invoke(g, new object[] { 1, 46, new V2(700f, 450f) });
        Check(hp.Hp == cdHp && hp.BlastTideCd > 0f, "it can't go again during its 8 s cooldown");

        // Blood Harvest (Rare card): +1 health a stack and +5 stacks an enemy.
        var card = Array.Find(Upgrades.All, u => u.Name == "Blood Harvest");
        Check(card != null && card.Rarity == Rarity.Rare && card.MaxStacks == 2 && Array.IndexOf(card.Weapons, WeaponId.BloodBlast) >= 0, "Blood Harvest is a Rare Blood Blast card, two stacks");
        card.Apply(hp.Stats);
        var tall = dummy(new V2(375f, 450f));
        for (int i = 0; i < 16; i++) { hp.Pos = hp.Disp = new V2(300f, 450f); tall.Pos = tall.Disp = new V2(375f, 450f); press(); run(0.6f); }
        hp.Hp = 100f;
        keep.Remove(tall);
        tall.Hp = 0f;
        run(Game.BlastReturnTime + 0.2f);
        Console.WriteLine("  harvest: cap {0}, healed {1:0.0}", Game.BlastStackCap(hp), hp.Hp - 100f);
        Check(Game.BlastStackCap(hp) == 15 && Math.Abs((hp.Hp - 100f) - 15 * 3f) < 2f, "with Blood Harvest an enemy holds 15 stacks and each heals 3");

        // Switching weapons starts the chain over.
        hp.BlastStep = 4;
        hp.Weapon = Defs.Weapon(Role.Tank, Kit.Hemomancer, 0);
        run(0.1f);
        Check(hp.BlastStep == 0, "putting the Blood Blast away starts its chain over");
        s[0].Leave(""); s[1].Leave("");
    }
}
