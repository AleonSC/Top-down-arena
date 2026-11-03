using System;

namespace Arena
{
    /// The Nimbus (Healer Elementalist): a small storm cloud that floats over your cursor and rains healing on whoever stands
    /// under it. Left click calls lightning onto the spot beneath it. The cloud is a companion Minion (so everyone sees it); its
    /// drift, rain and strikes are host logic here. Looks: Game/NimbusLook.cs. Mockup: Docs/Mockups/totem-staff-rework.html.
    public partial class Game
    {
        public const float NimbusLeash = 380f, NimbusRain = 70f, NimbusRainHeal = 8f, NimbusBlast = 40f, NimbusSpread = 45f, StrikeFxTime = 0.4f;

        /// How far from you the cloud can be (Piercing Rounds stretch it); the aim line ends here too.
        public static float NimbusReach(Player p) => NimbusLeash * PierceReach(p);

        Minion NimbusOf(Player p)
        {
            foreach (var m in Minions.Values)
                if (m.Owner == p.Id && m.Kind == MinionKind.Nimbus && m.Companion)
                    return m;
            return null;
        }

        /// Where the cloud is heading: your cursor, held within its leash of you.
        V2 NimbusGoal(Player owner) => owner.Pos + (owner.AimAt - owner.Pos).LimitLength(NimbusReach(owner));

        /// A click: lightning on the spot under the cloud (it falls to your cursor if the cloud isn't out yet). Each extra
        /// projectile adds a strike landing in a ring round the first. Homing Rounds widen the blast and the rain.
        void NimbusStrike(Player p, float dmg, int extra)
        {
            var cloud = NimbusOf(p);
            V2 centre = cloud != null ? cloud.Pos : ClampToArena(p.AimAt, 20f);
            float radius = NimbusBlast * AreaMult(p);
            dmg *= HomingCost(p);  // Homing Rounds widen it instead (AreaMult)
            bool anyHit = false;
            for (int k = 0; k <= extra; k++)
            {
                V2 at = k == 0 ? centre : ClampToArena(centre + V2.FromAngle(MathX.Tau * k / Math.Max(1, extra) + Rand(0f, 1f)) * (NimbusSpread * AreaMult(p)), 10f);
                BroadcastFx(FxKind.Strike, at, at, new Rgba(0.78f, 0.86f, 1f), radius, StrikeFxTime, Sfx.SpellZap);
                foreach (var e in Enemies.Values)
                {
                    if (e.SpawnT > 0f || e.Hp <= 0f || e.Pos.DistanceTo(at) > radius + e.Radius)
                        continue;
                    anyHit = true;
                    HurtEnemy(e, dmg, p.Id);
                    OnBulletHit(new Bullet { Pos = e.Pos, Vel = new V2(1f, 0f), Owner = p.Id, Dmg = dmg, Kind = Defs.KnightSlash }, e);  // so weapon mods and legendaries apply, as for a swing
                }
            }
            p.Run.ShotsFired++;
            if (anyHit)
                p.Run.ShotsHit++;
        }

        /// Host, every tick: rain under each cloud heals every ally standing in it (you too).
        void TickNimbus(float dt)
        {
            foreach (var m in Minions.Values)
            {
                Player p;
                if (m.Kind != MinionKind.Nimbus || !m.Companion || !Players.TryGetValue(m.Owner, out p) || p.Downed || p.Weapon.Id != WeaponId.Nimbus)
                    continue;
                float r = NimbusRain * AreaMult(p);
                foreach (var q in Players.Values)
                    if (!q.Downed && q.Pos.DistanceTo(m.Pos) <= r)
                        Heal(q, NimbusRainHeal * dt, p);
            }
        }
    }
}
