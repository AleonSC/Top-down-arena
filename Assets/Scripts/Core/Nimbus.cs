using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Nimbus (Healer Elementalist): a storm cloud that floats over your cursor and rains healing on whoever stands
    /// under it. Left click calls lightning onto the spot beneath it. Enemies under the rain are WET (Tsunami.cs: slowed, and
    /// every lightning hit arcs to them) while there and NimbusWetLinger s after. The cloud is a companion Minion (so everyone
    /// sees it); its drift, rain and strikes are host logic here. Looks: Game/NimbusLook.cs (the Anvil: Docs/Mockups/nimbus-rework.html).
    ///   Eye of the Storm (Alt, Msg.WeaponDash WeaponAlt.Storm): the cloud spins up into a hurricane (NimbusStormRadius) for
    ///     NimbusStormTime, drifting after your cursor at NimbusStormDrift of its speed. Its arms drag enemies round and in
    ///     (NimbusStormSwirl/NimbusStormPull px/s, not heavies or bosses), hurt them NimbusStormDps and soak them; a bolt from
    ///     the arms hits an enemy in it every NimbusStormZap (NimbusStormBolt, arcing to wet enemies as lightning does); enemy
    ///     shots that enter it are torn apart; allies in the calm eye (NimbusEyeRadius) heal NimbusEyeHeal a second. No strikes
    ///     while it blows. It ends in a burst that shoves enemies out NimbusBurstShove px. Cooldown NimbusStormCd.
    ///     Everyone draws it from one FxKind.NimbusStorm (Pos2.X = the cloud's minion id, Radius = its radius, Dur = its life).
    public partial class Game
    {
        public const float NimbusLeash = 380f, NimbusRain = 70f, NimbusRainHeal = 8f, NimbusBlast = 40f, NimbusSpread = 45f, StrikeFxTime = 0.4f;
        public const float NimbusStrikeFxTime = 1.5f;  // the Nimbus's own strike effect: the bolt in its first StrikeFxTime, then the scorch fading
        public const float NimbusWetLinger = 1.5f;
        public const float NimbusStormTime = 4f, NimbusStormCd = 12f, NimbusStormRadius = 140f, NimbusStormDrift = 0.5f, NimbusStormDps = 20f;
        public const float NimbusStormSwirl = 120f, NimbusStormPull = 60f, NimbusStormZap = 0.33f, NimbusStormBolt = 10f;
        public const float NimbusEyeRadius = 26f, NimbusEyeHeal = 16f, NimbusBurstShove = 70f, NimbusStormSpinUp = 0.4f;
        public static readonly Rgba NimbusBoltColor = new Rgba(0.78f, 0.86f, 1f);

        float stormLocalCd;  // your own game: until the Alt fires again (drawn as a ring under your feet)
        public float StormLocalCd => stormLocalCd;
        readonly List<int> stormShots = new List<int>();

        /// How far from you the cloud can be (Piercing Rounds stretch it); the aim line ends here too.
        public static float NimbusReach(Player p) => NimbusLeash * PierceReach(p);
        public static float NimbusRainRadius(Player p) => NimbusRain * AreaMult(p) * p.Stats.RainSizeMult;
        public static float NimbusStormRadiusOf(Player p) => NimbusStormRadius * AreaMult(p);

        Minion NimbusOf(Player p)
        {
            foreach (var m in Minions.Values)
                if (m.Owner == p.Id && m.Kind == MinionKind.Nimbus && m.Companion)
                    return m;
            return null;
        }

        /// Where the cloud is heading: your cursor, held within its leash of you.
        V2 NimbusGoal(Player owner) => owner.Pos + (owner.AimAt - owner.Pos).LimitLength(NimbusReach(owner));

        /// How fast the cloud drifts: slower while it's a hurricane.
        float NimbusDrift(Player owner) => owner.NimbusStormT > 0f ? NimbusStormDrift : 1f;

        /// A click: lightning on the spot under the cloud (it falls to your cursor if the cloud isn't out yet). Each extra
        /// projectile adds a strike landing in a ring round the first. Homing Rounds widen the blast and the rain.
        void NimbusStrike(Player p, float dmg, int extra)
        {
            if (p.NimbusStormT > 0f)
                return;  // the hurricane's arms do the striking
            var cloud = NimbusOf(p);
            V2 centre = cloud != null ? cloud.Pos : ClampToArena(p.AimAt, 20f);
            float radius = NimbusBlast * AreaMult(p);
            dmg *= HomingCost(p);  // Homing Rounds widen it instead (AreaMult)
            bool anyHit = false;
            for (int k = 0; k <= extra; k++)
            {
                V2 at = k == 0 ? centre : ClampToArena(centre + V2.FromAngle(MathX.Tau * k / Math.Max(1, extra) + Rand(0f, 1f)) * (NimbusSpread * AreaMult(p)), 10f);
                BroadcastFx(FxKind.NimbusStrike, at, at, NimbusBoltColor, radius, NimbusStrikeFxTime, Sfx.SpellZap);
                foreach (var e in FoesInCircle(at, radius, by: p.Id))
                {
                    anyHit = true;
                    HurtEnemy(e, dmg, p.Id);
                    WetArcs(e.Pos, e, dmg, p.Id);  // Tsunami.cs
                    ModHit(e, p.Id, dmg, Defs.KnightSlash, new V2(1f, 0f));  // so weapon mods and legendaries apply, as for a swing
                }
            }
            p.Run.ShotsFired++;
            if (anyHit)
                p.Run.ShotsHit++;
        }

        /// Your own game: the Nimbus's Alt. True when it sent something (LocalWeaponDash stops there).
        bool LocalNimbusStorm(Player me, V2 aimAt, bool pressed, float dt)
        {
            stormLocalCd = Math.Max(0f, stormLocalCd - dt);
            if (!pressed || me.Weapon.Id != WeaponId.Nimbus || stormLocalCd > 0f)
                return false;
            stormLocalCd = NimbusStormCd;
            if (IsHost)
                HostWeaponDash(MyId, WeaponAlt.Storm, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(WeaponAlt.Storm);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }

        /// Host: Alt pressed. The cloud becomes a hurricane, if it's out and the cooldown is done.
        void HostNimbusStorm(Player p)
        {
            var cloud = NimbusOf(p);
            if (p.Weapon.Id != WeaponId.Nimbus || cloud == null || p.StunT > 0f || p.NimbusStormCd > 0f || p.NimbusStormT > 0f || State == MatchState.GameOver)
                return;
            p.NimbusStormCd = NimbusStormCd;
            p.NimbusStormT = NimbusStormTime;
            p.NimbusZapT = NimbusStormSpinUp;
            BroadcastFx(FxKind.NimbusStorm, cloud.Pos, new V2(cloud.Id, p.Id), NimbusBoltColor, NimbusStormRadiusOf(p), NimbusStormTime, Sfx.SpellZap);
        }

        /// Host, every tick: rain under each cloud heals every ally standing in it (you too) and soaks enemies; a hurricane does its work.
        void TickNimbus(float dt)
        {
            foreach (var p in Players.Values)
                p.NimbusStormCd = Math.Max(0f, p.NimbusStormCd - dt);
            foreach (var m in Minions.Values)
            {
                Player p;
                if (m.Kind != MinionKind.Nimbus || !m.Companion || !Players.TryGetValue(m.Owner, out p))
                    continue;
                if (p.Downed || p.Weapon.Id != WeaponId.Nimbus)
                {
                    p.NimbusStormT = 0f;
                    continue;
                }
                if (p.NimbusStormT > 0f)
                {
                    TickNimbusStorm(p, m, dt);
                    continue;
                }
                float r = NimbusRainRadius(p);  // (Cloudburst: wider, heals more)
                foreach (var q in Players.Values)
                    if (!q.Downed && q.Pos.WithinReach(m.Pos, r))
                        Heal(q, NimbusRainHeal * dt * p.Stats.RainHealMult2, p);
                Soak(m.Pos, r);
            }
        }

        /// Every enemy within r of at is wet now and for NimbusWetLinger after.
        void Soak(V2 at, float r)
        {
            foreach (var e in Enemies.Values)
                if (Targetable(e) && e.Pos.WithinReach(at, r + e.Radius))
                    e.WetT = Math.Max(e.WetT, NimbusWetLinger);
        }

        void TickNimbusStorm(Player p, Minion m, float dt)
        {
            float age = NimbusStormTime - p.NimbusStormT, R = NimbusStormRadiusOf(p) * Math.Min(1f, age / NimbusStormSpinUp + 0.25f);
            float dmg = NimbusStormDps * p.Stats.DmgMult * dt;
            Soak(m.Pos, R);
            foreach (var e in AllFoes(by: p.Id))
            {
                V2 rel = m.Pos - e.Pos;
                float d = rel.Length;
                if (d > R + e.Radius)
                    continue;
                if (!Heavy(e) && d > 1f)
                {
                    V2 inward = rel / d, round = new V2(-inward.Y, inward.X);
                    float pull = d > NimbusEyeRadius ? NimbusStormPull : 0f;
                    e.Pos = ClampToArena(e.Pos + (inward * pull + round * NimbusStormSwirl) * dt, e.Radius);
                }
                HurtEnemy(e, dmg, p.Id);
            }
            // Enemy shots that enter it are torn apart.
            stormShots.Clear();
            foreach (var b in Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind) && !Defs.Unblockable(b.Kind) && b.Pos.WithinReach(m.Pos, R))
                    stormShots.Add(b.Id);
            foreach (int id in stormShots)
                KillBullet(id);
            // Allies in the calm eye.
            float eye = NimbusEyeRadius * AreaMult(p);
            foreach (var q in Players.Values)
                if (!q.Downed && q.Pos.WithinReach(m.Pos, eye + q.Def.HitR))
                    Heal(q, NimbusEyeHeal * dt * p.Stats.RainHealMult2, p);
            // A bolt from the arms.
            p.NimbusZapT -= dt;
            if (p.NimbusZapT <= 0f)
            {
                p.NimbusZapT += NimbusStormZap;
                Enemy pick = null;
                int seen = 0;
                foreach (var e in Enemies.Values)
                    if (Targetable(e) && e.Pos.WithinReach(m.Pos, R + e.Radius) && Rand(0f, 1f) * ++seen < 1f)
                        pick = e;  // one at random
                if (pick != null)
                {
                    float a = (pick.Pos - m.Pos).Angle - 0.6f;
                    V2 from = m.Pos + V2.FromAngle(a) * Math.Max(NimbusEyeRadius, pick.Pos.DistanceTo(m.Pos) * 0.5f);
                    float bolt = NimbusStormBolt * p.Stats.DmgMult;
                    BroadcastFx(FxKind.Bolt, from, pick.Pos, NimbusBoltColor, 0f, 0.2f, Sfx.SpellZap);
                    HurtEnemy(pick, bolt, p.Id);
                    WetArcs(pick.Pos, pick, bolt, p.Id);  // Tsunami.cs
                }
            }
            p.NimbusStormT -= dt;
            if (p.NimbusStormT > 0f)
                return;
            // Blown out: a burst that shoves everything in it outward.
            p.NimbusStormT = 0f;
            BroadcastFx(FxKind.Ring, m.Pos, NimbusBoltColor, R + 40f, 0.45f, Sfx.None);
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || Heavy(e) || e.Pos.FartherThan(m.Pos, R + 40f))
                    continue;
                V2 rel = e.Pos - m.Pos;
                WindHit(e, rel);  // Wind.cs
                KnockEnemy(e, (rel.Length > 1f ? rel / rel.Length : RandDir()) * NimbusBurstShove);
            }
        }
    }
}
