using System.Linq;
using System;
using Arena;

// Wildcard (Docs/Wildcard.md): a fourth role that borrows DPS gear from any class. Checks the loadout it resolves to (weapon,
// two abilities with the second on its chosen key, any class's DPS ultimate picked on the Ultimate row), the middle health, the damage and
// cooldown penalty, that class passives don't carry (no Hemomancer low-health bonus), moving the second ability to another key,
// the lobby clamps, what a client sees, and the unlock (a win with each of the other three roles).
static partial class Program
{
    static void WildcardTest(float dt)
    {
        // Picks: weapon = Ninja's second DPS weapon; first ability = Elementalist's first (Meteor); Wildcard ability = Hemomancer's first, on the 2nd slot.
        int weapon = (int)Kit.Ninja * Defs.WeaponsPerRole + 1;
        int first = (int)Kit.Elementalist * Defs.WeaponsPerRole;
        int second = (int)Kit.Hemomancer * Defs.WeaponsPerRole;
        int port = nextPort++;
        var host = new Session { AutoStart = 2 };
        host.Host(port, "Host", Role.Wildcard, weapon, first, Kit.Soldier, Defs.MakeWild(second), Upgrades.WildUlt(Kit.Ninja, 0));
        var client = JoinAs(port, false, "Friend", Role.Tank, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var p = g.Players[1];

        Check(p.Role == Role.Wildcard, "the host is a Wildcard");
        Check(p.Kit == Kit.Soldier, "a Wildcard has no class of its own (no class health, speed or passives)");
        Check(Math.Abs(p.BaseHp - 125f) < 0.01f, "a Wildcard has 125 health, got " + p.BaseHp);
        Check(p.Weapon.Id == Defs.Weapon(Role.Dps, Kit.Ninja, 1).Id, "the weapon is the Ninja's second DPS weapon");
        Check(p.FirstAbility == Upgrades.FirstAbility(Role.Dps, Kit.Elementalist, 0), "the first ability is the Elementalist's Meteor");
        var bonus = Upgrades.FirstAbility(Role.Dps, Kit.Hemomancer, 0);
        Check(p.Slots[1].Id == bonus && p.Slots[2].Id == AbilityId.None && p.Slots[3].Id == AbilityId.None,
            "the Wildcard ability sits on the 2nd slot (slot 1) and the 3rd and 4th are free");
        Check(p.Slots[Upgrades.UltSlot].Id == AbilityId.DeathLotus && p.WildKit == Kit.Ninja, "the ultimate is the one picked (the Ninja's Death Lotus), not the first ability's class's");
        Check(p.Slots.Length == Upgrades.SlotCount, "a Wildcard has the same number of ability slots");

        // The price: less damage and slower cooldowns than the same build as a DPS.
        Check(Math.Abs(p.Stats.DmgMult - Game.WildcardDmgMult) < 0.001f, "damage is x" + Game.WildcardDmgMult + ", got " + p.Stats.DmgMult);
        Check(Math.Abs(p.Stats.CdMult - Game.WildcardCdMult) < 0.001f, "cooldowns are x" + Game.WildcardCdMult + ", got " + p.Stats.CdMult);
        float baseCd = Upgrades.Ability(p.FirstAbility).Cooldown;
        Check(Math.Abs(g.Cooldown(p, 0) - baseCd * Game.WildcardCdMult) < 0.01f, "the 1st ability recharges 25% slower");
        float ultBase = Upgrades.Ability(p.Slots[Upgrades.UltSlot].Id).Cooldown;
        Check(g.Cooldown(p, Upgrades.UltSlot) >= ultBase * Game.WildcardCdMult - 0.01f, "the ultimate recharges slower too");

        // A client sees the same slots.
        Run(s, dt, 1, () => false);
        var seen = client.Game.Players[1];
        Check(seen.Role == Role.Wildcard && seen.Slots[1].Id == bonus, "the client sees the Wildcard ability on the 2nd slot");

        // Move the Wildcard ability to the 4th slot: charges follow, the key it left is free.
        g.SetAbilityKey(bonus, 3);
        Check(p.Slots[3].Id == bonus && p.Slots[1].Id == AbilityId.None, "the Wildcard ability moves to the 4th slot");
        Run(s, dt, 1, () => false);
        Check(client.Game.Players[1].Slots[3].Id == bonus, "the client sees it move");

        // Lobby clamps: the second ability can't equal the first, and a normal role keeps three choices.
        var lobby = new Session();
        lobby.Host(nextPort++, "A", Role.Dps, 7, 7, Kit.Knight, 200);
        var e = lobby.Lobby[1];
        Check(e.Weapon == 0 && e.Ability == 0 && e.Wild == 0, "a DPS' out-of-range picks fall back to the first");
        lobby.RequestRole(Role.Wildcard);
        e = lobby.Lobby[1];
        Check(e.Role == Role.Wildcard && e.Kit == Kit.Soldier, "choosing Wildcard clears the class");
        lobby.RequestWildcard(5, 4, 4);
        e = lobby.Lobby[1];
        Check(Defs.WildSecond(e.Wild) != e.Ability, "a Wildcard ability equal to the first is moved on");
        lobby.RequestWildcard(99, 99, 99);
        e = lobby.Lobby[1];
        Check(e.Weapon < Defs.WildcardChoices && e.Ability < Defs.WildcardChoices && e.Wild < Defs.WildcardChoices, "out-of-range Wildcard picks are clamped");
        int knuckles = Defs.WildWeaponPick(Kit.Knight, 3);
        lobby.RequestWildcard(knuckles, 2, 5);
        e = lobby.Lobby[1];
        // (a class's 4th DPS weapon comes after every class's three, then after the 4th weapons of the classes before it)
        Func<Kit, int> wildFourth = kit => Defs.WildcardChoices + Enumerable.Range(0, (int)kit).Count(k => Content.WeaponCount(Role.Dps, (Kit)k) > Defs.WeaponsPerRole);
        int barrel = Defs.WildWeaponPick(Kit.Soldier, 3);
        Check(barrel == wildFourth(Kit.Soldier) && Defs.Weapon(Role.Wildcard, Kit.Soldier, barrel).Id == WeaponId.DoubleBarrel,
            "the fourth weapons come after every class's three, in class order: the Soldier's Double Barrel first (pick " + barrel + ")");
        Check(knuckles == wildFourth(Kit.Knight) && e.Weapon == knuckles && Defs.Weapon(Role.Wildcard, Kit.Soldier, e.Weapon).Id == WeaponId.ArcKnuckles,
            "a Wildcard can pick the Knight DPS's fourth weapon, the Arc Knuckles (pick " + knuckles + ", after the earlier classes' fourth weapons)");
        int spear = Defs.WildWeaponPick(Kit.Elementalist, 3);
        Check(spear == wildFourth(Kit.Elementalist) && Defs.Weapon(Role.Wildcard, Kit.Soldier, spear).Id == WeaponId.Stonespear && Defs.WildWeaponKit(spear) == Kit.Elementalist,
            "and the Elementalist DPS's Stonespear (pick " + spear + ", in class order)");
        int bees = Defs.WildWeaponPick(Kit.Summoner, 3);
        Check(bees == wildFourth(Kit.Summoner) && Defs.Weapon(Role.Wildcard, Kit.Soldier, bees).Id == WeaponId.HoneybeeStaff && Defs.WildWeaponKit(bees) == Kit.Summoner,
            "and the Summoner DPS's Honeybee Staff (pick " + bees + ", in class order)");
        int chucks = Defs.WildWeaponPick(Kit.Ninja, 3);
        Check(chucks == wildFourth(Kit.Ninja) && Defs.Weapon(Role.Wildcard, Kit.Soldier, chucks).Id == WeaponId.Nunchaku && Defs.WildWeaponKit(chucks) == Kit.Ninja,
            "and the Ninja DPS's Nunchaku (pick " + chucks + ", in class order)");
        int embolus = Defs.WildWeaponPick(Kit.Hemomancer, 3);
        Check(embolus == wildFourth(Kit.Hemomancer) && Defs.Weapon(Role.Wildcard, Kit.Soldier, embolus).Id == WeaponId.EmbolusSpears && Defs.WildWeaponKit(embolus) == Kit.Hemomancer,
            "and the Hemomancer DPS's Embolus Spears (pick " + embolus + ", in class order)");
        Check(Defs.WildWeaponKit(knuckles) == Kit.Knight && Defs.WeaponCount(Role.Wildcard, Kit.Soldier) == Defs.WildcardChoices + Content.DpsFourthWeapons,
            "its class on the wheel is the Knight, and the Wildcard has " + Defs.WeaponCount(Role.Wildcard, Kit.Soldier) + " weapons to pick from (every class's three DPS ones and the "
            + Content.DpsFourthWeapons + " DPS fourth weapons)");
        lobby.RequestUlt(Upgrades.WildUlt(Kit.Hemomancer, 0));
        e = lobby.Lobby[1];
        Check(e.Ult == Upgrades.WildUlt(Kit.Hemomancer, 0) && e.UltKit == Kit.Hemomancer && e.UltId == AbilityId.CrimsonHarvest, "a Wildcard picks any class's DPS ultimate");
        var noKey3 = Content.RoleWithoutUlt(3, Role.Dps, complete: false);  // (a DPS row with nothing on key 3)
        lobby.RequestUlt(Upgrades.WildUlt(noKey3.Kit, 3));
        e = lobby.Lobby[1];
        Check(e.Ult == Upgrades.WildUlt(noKey3.Kit, 0), "a key that class has no ultimate on falls back to its default, the class kept (" + noKey3 + ")");
        lobby.RequestUlt(Defs.KitCount * Upgrades.UltKeys);
        e = lobby.Lobby[1];
        Check(e.Ult == 0, "a pick past the last class is clamped");
        for (int k = 0; k < Defs.KitCount; k++)
            Check(Upgrades.Ultimate(Role.Wildcard, (Kit)k, Upgrades.WildUlt((Kit)k, 0)) == Upgrades.Ultimate(Role.Dps, (Kit)k), "Wildcard ultimate " + k + " is that class's DPS one");
        lobby.RequestRole(Role.Tank);
        e = lobby.Lobby[1];
        Check(e.Wild == 0 && e.Weapon == 0 && e.Ult == 0, "leaving Wildcard clears its picks");

        // Every Wildcard choice maps to a real DPS weapon and ability (and the picks after them to each class's fourth).
        for (int i = Defs.WildcardChoices; i < Defs.WildcardWeaponChoices; i++)
        {
            var wd = Defs.Weapon(Role.Wildcard, Kit.Soldier, i);
            Check(wd.Role == Role.Dps && wd.Choice == Defs.WeaponsPerRole && Defs.WildWeaponPick(wd.Kit, wd.Choice) == i && Defs.WildWeaponKit(i) == wd.Kit,
                "Wildcard weapon " + i + " is " + wd.Kit + "'s fourth DPS weapon");
        }
        for (int i = 0; i < Defs.WildcardChoices; i++)
        {
            var wd = Defs.Weapon(Role.Wildcard, Kit.Soldier, i);
            if (!Defs.WeaponGap(Role.Dps, (Kit)(i / 3), i % 3))  // (a class still being built: its locked places are stand-ins)
                Check(wd.Role == Role.Dps && (int)wd.Kit * 3 + wd.Choice == i, "Wildcard weapon " + i + " is that class's DPS weapon");
            var ab = Upgrades.Ability(Upgrades.FirstAbility(Role.Wildcard, Kit.Soldier, i));
            if (!Upgrades.AbilityGap(Role.Dps, (Kit)(i / 3), i % 3))
                Check(ab.Role == Role.Dps && (int)ab.Kit * 3 + ab.Choice == i, "Wildcard ability " + i + " is that class's DPS ability");
        }

        // The unlock: a win with each of the other three roles (any class, solo or co-op).
        var prof = new Profile();
        Check(!prof.WildcardUnlocked, "a new profile has Wildcard locked");
        prof.RunEnded(true, 50, 1000, false, RunMod.None, Role.Tank, Kit.Knight, null, 600f);
        prof.RunEnded(true, 50, 1000, false, RunMod.None, Role.Dps, Kit.Ninja, null, 600f);
        prof.RunEnded(false, 20, 100, false, RunMod.None, Role.Healer, Kit.Soldier, null, 100f);
        Check(!prof.WildcardUnlocked, "losing as the Healer doesn't count");
        prof.RunEnded(true, 50, 1000, false, RunMod.None, Role.Healer, Kit.Summoner, null, 600f);
        Check(prof.WildcardUnlocked, "a win as each of Tank, Healer and DPS unlocks Wildcard");
        Console.WriteLine("  wildcard: hp {0:0}, dmg x{1}, cooldown x{2}, slots {3}/{4}/{5}/{6}/{7}", p.BaseHp, p.Stats.DmgMult, p.Stats.CdMult,
            p.Slots[0].Id, p.Slots[1].Id, p.Slots[2].Id, p.Slots[3].Id, p.Slots[4].Id);
    }
}
