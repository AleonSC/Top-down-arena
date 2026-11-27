using System;

namespace Arena
{
    /// Sacrificial Ritual: the Tank Hemomancer's fourth ultimate (key 3 of the lobby's Ultimate row; Vein Tithe stays the default,
    /// Bloodbath is key 1, Blood Golem key 2). Docs/Mockups/sacrificial-ritual.html (look Bloodmist, claws B Scythe).
    ///   - The rite (SacRite s): a pentagram bleeds under you and five spikes close in. You're rooted and nothing can hurt you.
    ///   - Death: your body stays on the sigil (Player.SacBody) and you rise as a blood spirit at 1 HP for SacTime s (mastered
    ///     SacTimeMastered), SacSpeed faster. Enemies can't see the spirit: none picks it (PickTarget), one already on it looks
    ///     again and goes for the body instead (SacTarget). Nothing can hurt the spirit or the body, and enemy shots pass through.
    ///   - Spirit claws: Fire swings a scythe of blood (your weapon and its Alt are put away): SacClawDamage to every enemy within
    ///     SacClawReach in a SacClawHalf arc either side of your aim, one every SacClawEvery s (FxKind.SacScythe).
    ///   - Each kill while you're the spirit stores SacPerKill (mastered SacPerKillMastered) of your max health, a boss
    ///     SacBossKills kills' worth (a FxKind.Drain stream to the body).
    ///   - The return (time up, the wave over, the slot changed): the spirit goes back to the body and you stand up there with the
    ///     stored health (up to full), or with 1 HP if you got no kills. Ending during the rite does nothing (you never died).
    /// You can't go down while it's on (DownIfDead). Claw damage scales by your damage.
    /// Replicated: Player.SacT (time left, 0 = off) and, while on, SacAge (since the cast), SacBody and SacKills.
    /// Drawn by Game/SacrificialRitualLook.cs.
    public partial class Game
    {
        public const float SacRite = 1f, SacTime = 10f, SacTimeMastered = 12f;
        public const float SacPerKill = 0.1f, SacPerKillMastered = 0.15f;
        public const int SacBossKills = 5;
        public const float SacSpeed = 1.4f;
        public const float SacClawDamage = 40f, SacClawReach = 70f, SacClawHalf = 75f * MathX.Pi / 180f, SacClawEvery = 0.33f, SacClawFx = 0.3f;
        public const int SacBotCrowd = 4;

        public static readonly Rgba SacRed = new Rgba(0.88f, 0.12f, 0.24f);

