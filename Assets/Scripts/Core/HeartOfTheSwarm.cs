using System;

namespace Arena
{
    /// Heart of the Swarm: the Healer Hemomancer's third ultimate (key 2 of the lobby's Ultimate row; Sanguine Rain stays the
    /// default), from the mockup (claude.ai artifact "Heart of the Swarm").
    ///   - A giant heart appears at your cursor and follows it (HsFollow px/s) for HsTotal s.
    ///   - It beats HsBeats times: the first HsWind s after the cast, then one every HsGap s.
    ///   - Each beat heals every other ally within HsReach px of the heart HsHeal of their max health (HsHealMastered mastered)
    ///     and yanks every enemy in reach HsPull px toward it, never past the heart (heavy enemies and bosses aren't moved, as with every push).
    ///   - No health cost. It ends after the last beat's wave, or early when you go down, the wave ends or the slot changes.
    /// Replicated: Player.HsT (seconds since the cast; 0 = off) and Player.HsPos (the heart). The beats follow from HsT, so every
    /// game draws them the same. Drawn by Game/HeartOfTheSwarmLook.cs.
    public partial class Game
    {
        public const int HsBeats = 6;
        public const float HsWind = 0.35f, HsGap = 1.5f, HsWaveTime = 0.6f, HsReach = 300f, HsFollow = 900f;
        public const float HsHeal = 0.08f, HsHealMastered = 0.11f, HsPull = 70f;

        /// Heart of the Swarm's entry: key 2 of the Healer Hemomancer's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] SwarmHeartUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.HeartOfTheSwarm, Kit = Kit.Hemomancer, Role = Role.Healer, Choice = AbilityDef.Ult, UltKey = 2,
                Name = "Heart of the Swarm", Cooldown = 80f,
                Desc = "A giant heart beats at your cursor six times, once every 1.5 s: each beat heals allies within 300 px of it 8% of their health and pulls enemies in.",
                Capstone = "Each beat heals 11%.",
                Bot = b => b.NearEnemies >= 4,
            },
        };

        /// The heart is beating.
        public static bool HsOn(Player p) => p.HsT > 0f && !p.Downed;

        /// When beat k (0-based) comes, in seconds since the cast.
        public static float HsBeatAt(int k) => HsWind + k * HsGap;

        /// The whole cast: the last beat's wave reaching its end.
        public static float HsTotal => HsBeatAt(HsBeats - 1) + HsWaveTime;

        /// How many beats have come by `t` seconds since the cast.
        public static int HsFired(float t)
        {
            int n = 0;
            while (n < HsBeats && t >= HsBeatAt(n))
                n++;
            return n;
        }

        void CastSwarmHeart(Player p, bool mastered)
        {
            p.HsT = 0.0001f;
            p.HsMastered = mastered;
            p.HsPos = ClampToArena(p.AimAt, 40f);
            p.UltT = HsTotal;
            BroadcastFx(FxKind.Ring, p.HsPos, HbBlood, 60f, 0.35f, Sfx.None);
        }

        void EndSwarmHeart(Player p)
        {
            if (p.HsT <= 0f)
                return;
            p.HsT = 0f;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.HeartOfTheSwarm)
                p.UltT = 0f;
        }

        /// One beat: heal the allies in reach, yank the enemies in.
        void SwarmHeartBeat(Player p)
        {
            p.HsBeatCount++;
            float frac = p.HsMastered ? HsHealMastered : HsHeal;
            foreach (var q in Players.Values)
                if (q != p && !q.Downed && q.Pos.CloserThan(p.HsPos, HsReach + q.Def.BodyR))
                {
                    p.HsHealed += Heal(q, q.MaxHp * frac, p);
                    BroadcastFx(FxKind.Ring, q.Pos, HbCrest, q.Def.BodyR + 10f, 0.3f, Sfx.None);
                }
            foreach (var e in AllFoes(by: p.Id))
            {
                if (e.SpawnT > 0f || e.Hp <= 0f)
                    continue;
                V2 to = p.HsPos - e.Pos;
                float d = to.Length, gap = d - e.Radius;
                if (gap > HsReach || d < 1f || Heavy(e))
                    continue;
                float pull = Math.Min(HsPull, Math.Max(0f, d - e.Radius - 8f));
                if (pull <= 0f)
                    continue;
                KnockEnemy(e, to * (pull / d));
                p.HsPulls++;
            }
            BroadcastFx(FxKind.Ring, p.HsPos, HbBlood, 80f, 0.25f, Sfx.Shockwave);
        }

        /// Host, every tick: the clock, the heart following the cursor, the beats.
        void TickSwarmHeart(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.HsT <= 0f)
                    continue;
                if (p.Downed || !Battle || p.Slots[Upgrades.UltSlot].Id != AbilityId.HeartOfTheSwarm)
                {
                    EndSwarmHeart(p);
                    continue;
                }
                float prev = p.HsT;
                p.HsT += dt;
                p.HsPos = ClampToArena(p.HsPos + (p.AimAt - p.HsPos).LimitLength(HsFollow * dt), 40f);
                for (int k = 0; k < HsBeats; k++)
                {
                    float at = HsBeatAt(k);
                    if (prev < at && p.HsT >= at)
                        SwarmHeartBeat(p);
                }
                if (p.HsT >= HsTotal)
                    EndSwarmHeart(p);
            }
        }

        // --- Network: the player's Heart of the Swarm fields (SyncPlayer) ---

        void SyncSwarmHeart(NetSync s, Player p)
        {
            s.Float(ref p.HsT);
            if (p.HsT > 0f)
                s.Vec(ref p.HsPos);
        }
    }
}
