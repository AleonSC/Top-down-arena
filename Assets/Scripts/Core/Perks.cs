using System;
using System.Collections.Generic;

namespace Arena
{
    /// Perks: one-per-boss rules you bring into a run, unlocked by winning that boss's Boss Hunt. See Docs/RiotGear.md and
    /// Docs/BossHuntsAndPerks.md. New ids go at the end, before None (they will be sent over the network).
    public enum PerkId : byte { RiotGear, Contraband, None }

    public class PerkDef
    {
        public PerkId Id;
        public EnemyType Boss;
        public string Name;
        public string Line;        // the card's one line
        public string Tip;         // the tooltip
        public string UnlockText;  // what a locked card says
        public Func<Profile, bool> Unlock;
        /// False: listed but not playable yet (its effect isn't built), so it can't be equipped even if unlocked.
        public bool Built;
    }

    public static class Perks
    {
        /// Riot Gear (Warden): after a block the shield takes this long to come back, and you can't be hurt for RiotGraceT.
        public const float RiotRecharge = 30f;
        public const float RiotGraceT = 0.5f;

        public const int MaxSlots = 3;

        public static readonly PerkDef[] All =
        {
            new PerkDef
            {
                Id = PerkId.RiotGear, Boss = EnemyType.Boss, Name = "Riot Gear", Built = true,
                Line = "Blocks a hit every 30 s",
                Tip = "You start every run with a shield that blocks the next hit that would hurt you. A block gives half a second of invulnerability, then the shield takes 30 seconds to come back.",
                UnlockText = "Win the Warden Hunt",
                Unlock = p => p.HuntWins(EnemyType.Boss) > 0,
            },
            new PerkDef
            {
                Id = PerkId.Contraband, Boss = EnemyType.Boss, Name = "Contraband", Built = false,
                Line = "Boss-cycle chests are rare or better",
                Tip = "Chests at the start of each boss cycle are guaranteed rare or better.",
                UnlockText = "Win the Warden Hunt on Hard",
                Unlock = p => false,  // needs a "won on Hard" counter, which comes with the second perk of each boss
            },
        };

        public static PerkDef Get(PerkId id) => Array.Find(All, d => d.Id == id);

        public static PerkDef Get(string name)
        {
            PerkId id;
            return Enum.TryParse(name, out id) && id != PerkId.None ? Get(id) : null;
        }

        /// Is the perk unlocked for this profile (and playable)?
        public static bool Unlocked(Profile p, PerkId id)
        {
            var d = Get(id);
            return d != null && p != null && d.Built && d.Unlock(p);
        }

        /// How many perks this profile may bring: one to start, a second after a first full-run win, a third after winning five different hunts.
        public static int Slots(Profile p)
        {
            if (p == null) return 1;
            int n = 1;
            if (p.RunsWon > 0) n++;
            if (p.HuntsWonCount >= 5) n++;
            return Math.Min(MaxSlots, n);
        }

        /// The equipped perks, in slot order.
        public static List<PerkId> Equipped(Profile p)
        {
            var list = new List<PerkId>();
            if (p == null) return list;
            foreach (var n in p.PerkNames)
            {
                var d = Get(n);
                if (d != null) list.Add(d.Id);
            }
            return list;
        }

        public static bool IsEquipped(Profile p, PerkId id) => p != null && p.PerkNames.Contains(id.ToString());

        /// Equips a perk you've unlocked. A second perk from the same boss takes the first one's slot. False: it isn't yours, or every slot is full.
        public static bool Equip(Profile p, PerkId id)
        {
            if (!Unlocked(p, id))
                return false;
            string name = id.ToString();
            if (p.PerkNames.Contains(name))
                return true;
            var boss = Get(id).Boss;
            for (int i = 0; i < p.PerkNames.Count; i++)
            {
                var other = Get(p.PerkNames[i]);
                if (other != null && other.Boss == boss)
                {
                    p.PerkNames[i] = name;
                    return true;
                }
            }
            if (p.PerkNames.Count >= Slots(p))
                return false;
            p.PerkNames.Add(name);
            return true;
        }

        public static bool Unequip(Profile p, PerkId id) => p != null && p.PerkNames.Remove(id.ToString());

        /// Drops anything that isn't a real, unlocked perk any more, repeats, a second perk from one boss, and what no longer fits the slots.
        public static void Clean(Profile p)
        {
            var keep = new List<string>();
            var bosses = new HashSet<EnemyType>();
            int slots = Slots(p);
            foreach (var n in p.PerkNames)
            {
                var d = Get(n);
                if (d == null || !Unlocked(p, d.Id) || keep.Contains(n) || bosses.Contains(d.Boss) || keep.Count >= slots)
                    continue;
                keep.Add(n);
                bosses.Add(d.Boss);
            }
            p.PerkNames.Clear();
            p.PerkNames.AddRange(keep);
        }
    }
}
