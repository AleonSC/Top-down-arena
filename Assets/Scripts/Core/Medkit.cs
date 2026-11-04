using System;

namespace Arena
{
    /// Deployable Medkit (Healer Soldier): thrown at your cursor like a grenade. Where it lands it bursts into healing
    /// orbs that lie on the ground for 6 s; anyone who walks over one is healed (TickOrbs, Combat.cs), and a downed
    /// ally an orb reaches is picked up at 20% health.
    public partial class Game
    {
        const int MedkitOrbs = 6, MedkitOrbsMastered = 9;
        const float MedkitOrbHeal = 15f, MedkitOrbHealMastered = 20f;

        void ThrowMedkit(Player p, V2 target, bool mastered, float power)
        {
            V2 to = target - p.Pos;
            if (to.Length > MedkitReach)
                to = to.Normalized() * MedkitReach;
            var b = SpawnBullet(Defs.MedkitShot, p.Pos, to / MedkitFlight, 0f, MedkitFlight, 0f, p.Id);
            b.Burst = mastered ? MedkitOrbsMastered : MedkitOrbs;  // host-only: marks it to land (MoveBullets) and says how many orbs
            b.Dmg = (mastered ? MedkitOrbHealMastered : MedkitOrbHeal) * power;  // reused as the heal each orb carries
        }

        /// The medkit landed: the orbs scatter in a ring around it.
        void LandMedkit(Bullet b)
        {
            float offset = Rand(0f, MathX.Tau);
            for (int i = 0; i < b.Burst; i++)
            {
                V2 at = b.Pos + V2.FromAngle(offset + MathX.Tau * i / b.Burst) * Rand(34f, 68f);
                var o = new Orb { Id = nextOrbId++, Med = true, MedHeal = b.Dmg, Pos = ClampToArena(at, 12f), Life = MedkitLife };
                o.Disp = b.Pos;  // they pop out of the case
                Orbs[o.Id] = o;
            }
            BroadcastFx(FxKind.Ring, b.Pos, new Rgba(0.4f, 1f, 0.55f), 70f, 0.45f, Sfx.Ability);
        }
    }
}
