using System;
using System.Collections.Generic;

namespace Arena
{
    /// Orbital Laser, the DPS Soldier's third ultimate (key 2 on the lobby's Ultimate row; Docs/Mockups/orbital-laser.html).
    /// You call a strike on your cursor, anywhere in the arena:
    ///   uplink   0 to OrbLockT: a red spotting line drops out of the sky onto the point
    ///   lock-on  to OrbHitT: a red circle of OrbRadius px with spinning brackets and a three-step countdown ring; the
    ///            enemies inside are marked; from OrbChargeT the screen dims and a thin pilot beam comes down
    ///   strike   OrbHitT to OrbBeamEndT: a white flash and a column of light. Every regular and heavy enemy it touches dies;
    ///            a boss (or a PvP stand-in) takes OrbBossShare of its max health, once; enemy shots inside are erased
    ///   scorch   OrbScorchT s of molten glass: OrbBurnDps a second (times your damage) and slowed OrbSlow
    /// Allies are never hurt. Mastered: OrbRadiusMastered px, and the beam ploughs OrbSweep px on, away from you, while it fires.
    /// Host logic; clients get one FxKind.OrbitalLaser at the cast (Pos = the point, Pos2 = where the beam ends up (Pos unless
    /// mastered), Radius = the caster's id, Color.A = the radius / 1000, Dur = OrbFxDur) and draw everything from its age
    /// (Game/OrbitalLaserLook.cs); its sound is Sfx.OrbitalLaser, the whole strike in one take (Game/OrbitalSoundBank.cs).
    public partial class Game
    {
        public const float OrbLockT = 0.3f, OrbChargeT = 1.5f, OrbHitT = 1.9f, OrbBeamT = 1.1f, OrbScorchT = 4f, OrbFadeT = 0.6f;
        public const float OrbRadius = 260f, OrbRadiusMastered = 340f, OrbSweep = 200f;
        public const float OrbBossShare = 0.2f, OrbBurnDps = 25f, OrbSlow = 0.3f, OrbBurnTick = 0.25f;
        public const float OrbBeamEndT = OrbHitT + OrbBeamT, OrbScorchEndT = OrbBeamEndT + OrbScorchT;
        public const float OrbFxDur = OrbScorchEndT + OrbFadeT;

        internal static readonly AbilityDef[] OrbitalUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.OrbitalLaser, Kit = Kit.Soldier, Role = Role.Dps, Choice = AbilityDef.Ult, UltKey = 2,
                Name = "Orbital Laser", Cooldown = 90f,
                Desc = "Call a laser down from orbit on your cursor: after a 1.9 s lock-on it kills every enemy in a huge circle (bosses lose 20%) and leaves the ground burning.",
                Capstone = "The circle is 340 px wide instead of 260, and the beam ploughs 200 px on while it fires.",
                Bot = b => b.NearEnemies >= 4 || (b.HaveTarget && b.BotDanger > 2.5f),
            },
        };

        /// Where the beam is (its middle) t s after the cast: on the point, sliding to `end` while it fires.
        public static V2 OrbCentre(V2 pos, V2 end, float t)
        {
            float k = Math.Max(0f, Math.Min(1f, (t - OrbHitT) / OrbBeamT));
            return pos + (end - pos) * k;
        }

        class OrbStrike
        {
            public int Owner;
            public V2 Pos, End;
            public float T, R, Burn, BurnGap;
            public readonly HashSet<int> Chunked = new HashSet<int>();  // bosses (and stand-ins) that took their share
        }

        readonly List<OrbStrike> orbStrikes = new List<OrbStrike>();

        void CastOrbital(Player p, V2 target, bool cap)
        {
            V2 at = ClampToArena(target, 20f);
            V2 dir = at - p.Pos;
            dir = dir.Length < 1f ? V2.FromAngle(p.Aim) : dir.Normalized();
            float r = cap ? OrbRadiusMastered : OrbRadius;
            V2 end = cap ? ClampToArena(at + dir * OrbSweep, 20f) : at;
            orbStrikes.Add(new OrbStrike { Owner = p.Id, Pos = at, End = end, R = r, Burn = OrbBurnDps * p.Stats.DmgMult });
            BroadcastFx(FxKind.OrbitalLaser, at, end, new Rgba(1f, 0.8f, 0.35f, r / 1000f), p.Id, OrbFxDur, Sfx.OrbitalLaser);
        }

        /// Host, every tick: the beam's kills, then the scorch's burn.
        void TickOrbitals(float dt)
        {
            for (int i = orbStrikes.Count - 1; i >= 0; i--)
            {
                var s = orbStrikes[i];
                s.T += dt;
                if (s.T > OrbScorchEndT)
                {
                    orbStrikes.RemoveAt(i);
                    continue;
                }
                if (s.T >= OrbHitT && s.T < OrbBeamEndT)
                {
                    V2 c = OrbCentre(s.Pos, s.End, s.T);
                    foreach (var e in FoesInCircle(c, s.R, by: s.Owner))
                    {
                        if (e.Def.IsBoss || e.Stand != null)
                        {
                            if (s.Chunked.Add(e.Id))
                                HurtEnemy(e, e.MaxHp * OrbBossShare, s.Owner);
                        }
                        else
                            HurtEnemy(e, e.Hp + e.MaxHp, s.Owner);  // (more than it has left: it dies through most armour, and is hit again next tick if not)
                        castHits++;
                    }
                    EraseEnemyBullets(c, s.R);
                }
                else if (s.T >= OrbBeamEndT && (s.BurnGap -= dt) <= 0f)
                {
                    s.BurnGap += OrbBurnTick;
                    foreach (var e in FoesInCircle(s.End, s.R * 0.95f, by: s.Owner))
                    {
                        Slow(e, OrbBurnTick + 0.15f, 1f - OrbSlow);
                        HurtEnemy(e, s.Burn * OrbBurnTick, s.Owner);
                    }
                }
            }
        }
    }
}
