using System;

namespace Arena
{
    /// Blood Boil: the DPS Hemomancer's third ultimate (key 2 of the lobby's Ultimate row; Crimson Harvest stays the default),
    /// from the mockup (claude.ai artifact "Blood Boil").
    ///   - Your blood boils for BoilTime s: it drains BoilDrain of your max health a second (never below BoilFloor, so it
    ///     can't down you; Bloodthirst makes it cheaper).
    ///   - The boil (BoilLevel, 0 to 1) follows your health while it runs: 0 at full, 1 at BoilFull of max health. You move
    ///     1 + BoilSpeed x boil times as fast, fire 1 + BoilFire x boil times as fast and deal 1 + BoilDmg x boil times the
    ///     damage (BoilDmgMastered mastered), on top of Blood Price.
    ///   - It ends when the time runs out, when you press it again, you go down, the wave ends or the slot changes. You keep
    ///     the health you're at; speed, fire rate and damage go straight back to normal.
    /// Replicated: Player.BoilT (seconds since the cast; 0 = off) and BoilLen (its length). The boil comes from your health,
    /// which is replicated too, so every game moves and fires you the same. FxKind.BoilEnd is the steam burst at the end.
    /// Drawn by Game/BloodBoilLook.cs.
    public partial class Game
    {
        public const float BoilTime = 10f, BoilDrain = 0.07f, BoilFloor = 0.05f, BoilFull = 0.1f;
        public const float BoilSpeed = 0.7f, BoilFire = 1f, BoilDmg = 0.6f, BoilDmgMastered = 0.9f;
        public static readonly Rgba BoilHot = new Rgba(1f, 0.42f, 0.17f), BoilBlood = new Rgba(0.76f, 0.08f, 0.17f), BoilSteam = new Rgba(0.95f, 0.9f, 0.87f);

        /// Blood Boil's entry: key 2 of the DPS Hemomancer's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] BoilUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.BloodBoil, Kit = Kit.Hemomancer, Role = Role.Dps, Choice = AbilityDef.Ult, UltKey = 2,
                Name = "Blood Boil", Cooldown = 70f,
                Desc = "Your health drains for 10 s; the lower it gets, the faster you move and fire and the harder you hit. Press again to end it.",
                Capstone = "Up to +90% damage at full boil.",
                Bot = b => b.HaveTarget && b.TargetD < 500f && b.Me.Hp > b.Me.MaxHp * 0.6f,
            },
        };

        /// The blood is boiling.
        public static bool BoilOn(Player p) => p.BoilT > 0f && !p.Downed;

        /// How hard it boils, 0 to 1: 0 at full health, 1 at BoilFull of max health; 0 when it isn't on.
        public static float BoilLevel(Player p)
        {
            if (!BoilOn(p) || p.MaxHp <= 0f)
                return 0f;
            return Math.Max(0f, Math.Min(1f, (1f - p.Hp / p.MaxHp) / (1f - BoilFull)));
        }

        /// Your speed share (Player.Speed) while it boils.
        public static float BoilSpeedMult(Player p) => 1f + BoilSpeed * BoilLevel(p);

        /// Your fire rate share (FireRate) while it boils.
        public static float BoilFireMult(Player p) => 1f + BoilFire * BoilLevel(p);

        /// Your damage share (BloodDamageMult, host) while it boils.
        float BoilDmgMult(Player p) => 1f + (Mastered(p, AbilityId.BloodBoil) ? BoilDmgMastered : BoilDmg) * BoilLevel(p);

        /// True if this slot is a Blood Boil that's running: pressing it ends it instead of casting again.
        public bool BoilPress(Player p, int slot) => p.Slots[slot].Id == AbilityId.BloodBoil && p.BoilT > 0f;

        void CastBoil(Player p)
        {
            p.BoilLen = BoilTime;
            p.BoilT = 0.0001f;
            p.UltT = p.BoilLen;
            BroadcastFx(FxKind.Ring, p.Pos, BoilHot, 70f, 0.4f, Sfx.SpellHoly);
        }

        /// It stops: a burst of steam (when it boiled at all), and everything goes back to normal. You keep your health.
        void EndBoil(Player p)
        {
            if (p.BoilT <= 0f)
                return;
            p.BoilT = 0f;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.BloodBoil)
                p.UltT = 0f;
            if (!p.Downed)
                BroadcastFx(FxKind.BoilEnd, p.Pos, new V2(p.Id, 0f), BoilSteam, 60f, BoilEndTime, Sfx.None);
        }

        /// How long the steam burst at the end lasts.
        public const float BoilEndTime = 1.2f;

        /// Host, every tick: the clock and the drain.
        void TickBoil(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.BoilT <= 0f)
                    continue;
                if (p.Downed || !Battle || p.Slots[Upgrades.UltSlot].Id != AbilityId.BloodBoil)
                {
                    EndBoil(p);
                    continue;
                }
                p.BoilT += dt;
                if (p.BoilT >= p.BoilLen)
                {
                    EndBoil(p);
                    continue;
                }
                float room = p.Hp - p.MaxHp * BoilFloor;
                if (room > 0f)
                    PayHealth(p, Math.Min(room / p.Stats.HealthCostMult, p.MaxHp * BoilDrain * dt), "Blood Boil");
            }
        }

        // --- Network: the player's Blood Boil fields (SyncPlayer) ---

        void SyncBloodBoil(NetSync s, Player p)
        {
            s.Float(ref p.BoilT);  // Blood Boil
            if (p.BoilT > 0f)
                s.Float(ref p.BoilLen);
        }
    }
}
