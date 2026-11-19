using System;
using System.Reflection;

namespace Arena
{
    /// The bots' level-up picks (both bots; Game.cs LocalControl): each offered card is scored and the best one taken,
    /// the way a person reads the cards. What a card does to the core numbers (damage, fire rate, extra shots, health,
    /// damage taken, cooldowns, speed) is measured by applying it to a blank Stats, weighted by the bot's role; a card that
    /// only changes something else (an ability's or a weapon's own numbers) scores as a niche card, more when it was made
    /// for this weapon, class or ability. Rarity adds to it, a new ability adds a lot while a key is free, and a card
    /// already taken counts for less as its copies diminish (Upgrades.CopyStrength). Ties go to the leftmost card.
    public partial class Game
    {
        static readonly FieldInfo[] statFields = typeof(Stats).GetFields(BindingFlags.Public | BindingFlags.Instance);
        static readonly string[] coreStats = { "DmgMult", "FireRateMult", "SpeedMult", "CdMult", "DamageTaken", "BonusHp", "Regen", "ExtraShots", "Pierce", "Fan" };

        /// Which of the offered cards the bot takes.
        int BotPickUpgrade(Player me)
        {
            int best = 0;
            float bestScore = float.MinValue;
            for (int i = 0; i < me.Offer.Length; i++)
            {
                float s = BotCardScore(me, me.Offer[i]);
                if (s > bestScore)
                {
                    bestScore = s;
                    best = i;
                }
            }
            return best;
        }

        public static float BotCardScore(Player me, int id)
        {
            if (id < 0 || id >= Upgrades.All.Length)
                return -999f;
            var u = Upgrades.All[id];
            float score = RarityScore(u.Rarity);
            bool made = u.Weapons != null || u.Kit != null || u.RequiresAbility != AbilityId.None || u.Role != null;  // made for this kit
            if (u.Grants != AbilityId.None || u.ClassAbility)
                score += FreeCardAbilitySlot(me) ? 30f : -10f;
            score += u.RerollsNow * 2f + u.VectorsNow * 0.05f;
            if (u.Apply != null)
            {
                var blank = new Stats();
                var after = new Stats();
                try { u.Apply(after); }
                catch (Exception) { return score; }
                float core = CoreValue(me.Role, after);
                bool niche = false;
                foreach (var f in statFields)
                {
                    if (Array.IndexOf(coreStats, f.Name) >= 0)
                        continue;
                    object a = f.GetValue(blank), b = f.GetValue(after);
                    if (a is int[] ia && b is int[] ib)
                    {
                        for (int k = 0; k < ia.Length && k < ib.Length; k++)
                            niche |= ia[k] != ib[k];
                    }
                    else if (!Equals(a, b))
                        niche = true;
                }
                int copies = 0;
                foreach (int t in me.Taken)
                    if (t == id)
                        copies++;
                float strength = Upgrades.CopyStrength(u, copies + 1);
                score += (core + (niche ? (made ? 16f : 9f) : 0f)) * strength;
                if (copies > 0 && made && strength >= 1f)
                    score += 4f;  // building on a card made for this kit
            }
            return score;
        }

        static float RarityScore(Rarity r)
        {
            switch (r)
            {
                case Rarity.Uncommon: return 4f;
                case Rarity.Rare: return 8f;
                case Rarity.VeryRare: return 12f;
                case Rarity.Legendary: return 20f;
                case Rarity.Mod: return 10f;
                case Rarity.Milestone: return 15f;
                default: return 0f;
            }
        }

        /// What a card's change to the core numbers is worth to this role (a +10% damage card is about 10 for a DPS).
        static float CoreValue(Role role, Stats s)
        {
            float dmg = (s.DmgMult - 1f) * 100f + (s.FireRateMult - 1f) * 90f + s.ExtraShots * 12f + s.Fan * 10f + s.Pierce * 5f;
            float tough = s.BonusHp * 0.12f + s.Regen * 3f + (1f - s.DamageTaken) * 70f;
            float util = (1f - s.CdMult) * 60f + (s.SpeedMult - 1f) * 40f;
            switch (role)
            {
                case Role.Tank: return dmg * 0.6f + tough * 1.6f + util;
                case Role.Healer: return dmg * 0.5f + tough + util * 1.6f;
                default: return dmg + tough * 0.7f + util * 0.8f;
            }
        }

        /// Is one of 2nd, 3rd and 4th still without an ability?
        static bool FreeCardAbilitySlot(Player me)
        {
            for (int k = 1; k <= Upgrades.CardAbilitySlots; k++)
                if (me.Slots[k].Id == AbilityId.None)
                    return true;
            return false;
        }
    }
}
