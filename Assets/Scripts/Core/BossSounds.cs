using System;

namespace Arena
{
    /// Soft sound cues for bosses' abilities: a quiet swell when one switches attacks, a rising shimmer
    /// before something telegraphed (lasers, bombers, lightning), a low thump for slams, and a chime for
    /// summons and phase changes. The host notices each new attack and sends the cue to everyone.
    public partial class Game
    {
        /// Which attack (or stage) a boss is on; a change means it's starting something new.
        int BossAttackSig(Enemy e)
        {
            switch (e.Type)
            {
                case EnemyType.Boss: return WardenAttack(e);
                case EnemyType.HiveQueen: return (int)e.T2;
                case EnemyType.TwinA: return e.Phase * 10 + (int)(e.T / 5f) % 2;  // one Reaper speaks for both
                case EnemyType.TwinB: return e.Phase * 10;
                case EnemyType.Singularity: return e.Phase;
                case EnemyType.Lich: return e.Phase * 10 + (int)(e.T / 5f) % 2;
                case EnemyType.Devourer: return e.Phase;
                case EnemyType.Eclipse:
                    return e.Phase * 10 + (e.Phase == 0 ? (int)(e.T / 4f) % 2 : MathX.Fmod(e.T2, 10f) < 3.5f ? 5 : 0);
                default: return 0;  // Colossus, Duelist and Storm Herald cue from their events
            }
        }

        Sfx BossCue(Enemy e, int sig)
        {
            switch (e.Type)
            {
                case EnemyType.Boss: return sig == WardenCross ? Sfx.BossCharge : Sfx.BossShift;
                case EnemyType.HiveQueen: return sig == QueenSwarm ? Sfx.BossChime : sig == QueenBombers ? Sfx.BossCharge : sig == QueenBullets ? Sfx.BossShift : Sfx.None;
                case EnemyType.TwinA: case EnemyType.TwinB: return sig >= 10 ? Sfx.BossChime : Sfx.BossShift;  // enraged or soul-bound / new pattern
                case EnemyType.Singularity:
                    switch (sig)
                    {
                        case SingPull: case SingLaserWarn: return Sfx.BossCharge;
                        case SingCollapse: return Sfx.BossPulse;
                        case SingCage: return Sfx.BossChime;
                        case SingNormal: return Sfx.BossShift;
                        default: return Sfx.None;  // the lasers firing after their warning
                    }
                case EnemyType.Lich: return sig >= 10 && sig % 10 == 0 ? Sfx.BossChime : Sfx.BossShift;
                case EnemyType.Devourer: return sig == 1 ? Sfx.BossCharge : Sfx.None;
                case EnemyType.Eclipse: return sig % 10 == 5 ? Sfx.BossCharge : Sfx.BossShift;
                default: return Sfx.None;
            }
        }

        /// Host, each tick for each boss: play a cue when it starts a new attack.
        void BossCues(Enemy e)
        {
            int sig = BossAttackSig(e);
            if (e.CueSig == int.MinValue)
            {
                e.CueSig = sig;  // no sound for the attack it spawns into
                return;
            }
            if (sig == e.CueSig)
                return;
            e.CueSig = sig;
            var cue = BossCue(e, sig);
            if (cue != Sfx.None)
                BroadcastSound(cue);
        }

        /// A sound for everyone, with nothing to draw.
        void BroadcastSound(Sfx sound)
        {
            Sounds.Add(sound);
            var w = Proto.Begin(Msg.Sound);
            w.Write((byte)sound);
            session.Broadcast(Proto.End(w));
        }
    }
}
