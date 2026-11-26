using System.Linq;
using System;
using Arena;

// Chakra Reserves (ChakraReserves.cs), the Healer Ninja's 4th choice on the lobby's 4th key: the table and cards, the pick
// reaching the run, the real key path, the heal (40 + 15% of what's missing), the Chakra Surge (faster moving and attacks,
// on both screens), the refresh of the teammate's cooldowns on their own game (not the ultimate), the caster's own cooldown
// kept, the teammate nearest the cursor, nobody in reach (half on yourself, no refresh), going down ending the Surge,
// mastery (a second thread) and Deep Reserves.
static partial class Program
{
    static void ChakraReservesTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);

        var def = Upgrades.Ability(AbilityId.ChakraReserves);
        Check(def.Name == "Chakra Reserves" && def.Choice == AbilityDef.Fourth && def.Kit == Kit.Ninja && def.Role == Role.Healer && def.Cooldown == Game.CrCooldown,
            "Chakra Reserves: a 28 s Healer Ninja 1st ability on the 4th key");
        Check(Upgrades.AbilityLimit(Role.Healer, Kit.Ninja) == 4 && Upgrades.FirstAbility(Role.Healer, Kit.Ninja, 3) == AbilityId.ChakraReserves
            && Upgrades.FirstAbility(Role.Healer, Kit.Ninja, 0) == AbilityId.HealingDash && Upgrades.FirstAbility(Role.Healer, Kit.Ninja, 2) == AbilityId.SpiritWard,
            "the Healer Ninja's 4th key is Chakra Reserves; its first three are unchanged");
        Check(Upgrades.All.Any(u => u.ClassAbility && u.Grants == AbilityId.ChakraReserves) && Upgrades.All.Any(u => u.Name == "Deep Reserves" && u.RequiresAbility == AbilityId.ChakraReserves && u.Rarity == Rarity.Rare),
            "Chakra Reserves is a very rare card for Healer Ninjas who picked another, and Deep Reserves a Rare card for it");

        int port = nextPort++;
        var hostS = new Session { AutoStart = 2, StartWave = 3 };
        hostS.Host(port, "Weaver", Role.Healer, 0, 3, Kit.Ninja);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0, 0, Kit.Soldier);
        var s = new[] { hostS, client };
        WaitForGame(hostS, client, dt);
        var g = hostS.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];
        var allyOwn = client.Game.Players[2];
        Check(me.Slots[0].Id == AbilityId.ChakraReserves && client.Game.Players[1].Slots[0].Id == AbilityId.ChakraReserves, "the 4th pick reaches the run, on both screens");
        g.DevSetGod(1, true);

        V2 home = new V2(500f, 450f), allyAt = new V2(700f, 450f);
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                g.Enemies.Clear(); g.Bullets.Clear();
                me.Pos = home; ally.Pos = allyAt;
                hostS.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = allyAt });
            }
        };
        Action reset = () =>
        {
            step(0.2f);
            g.Effects.Clear(); client.Game.Effects.Clear();
            me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f; me.Downed = false; me.Hp = me.MaxHp; me.CrSurgeT = 0f;
            ally.Downed = false; ally.CrSurgeT = 0f; me.MasteryXp.Remove(AbilityId.ChakraReserves); me.Stats.CrBonusTime = 0f; me.Stats.CrBonusHeal = 0f;
        };
        Action<Player> spend = q =>  // put the ally's abilities on cooldown (theirs and the host's copy)
        {
            foreach (var p in new[] { q, allyOwn })
            {
                p.Slots[0].Charges = 0; p.Slots[0].Timer = 9f;
                p.Slots[Upgrades.UltSlot].Charges = 0; p.Slots[Upgrades.UltSlot].Timer = 30f;
            }
        };

        // The real key path: the heal, the Surge and the refresh.
        reset();
        spend(ally);
        ally.Hp = ally.MaxHp * 0.4f;
        float before = ally.Hp, missing = ally.MaxHp - ally.Hp, speed0 = ally.Speed;
        hostS.Poll(); client.Poll();
        g.FixedStep(dt, new PlayerInput { Ability = true, AimAt = allyAt });
        float healed = ally.Hp - before;
        Check(Math.Abs(healed - (Game.CrHeal + Game.CrMissing * missing)) < 1f, "it heals 40 + 15% of what's missing (" + healed.ToString("0.0") + ")");
        Check(g.Effects.Count(f => f.Kind == FxKind.ChakraThread) == 1, "pressing the ability key casts it: one thread");
        Check(ally.CrSurgeT > Game.CrSurgeTime - 0.1f && Math.Abs(ally.Speed / speed0 - Game.CrMove) < 0.01f, "the ally gets the Chakra Surge: 25% faster moving");
        Check(me.Slots[0].Charges == 0 && me.Slots[0].Timer > 20f, "the caster's own cooldown runs");
        Check(ally.Slots[0].Charges == ally.Slots[0].MaxCharges && ally.Slots[Upgrades.UltSlot].Charges == 0, "the host's copy: their 1st ability is back, the ultimate is not");
        step(0.15f);
        Check(allyOwn.Slots[0].Charges == allyOwn.Slots[0].MaxCharges && allyOwn.Slots[Upgrades.UltSlot].Charges == 0 && allyOwn.Slots[Upgrades.UltSlot].Timer > 20f,
            "their own game: the 1st ability refreshed, the ultimate untouched");
        Check(allyOwn.CrSurgeT > 0f && client.Game.Effects.Any(f => f.Kind == FxKind.ChakraThread), "the other screen sees the Surge and the thread");
        step(Game.CrSurgeTime);
        Check(ally.CrSurgeT <= 0f && Math.Abs(ally.Speed - speed0) < 0.01f, "the Surge runs out after 5 s");

        // Nobody in reach: half on yourself, no refresh.
        reset();
        allyAt = new V2(1200f, 450f);
        step(0.1f);
        spend(ally);
        me.Hp = me.MaxHp * 0.5f;
        float mine = me.Hp, myMissing = me.MaxHp - me.Hp;
        hostAbility.Invoke(g, new object[] { me.Id, 0, allyAt, false });
        Check(Math.Abs((me.Hp - mine) - Game.CrSolo * (Game.CrHeal + Game.CrMissing * myMissing)) < 1f && Math.Abs(me.CrSurgeT - Game.CrSurgeTime * Game.CrSolo) < 0.05f,
            "with no ally within 460 px it goes on you: half the heal and half the Surge");
        Check(ally.CrSurgeT <= 0f && ally.Slots[0].Charges == 0, "the ally out of reach gets nothing");

        // Going down ends the Surge.
        allyAt = new V2(700f, 450f);
        reset();
        hostAbility.Invoke(g, new object[] { me.Id, 0, allyAt, false });
        Check(ally.CrSurgeT > 0f, "a fresh Surge on the ally");
        ally.Hp = 0f;
        typeof(Game).GetMethod("DownIfDead", flags).Invoke(g, new object[] { ally, "test" });
        Check(ally.Downed && ally.CrSurgeT <= 0f && Math.Abs(ally.Speed - ally.Def.Speed * ally.Stats.SpeedMult * Game.MomentumMult(ally) * Defs.KitSpeedMult(ally.Kit)) < 50f, "going down ends the Surge");
        ally.Downed = false; ally.Hp = ally.MaxHp; ally.Wound = 0f;  // (a down leaves a wound that caps healing)

        // Deep Reserves: +1.5 s of Surge and +10 heal.
        reset();
        me.Stats.CrBonusTime = Game.CrCardTime; me.Stats.CrBonusHeal = Game.CrCardHeal;
        ally.Hp = ally.MaxHp * 0.2f;  // (enough missing that it can't reach full)
        before = ally.Hp; missing = ally.MaxHp - ally.Hp;
        hostAbility.Invoke(g, new object[] { me.Id, 0, allyAt, false });
        Check(Math.Abs(ally.CrSurgeT - (Game.CrSurgeTime + Game.CrCardTime)) < 0.05f && Math.Abs((ally.Hp - before) - (Game.CrHeal + Game.CrCardHeal + Game.CrMissing * missing)) < 1f,
            "Deep Reserves: the Surge lasts 1.5 s longer and it heals 10 more");

        // Mastered: a second thread, but only with a second teammate; alone with one ally there's just the one.
        reset();
        me.MasteryXp[AbilityId.ChakraReserves] = 99999f;
        g.Effects.Clear();
        hostAbility.Invoke(g, new object[] { me.Id, 0, allyAt, false });
        Check(g.Effects.Count(f => f.Kind == FxKind.ChakraThread) == 1, "mastered with one ally: one thread (no one else to reach)");

    }
}
