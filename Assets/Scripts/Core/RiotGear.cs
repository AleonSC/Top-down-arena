using System;

namespace Arena
{
    /// Riot Gear (Perks.cs, Docs/RiotGear.md): a shield that blocks the next hit that would hurt you, gives half a second of
    /// invulnerability, then takes 30 s to come back. Damage over time (burning, gas, acid, lava) goes through.
    public partial class Game
    {
        public static bool HasPerk(Player p, PerkId id) => p != null && Array.IndexOf(p.Perks, id) >= 0;

        /// Is Riot Gear up for this player?
        public static bool RiotReady(Player p) => HasPerk(p, PerkId.RiotGear) && p.RiotT <= 0f;

        /// Match start (everyone, from the Start message): the perks a player brings, and the shield ready.
        void GivePerks(Player p, PerkId[] ids)
        {
            p.Perks = ids;
            p.RiotT = 0f;
        }

        /// Host: the perks this lobby entry takes into the match. None in the Practice Range, the lobby preview or the Boss Lab
        /// (anywhere runs aren't recorded); a continued run gives each seat back the perks it was saved with.
        PerkId[] PerksFor(LobbyEntry e)
        {
            if (!RecordsRuns)
                return Perks.Empty;
            int seat;
            if (ResumeFrom != null && ResumeSeats.TryGetValue(e.Id, out seat) && seat >= 0 && seat < ResumeFrom.Seats.Count)
                return Perks.FromNames(ResumeFrom.Seats[seat].Perks);
            return e.ChosenPerks;
        }

        bool tickDamage;  // set while DamageOverTime runs

        /// A burn, gas, acid or lava tick: it hurts like any damage, but Riot Gear's shield doesn't block it.
        void DamageOverTime(Player p, float amount, string source)
        {
            tickDamage = true;
            try { Damage(p, amount, source); }
            finally { tickDamage = false; }
        }

        /// Called first in Damage. True: the shield took the hit, so nothing else about it happens.
        bool RiotBlocks(Player p, string source)
        {
            if (!RiotReady(p))
                return false;
            p.RiotT = Perks.RiotRecharge;
            p.InvulnT = Math.Max(p.InvulnT, Perks.RiotGraceT);
            p.Flash = 0.1f;
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.56f, 0.71f, 0.85f), 60f, 0.5f, Sfx.Clang);
            BroadcastFx(FxKind.Spark, p.Pos, new Rgba(1f, 1f, 1f), 34f, 0.35f, Sfx.None);
            DoLog(p.Name + ": Riot Gear blocked " + source);
            return true;
        }

        /// Counts the recharge down (every frame, host and client; the host's number is the real one).
        void TickRiot(Player p, float dt)
        {
            if (p.RiotT <= 0f)
                return;
            p.RiotT = Math.Max(0f, p.RiotT - dt);
            if (p.RiotT <= 0f && IsHost && HasPerk(p, PerkId.RiotGear))
                BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.56f, 0.71f, 0.85f), 40f, 0.5f, Sfx.None);  // it's back
        }
    }
}