        /// Sacrificial Ritual's entry: key 3 of the tank Hemomancer's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] SacrificeUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.SacrificialRitual, Kit = Kit.Hemomancer, Role = Role.Tank, Choice = AbilityDef.Ult, UltKey = 3,
                Name = "Sacrificial Ritual", Cooldown = 90f,
                Desc = "Kill yourself on a blood sigil and hunt for 10 s as a spirit enemies can't see. Each kill gives back 10% health when you return to your body; no kills, 1 HP.",
                Capstone = "The spirit lasts 12 s and each kill gives back 15%.",
                Bot = b => b.NearEnemies >= SacBotCrowd && b.Me.Hp < b.Me.MaxHp * 0.4f,
            },
        };

        /// The rite or the spirit.
        public static bool SacOn(Player p) => p.SacT > 0f && !p.Downed;
        /// Dead, out as the spirit (past the rite).
        public static bool SacSpirit(Player p) => SacOn(p) && p.SacAge >= SacRite;
        /// Health stored so far, as a fraction of max health (capped at full).
        public static float SacStored(Player p, bool mastered) => Math.Min(1f, p.SacKills * (mastered ? SacPerKillMastered : SacPerKill));

        /// Your speed share (Player.Speed): rooted through the rite, faster as the spirit.
        public static float SacSpeedOf(Player p) => !SacOn(p) ? 1f : p.SacAge < SacRite ? 0f : SacSpeed;

        void CastSacrifice(Player p, bool mastered)
        {
            p.SacT = SacRite + (mastered ? SacTimeMastered : SacTime);
            p.SacAge = 0f;
            p.SacBody = p.Pos;
            p.SacKills = 0;
            p.SacClawCd = 0f;
            p.SacMastered = mastered;
            p.SacUp = true;
            p.UltT = p.SacT;
            BroadcastFx(FxKind.Ring, p.Pos, SacRed, 60f, 0.6f, Sfx.BlastTideGather);
        }

        /// The spikes close in: you die and rise as the spirit.
        void SacDie(Player p)
        {
            p.SacBody = p.Pos;
            p.Hp = 1f;
            foreach (var e in Enemies.Values)
                if (e.TargetId == p.Id)
                    e.RetargetT = 0f;
            BroadcastFx(FxKind.Ring, p.Pos, SacRed, 80f, 0.5f, Sfx.BlastTideWave);
        }

        /// The spirit goes back to the body (or, ended in the rite, nothing happens).
        void EndSacrifice(Player p)
        {
            if (!p.SacUp)
                return;
            p.SacUp = false;
            bool died = p.SacAge >= SacRite;
            p.SacT = 0f;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.SacrificialRitual)
                p.UltT = 0f;
            if (!died || p.Downed)
                return;
            MovePlayer(p, p.SacBody);
            p.Hp = Math.Max(1f, p.MaxHp * SacStored(p, p.SacMastered));
            p.SacReturns++;
            p.SacLastHp = p.Hp;
            BroadcastFx(FxKind.Ring, p.Pos, SacRed, 90f, 0.6f, Sfx.Revive);
        }

        /// Host, every tick: the clock, the death, the claws' cooldown, the return.
        void TickSacrifice(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (!p.SacUp)
                    continue;
                p.SacT = Math.Max(0f, p.SacT - dt);
                float was = p.SacAge;
                p.SacAge += dt;
                p.SacClawCd -= dt;
                if (p.SacT <= 0f || p.Downed || !Battle || p.Slots[Upgrades.UltSlot].Id != AbilityId.SacrificialRitual)
                {
                    EndSacrifice(p);
                    continue;
                }
                if (p.SacAge < SacRite)
                {
                    p.SacBody = p.Pos;
                    continue;
                }
                if (was < SacRite)
                    SacDie(p);
                p.Hp = 1f;  // dead: heals don't take until you're back
            }
        }

        /// TickPlayers' fire branch while it's on: Fire swings the spirit's scythe (the weapon is put away; nothing in the rite).
        void SacTrigger(Player p)
        {
            if (!SacSpirit(p) || !p.Firing || p.StunT > 0f || p.SacClawCd > 0f || State == MatchState.GameOver)
                return;
            p.SacClawCd = SacClawEvery;
            p.SacClaws++;
            float dmg = SacClawDamage * p.Stats.DmgMult;
            foreach (var e in AllFoes(by: p.Id))
            {
                V2 d = e.Pos - p.Pos;
                if (d.Length > SacClawReach + e.Radius || Math.Abs(MathX.WrapPi(d.Angle - p.Aim)) > SacClawHalf && d.Length > e.Radius + 10f)
                    continue;
                HurtEnemy(e, dmg, p.Id);
            }
            BroadcastFx(FxKind.SacScythe, p.Pos, new V2(p.Aim, p.Id), SacRed, SacClawReach, SacClawFx, Sfx.BlastCleave);
        }

        /// Host, an enemy died (HemomancerKit.OnKill): the spirit that killed it stores health.
        void SacKill(Enemy e)
        {
            var p = PlayerOrNull(e.LastHitBy);
            if (p == null || !p.SacUp || !SacSpirit(p))
                return;
            p.SacKills += e.Def.IsBoss ? SacBossKills : 1;
            BroadcastFx(FxKind.Drain, e.Pos, p.SacBody, SacRed, 3f, 0.6f, Sfx.None);
        }

        /// Enemy movement: one after the spirit (it can't see it) goes for the body instead.
        V2 SacTarget(Player target, V2 tpos) => target != null && SacSpirit(target) ? target.SacBody : tpos;

        // --- Network: the player's Sacrificial Ritual fields (SyncPlayer) ---

        void SyncSacrifice(NetSync s, Player p)
        {
            s.Float(ref p.SacT);
            if (p.SacT > 0f)
            {
                s.Float(ref p.SacAge);
                s.Vec(ref p.SacBody);
                s.Int(ref p.SacKills);
            }
        }
    }
}
