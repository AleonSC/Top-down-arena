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
                case EnemyType.Boss: return e.Warden == null ? 0 : (int)e.Warden.Stage * 100 + (e.Warden.TransT > 0f ? 50 : (int)e.Warden.Turn);
                case EnemyType.HiveQueen: return e.Queen == null ? 0 : (int)e.Queen.Stage * 100 + (e.Queen.TransT > 0f ? 50 : (int)e.Queen.Turn);
                case EnemyType.TwinA:  // one Reaper speaks for both (and the Dual Reaper for itself)
                case EnemyType.DualReaper:
                    return Reapers == null ? e.Phase * 10 : (int)Reapers.Stage * 100 + (Reapers.Trans != 0 ? 50 : (int)Reapers.Turn) + e.Phase * 1000;
                case EnemyType.TwinB: return e.Phase * 1000;
                case EnemyType.Singularity: return (e.Sing == null ? 0 : (int)e.Sing.Stage * 100) + e.Phase;
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
                case EnemyType.Boss:  // a stage change chimes (StartWardenTransition); the light and the charge rise; a breather is quiet
                {
                    var turn = (WardenTurn)(sig % 100);
                    return sig % 100 == 50 || turn == WardenTurn.Rest ? Sfx.None : turn == WardenTurn.Light || turn == WardenTurn.Charge ? Sfx.BossCharge : Sfx.BossShift;
                }
                case EnemyType.HiveQueen:  // summons chime, bombers, the swarm and the dive rise, a breather is quiet (Queen.cs)
                {
                    var turn = (QueenTurn)(sig % 100);
                    if (sig % 100 == 50 || turn == QueenTurn.Rest)
                        return Sfx.None;
                    if (turn == QueenTurn.Drones || turn == QueenTurn.Eggs)
                        return Sfx.BossChime;
                    return turn == QueenTurn.Bombers || turn == QueenTurn.Barrage || turn == QueenTurn.Cloud || turn == QueenTurn.Dive ? Sfx.BossCharge : Sfx.BossShift;
                }
                case EnemyType.TwinA: case EnemyType.TwinB: case EnemyType.DualReaper:  // enraged or soul-bound chimes; a breather is quiet (Reapers.cs)
                {
                    int turn = sig % 100;
                    if (sig >= 1000)
                        return Sfx.BossChime;
                    if (turn == 50 || turn == (int)ReaperTurn.Rest)
                        return Sfx.None;
                    return turn == (int)ReaperTurn.Sweep || turn == (int)ReaperTurn.Lasers || turn == (int)ReaperTurn.Chase || turn == (int)ReaperTurn.Reap ? Sfx.BossCharge : Sfx.BossShift;
                }
                case EnemyType.Singularity:  // a stage change chimes (StartSingTransition); a breather is quiet (Singularity.cs)
                    switch ((byte)(sig % 100))
                    {
                        case SingPull: case SingLaserWarn: case SingWhiteHole: case SingLast: return Sfx.BossCharge;
                        case SingCollapse: case SingSlingshot: case SingHawking: return Sfx.BossPulse;
                        case SingCage: return Sfx.BossChime;
                        case SingNormal: case SingSplit: return Sfx.BossShift;
                        default: return Sfx.None;  // the lasers firing after their warning, breathers, the stage change
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
