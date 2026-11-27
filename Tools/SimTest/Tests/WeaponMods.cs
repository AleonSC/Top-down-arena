using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Weapon mods (Core/WeaponMods.cs): each mod says what it does per weapon kind (shot, area, cone, melee), a weapon can
// override one, and a mod that would do nothing for your weapon is never offered.
static partial class Program
{
    static void WeaponModsTest(float dt)
    {
        var mods = Upgrades.All.Select((u, id) => new { u, id }).Where(x => x.u.Rarity == Rarity.Mod).ToList();
        Check(mods.Count == 8 && mods.All(x => x.u.Mod != ModId.None) && mods.Select(x => x.u.Mod).Distinct().Count() == mods.Count
              && Enum.GetValues(typeof(ModId)).Cast<ModId>().Where(m => m != ModId.None).All(m => WeaponMods.Card(m).Name == mods.First(x => x.u.Mod == m).u.Name),
            "every weapon mod has its own ModId, and WeaponMods.Card finds each (" + mods.Count + " mods)");
        Check(Upgrades.All.Where(u => u.Rarity != Rarity.Mod).All(u => u.Mod == ModId.None), "only weapon mods carry a ModId");

        // Every weapon x every mod: the card text is filled in, and it's offered unless it would do nothing.
        int pairs = 0, off = 0;
        var bad = new List<string>();
        foreach (var w in Defs.Weapons)
        {
            var offer = Upgrades.ModOffer(new OfferCtx(w.Role, AbilityId.None, new List<int>(), w.Id)) ?? new int[0];
            foreach (var x in mods)
            {
                pairs++;
                string desc = Upgrades.DescFor(x.u, w);
                bool isOff = WeaponMods.Off(x.u, w);
                if (isOff) off++;
                if (string.IsNullOrEmpty(desc) || desc.Contains("{") || desc.Contains(": :") || desc.Contains(":  "))
                    bad.Add(w.Name + " / " + x.u.Name + ": \"" + desc + "\"");
                if (offer.Contains(x.id) == isOff)
                    bad.Add(w.Name + " / " + x.u.Name + (isOff ? ": offered though it does nothing" : ": not offered"));
            }
        }
        foreach (var b in bad.Take(8)) Console.WriteLine("  " + b);
        Console.WriteLine("  {0} weapon x mod pairs, {1} switched off", pairs, off);
        Check(bad.Count == 0, "every weapon mod has card text for every weapon, and is offered exactly when it does something (" + bad.Count + " wrong)");
        Check(Defs.Weapons.Where(w => w.Mods != null).All(w => w.Mods.Keys.All(m => m != ModId.None)), "weapons only override real mods");

        // Kinds.
        var katana = Defs.Weapons.First(w => w.Id == WeaponId.Katana);
        var pistol = Defs.Weapons.First(w => w.Kind == WeaponKind.Shot);
        var flail = Defs.Weapons.First(w => w.Id == WeaponId.HeartFlail);
        var light = Defs.Weapons.First(w => w.Id == WeaponId.ChainLight);
        Check(katana.Kind == WeaponKind.Melee && light.Kind == WeaponKind.Cone && Defs.Weapons.Any(w => w.Kind == WeaponKind.Area)
              && Defs.Weapons.Where(w => w.Cone).Select(w => w.Id).OrderBy(i => i).SequenceEqual(new[] { WeaponId.ChainLight, WeaponId.LeechTendrils, WeaponId.RepulsionCannon, WeaponId.NanoSprayer }.OrderBy(i => i)),
            "weapons sort into shot, area, cone and melee kinds (the cones: Chain Light, Leech Tendrils, Repulsion Cannon, Nano Sprayer)");

        // Homing Rounds on a melee weapon: swings reach farther (it used to only bend Barrage's sword waves).
        int homing = mods.First(x => x.u.Mod == ModId.Homing).id;
        var three = Enumerable.Repeat(homing, 3).ToList();
        var melee = Upgrades.Compute(three, katana);
        var gun = Upgrades.Compute(three, pistol);
        var flailed = Upgrades.Compute(three, flail);
        var me = new Player { Stats = melee };
        float reach = Game.MeleeReach(me, 100f);
        Console.WriteLine("  Homing x3: katana reach x{0:0.00}, homing {1:0.0}; {2} homing {3:0.0}, reach bonus {4:0.00}; Heart Flail reach bonus {5:0.00}",
            reach / 100f, melee.Homing, pistol.Name, gun.Homing, gun.HomingReach, flailed.HomingReach);
        Check(Math.Abs(reach - 130f) < 0.01f && melee.Homing > 0f, "Homing Rounds x3 on a melee weapon: swings reach 30% farther (and sword waves still bend)");
        Check(gun.HomingReach == 0f && Math.Abs(gun.Homing - 6f) < 0.01f && Upgrades.Compute(three).HomingReach == 0f, "on a gun Homing Rounds only bend shots, as before");
        Check(flailed.HomingReach == 0f && Upgrades.DescFor(Upgrades.All[homing], flail).StartsWith("Heart Flail: the heartbeat"),
            "the Heart Flail (melee, but its hits land in an area) takes Homing Rounds' area version: a bigger area, not reach");
        Check(Upgrades.DescFor(Upgrades.All[homing], katana).Contains("reach 10% farther")
              && Upgrades.DescFor(Upgrades.All[homing], light).StartsWith("Chain Light: the cone of light 15% narrower")
              && Upgrades.DescFor(Upgrades.All[homing], pistol) == Upgrades.All[homing].Desc,
            "the Homing Rounds card says what it does for a melee weapon, Chain Light (its own words) and a gun (the card's own text)");
        var velocity = Upgrades.All[mods.First(x => x.u.Mod == ModId.Velocity).id];
        Check(Upgrades.DescFor(velocity, katana).Contains("25% more damage") && Math.Abs(Upgrades.Compute(new List<int> { mods.First(x => x.u.Mod == ModId.Velocity).id }, katana).DmgMult - 1.25f) < 1e-4f,
            "Velocity Rounds' melee text matches what it gives (25% more damage, was said to be 15%)");

        // A weapon can switch a mod off: it's then never offered.
        var fake = new WeaponDef { Id = katana.Id, Name = "Test", Melee = true, Mods = WeaponMods.Without(ModId.Homing) };
        Check(WeaponMods.Off(Upgrades.All[homing], fake) && !WeaponMods.Off(Upgrades.All[homing], katana) && !WeaponMods.Off(Upgrades.All[homing], null),
            "WeaponMods.Without switches a mod off for one weapon");
    }
}
