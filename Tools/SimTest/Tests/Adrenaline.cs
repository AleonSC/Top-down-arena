using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Healer Soldier's Adrenaline Shot: goes to the living player closest to the cursor (you, alone), and for 8 s gives +30% speed,
// +40% fire rate (not stacked with Overdrive) and 30% less damage. The capstone gives the nearest other ally within 250 px half a dose.
static partial class Program
{
    static void AdrenalineTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("HostAbility", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var fireRate = typeof(Game).GetMethod("FireRate", flags);

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Healer", Role.Healer, 0, 1, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];
        Check(me.Slots[0].Id == AbilityId.Adrenaline && Upgrades.Ability(AbilityId.Adrenaline).Name == "Adrenaline Shot", "the Soldier Healer's second Space ability is the Adrenaline Shot");

        Action calm = () => { foreach (var e in g.Enemies.Values) e.Hp = 0f; g.Bullets.Clear(); };
        Action<float> run = secs => Run(s, dt, secs, null, calm);
        Action<V2> use = at => { me.Slots[0].Charges = 1; cast.Invoke(g, new object[] { 1, 0, at, true }); };
        Func<Player, float> rate = p => (float)fireRate.Invoke(g, new object[] { p });
        Action place = () => { me.Pos = new V2(300f, 450f); ally.Pos = new V2(800f, 450f); };
        calm(); place(); run(0.3f); place();

        // Closest to the cursor wins, whoever that is.
        float baseSpeed = ally.Speed, baseRate = rate(ally), meSpeed = me.Speed, meRate = rate(me);
        use(new V2(760f, 420f));
        Console.WriteLine("  shot at the ally: ally {0:0.0} s, you {1:0.0} s", ally.AdrenalineT, me.AdrenalineT);
        Check(ally.AdrenalineT > 7.9f && ally.AdrenalineT <= 8.01f && me.AdrenalineT == 0f, "the shot goes to the ally nearest the cursor, 8 s long");
        Check(Math.Abs(ally.Speed / baseSpeed - 1.3f) < 0.001f && Math.Abs(rate(ally) / baseRate - 1.4f) < 0.001f, "it gives +30% move speed and +40% fire rate");
        run(0.3f);
        Check(client.Game.Players[2].AdrenalineT > 7f, "the buff is replicated to the other players' screens");

        ally.InvulnT = 0f; ally.ShieldHp = 0f; ally.Hp = ally.MaxHp;
        damage.Invoke(g, new object[] { ally, 40f, "test" });
        float buffedLoss = ally.MaxHp - ally.Hp;
        ally.AdrenalineT = 0f; ally.Hp = ally.MaxHp; ally.InvulnT = 0f;
        damage.Invoke(g, new object[] { ally, 40f, "test" });
        float plainLoss = ally.MaxHp - ally.Hp;
        Console.WriteLine("  40 damage: {0:0.0} with the shot, {1:0.0} without", buffedLoss, plainLoss);
        Check(plainLoss > 1f && Math.Abs(buffedLoss / plainLoss - 0.7f) < 0.01f, "it cuts damage taken by 30%");

        ally.Hp = ally.MaxHp; ally.InvulnT = 0f;
        use(new V2(330f, 450f));
        Check(me.AdrenalineT > 7.9f, "the cursor near you shoots yourself");
        Check(ally.AdrenalineT == 0f, "and only the closest player gets it");

        // Overdrive doesn't stack with it: the bigger wins.
        float withShot = rate(me);
        me.OverdriveT = 5f;
        Check(Math.Abs(withShot / meRate - 1.4f) < 0.001f && Math.Abs(rate(me) / meRate - 2f) < 0.001f, "with Overdrive up, fire rate is the larger of the two (x2), not both (x2.8)");
        me.OverdriveT = 0f;

        // A downed ally is skipped, even if the cursor is right on them.
        me.AdrenalineT = 0f; ally.Downed = true;
        use(new V2(800f, 450f));
        Check(me.AdrenalineT > 7.9f && ally.AdrenalineT == 0f, "downed players are skipped");
        ally.Downed = false; ally.Hp = ally.MaxHp * 0.5f;

        // It runs out after 8 s; recasting refreshes rather than stacking.
        me.AdrenalineT = 0f;
        use(me.Pos);
        run(5f); use(me.Pos);
        Check(me.AdrenalineT > 7.9f && me.AdrenalineT <= 8.01f, "recasting refreshes the 8 s (no stacking)");
        run(8.5f);
        Check(me.AdrenalineT == 0f && Math.Abs(me.Speed / meSpeed - 1f) < 0.001f, "it wears off after 8 s");

        // Long Dose: +3 s per stack.
        var longDose = Array.FindIndex(Upgrades.All, u => u.Name == "Long Dose");
        Check(longDose >= 0, "the Long Dose card exists");
        g.GrantUpgrade(1, longDose);
        use(me.Pos);
        Console.WriteLine("  with one Long Dose: {0:0.0} s", me.AdrenalineT);
        Check(me.AdrenalineT > 10.9f && me.AdrenalineT <= 11.01f, "Long Dose makes it last 3 s longer");
        me.AdrenalineT = 0f;

        // Going down ends it.
        me.AdrenalineT = 5f; me.Hp = 1f; me.InvulnT = 0f; me.ShieldHp = 0f;
        damage.Invoke(g, new object[] { me, 500f, "test" });
        Check(me.Downed && me.AdrenalineT == 0f, "going down ends the buff");
        me.Downed = false; me.Hp = me.MaxHp;

        // Capstone: the nearest other ally within 250 px gets half a dose; one farther away gets nothing.
        place(); me.AdrenalineT = ally.AdrenalineT = 0f;
        me.MasteryXp[AbilityId.Adrenaline] = Game.MasteryXpFor[Game.MasteryMax - 1];
        ally.Pos = new V2(450f, 450f);
        use(me.Pos);
        float full = me.AdrenalineT, half = ally.AdrenalineT;
        Console.WriteLine("  capstone, ally 150 px away: you {0:0.0} s, ally {1:0.0} s", full, half);
        Check(full > 7.9f && Math.Abs(half - full / 2f) < 0.05f, "mastered: the nearest other ally within 250 px gets a half-length dose");
        me.AdrenalineT = ally.AdrenalineT = 0f; ally.Pos = new V2(700f, 450f);
        use(me.Pos);
        Check(me.AdrenalineT > 7.9f && ally.AdrenalineT == 0f, "mastered: an ally farther than 250 px is left out");
        me.MasteryXp.Remove(AbilityId.Adrenaline);

        host.Leave(""); client.Leave("");
    }
}
