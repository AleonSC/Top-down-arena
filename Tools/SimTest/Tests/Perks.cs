using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Perks (Perks.cs, Docs/RiotGear.md): unlock rules, slots, equipping, and the profile line.
static partial class Program
{
    static void PerksTest(float dt)
    {
        var p = new Profile();
        Check(!Perks.Unlocked(p, PerkId.RiotGear) && Perks.Slots(p) == 1, "a new profile has no perks and one slot");
        Check(!Perks.Equip(p, PerkId.RiotGear) && p.PerkNames.Count == 0, "a locked perk can't be equipped");

        // Beating the Warden in a run opens the hunt, but the perk needs the hunt won.
        p.BossBeaten(EnemyType.Boss, 5);
        Check(!Perks.Unlocked(p, PerkId.RiotGear), "beating the Warden in a run doesn't unlock Riot Gear");
        p.HuntEnded(EnemyType.Boss, false, 60f);
        Check(!Perks.Unlocked(p, PerkId.RiotGear), "losing the Warden Hunt doesn't either");
        p.HuntEnded(EnemyType.Boss, true, 300f);
        Check(Perks.Unlocked(p, PerkId.RiotGear) && p.HuntWins(EnemyType.Boss) == 1 && p.HuntsWonCount == 1, "winning the Warden Hunt unlocks Riot Gear");
        Check(!Perks.Unlocked(p, PerkId.Contraband) && !Perks.Equip(p, PerkId.Contraband), "Contraband stays locked (its effect isn't built)");

        Check(Perks.Equip(p, PerkId.RiotGear) && Perks.IsEquipped(p, PerkId.RiotGear) && Perks.Equipped(p).Count == 1, "Riot Gear equips into the first slot");
        Check(Perks.Equip(p, PerkId.RiotGear) && p.PerkNames.Count == 1, "equipping it again changes nothing");

        // Slots: a second after a run win, a third after five different hunts won.
        p.RunsWon = 1;
        Check(Perks.Slots(p) == 2, "a first run win opens the second slot");
        for (int i = 0; i < 4; i++) p.HuntsWon["Fake" + i] = 1;
        Check(Perks.Slots(p) == 3 && p.HuntsWonCount == 5, "five different hunts won open the third slot");
        p.HuntsWon["Fake0"] = 0;
        Check(Perks.Slots(p) == 2, "a hunt with no wins doesn't count towards the third slot");

        Check(Perks.Unequip(p, PerkId.RiotGear) && p.PerkNames.Count == 0 && !Perks.Unequip(p, PerkId.RiotGear), "a perk comes off, once");

        // One perk per boss: a second perk from the same boss takes the first one's slot. (Contraband is made playable for the check.)
        var con = Perks.Get(PerkId.Contraband);
        var oldBuilt = con.Built; var oldUnlock = con.Unlock;
        try
        {
            con.Built = true; con.Unlock = pr => true;
            Perks.Equip(p, PerkId.RiotGear);
            Check(Perks.Equip(p, PerkId.Contraband) && p.PerkNames.Count == 1 && p.PerkNames[0] == "Contraband", "a second perk from the Warden replaces Riot Gear in its slot");

            // The profile line reads back, and Clean drops what isn't valid.
            Perks.Equip(p, PerkId.RiotGear);
            Check(p.PerkNames.Count == 1, "Riot Gear swaps back in");
            var back = Profile.Parse(p.ToText());
            Check(back.Valid && back.LoadedVersion == Profile.FormatVersion && back.PerkNames.SequenceEqual(p.PerkNames), "the perks line reads back the same");
            Check(p.ToText().Contains("perks RiotGear"), "the profile writes a perks line when one is equipped");
        }
        finally { con.Built = oldBuilt; con.Unlock = oldUnlock; }

        // Cleaning a file that was hand-edited or that lost its unlocks.
        var edited = Profile.Parse("arena-profile 4\nperks RiotGear,Nonsense,RiotGear,Contraband\nend 1\n");
        Check(edited.Valid && edited.PerkNames.Count == 0, "a perk that isn't unlocked, isn't real or is repeated is dropped when the profile loads");
        var kept = Profile.Parse("arena-profile 4\nhunt Boss 1 1 300\nperks RiotGear,RiotGear\nend 1\n");
        Check(kept.Valid && kept.PerkNames.Count == 1 && kept.PerkNames[0] == "RiotGear", "an unlocked perk survives loading, once");
        var old = Profile.Parse("arena-profile 3\nruns 1 0 0\nend 1\n");
        Check(old.Valid && old.PerkNames.Count == 0 && old.LoadedVersion == Profile.FormatVersion, "a version 3 profile loads with no perks");

        // No profile at all never throws.
        Check(Perks.Slots(null) == 1 && !Perks.Unlocked(null, PerkId.RiotGear) && Perks.Equipped(null).Count == 0 && Perks.Get("nope") == null && Perks.Get("RiotGear") != null, "perks handle a missing profile and unknown names");
    }
}
