using System;

namespace Arena
{
    /// Heartbeat: the Healer Hemomancer's second ultimate (key 1 of the lobby's Ultimate row; Sanguine Rain stays the default),
    /// from the mockup (claude.ai artifact "Hemomancer Heartbeat Ult").
    ///   - Your heart beats HbBeats times: the first HbWind s after the cast, then one every HbGap s.
    ///   - Each beat costs HbCost of your max health (never below HbFloor, so it can't down you; Bloodthirst makes it
    ///     cheaper) and sends a wave of blood out of you to HbReach px in HbWaveTime s. The wave hits each enemy it reaches
    ///     once for HbDamage (HbDamageMastered mastered; scaled by your damage) and shoves it HbShove px.
    ///   - Every enemy a wave kills drops a blood orb (Combat.cs DropBloodOrb, Enemy.HeartKill): the Hemomancer's shared one,
    ///     25 health to whoever drinks it, drifting to a hurt player. Not where health orbs are off (Famine, Bloodlust, Blood Moon).
    ///   - It ends after the last wave, or early when you go down, the wave ends or the slot changes.
    /// Replicated: Player.HbT (seconds since the cast; 0 = off). The beats and waves follow from it, so every game draws them
    /// the same. Drawn by Game/HeartbeatLook.cs.
    public partial class Game
    {
        public const int HbBeats = 3;
        public const float HbWind = 0.35f, HbGap = 0.9f, HbWaveTime = 0.6f, HbReach = 320f;
        public const float HbCost = 0.08f, HbFloor = 0.15f, HbDamage = 45f, HbDamageMastered = 63f, HbShove = 26f;
        public static readonly Rgba HbBlood = new Rgba(0.78f, 0.09f, 0.16f), HbCrest = new Rgba(1f, 0.55f, 0.55f), HbDark = new Rgba(0.33f, 0.02f, 0.05f);

        /// Heartbeat's entry: key 1 of the Healer Hemomancer's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] HeartbeatUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.Heartbeat, Kit = Kit.Hemomancer, Role = Role.Healer, Choice = AbilityDef.Ult, UltKey = 1,
                Name = "Heartbeat", Cooldown = 80f,
                Desc = "Your heart beats three times, each beat costing 8% of your health: a wave of blood bursts 320 px out of you, hitting for 45. Every enemy it kills drops a blood orb.",
                Capstone = "The waves hit 40% harder.",
                Bot = b => b.NearEnemies >= 4 && b.Me.Hp > b.Me.MaxHp * 0.5f,
            },
        };

        /// The heart is beating.
        public static bool HbOn(Player p) => p.HbT > 0f && !p.Downed;

        /// When beat k (0-based) comes, in seconds since the cast.
        public static float HbBeatAt(int k) => HbWind + k * HbGap;

        /// The whole cast: the last beat's wave reaching its end.
        public static float HbTotal => HbBeatAt(HbBeats - 1) + HbWaveTime;

        /// How many beats have come by `t` seconds since the cast.
        public static int HbFired(float t)
        {
            int n = 0;
            while (n < HbBeats && t >= HbBeatAt(n))
                n++;
            return n;
        }

        /// A wave's radius `age` s after its beat: fast out of you, slowing as it reaches HbReach.
        public static float HbWaveR(float age)
        {
            float a = Math.Max(0f, Math.Min(1f, age / HbWaveTime));
            return HbReach * (1f - (1f - a) * (1f - a));
        }

        void CastHeartbeat(Player p, bool mastered)
        {
            p.HbT = 0.0001f;
            p.HbMastered = mastered;
            p.UltT = HbTotal;
            BroadcastFx(FxKind.Ring, p.Pos, HbBlood, 30f, 0.3f, Sfx.None);
        }

        void EndHeartbeat(Player p)
        {
            if (p.HbT <= 0f)
                return;
            p.HbT = 0f;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.Heartbeat)
                p.UltT = 0f;
        }

        /// One beat: it pays its health, then the wave starts.
        void HeartbeatPay(Player p)
        {
            float room = p.Hp - p.MaxHp * HbFloor;
            if (room > 0f)
                p.HbPaid += PayHealth(p, Math.Min(room / p.Stats.HealthCostMult, p.MaxHp * HbCost), "Heartbeat");
            p.HbBeatCount++;
            BroadcastFx(FxKind.Ring, p.Pos, HbBlood, 46f, 0.25f, Sfx.Shockwave);
        }

        /// Host, every tick: the clock, the beats and the waves' hits.
        void TickHeartbeat(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.HbT <= 0f)
                    continue;
                if (p.Downed || !Battle || p.Slots[Upgrades.UltSlot].Id != AbilityId.Heartbeat)
                {
                    EndHeartbeat(p);
                    continue;
                }
                float prev = p.HbT;
                p.HbT += dt;
                float dmg = (p.HbMastered ? HbDamageMastered : HbDamage) * p.Stats.DmgMult;
                int sparks = 0;
                for (int k = 0; k < HbBeats; k++)
                {
                    float at = HbBeatAt(k);
                    if (prev < at && p.HbT >= at)
                        HeartbeatPay(p);
                    float age0 = prev - at, age1 = p.HbT - at;
                    if (age1 < 0f || age0 > HbWaveTime)
                        continue;
                    float r0 = age0 <= 0f ? -1e9f : HbWaveR(age0), r1 = HbWaveR(age1);
                    foreach (var e in AllFoes(by: p.Id))
                    {
                        if (e.SpawnT > 0f || e.Hp <= 0f)
                            continue;
                        float edge = e.Pos.DistanceTo(p.Pos) - e.Radius;
                        if (edge > r1 || edge <= r0)
                            continue;
                        HurtEnemy(e, dmg, p.Id);
                        if (e.Hp <= 0f)
                            e.HeartKill = true;
                        ShoveFrom(e, p.Pos, HbShove);
                        p.HbHits++;
                        if (sparks++ < 8)
                            BroadcastFx(FxKind.Spark, e.Pos, e.Pos, HbBlood, e.Radius + 4f, 0.25f, Sfx.Hit);
                    }
                }
                if (p.HbT >= HbTotal)
                    EndHeartbeat(p);
            }
        }
    }
}
