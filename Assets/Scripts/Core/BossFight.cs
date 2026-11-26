using System;

namespace Arena
{
    /// A boss's fight: the turn clocks every boss state shares (Enemy.Boss holds it). Each phase (Stage) has a list of
    /// turns that repeats; NextTurn moves to the next one. Each boss replicates its own fields (its Sync method).
    public abstract class BossFight
    {
        public float TurnT, TransT;  // seconds into the turn; the phase change's clock (> 0: changing phase)

        // Host only.
        public int Step = -1;        // the turn's place in its stage's list (-1: the phase hasn't started)
        public int Fired;            // the turn's attacks done so far
        public float TurnLen, ActT;  // how long the turn lasts; until its next attack
    }

    /// A boss's fight with its own phases and turns (enums; one list of turns and of their lengths per phase).
    public abstract class BossFight<TStage, TTurn> : BossFight where TStage : struct where TTurn : struct
    {
        public TStage Stage, TransTo;
        public TTurn Turn;

        /// The phase's next turn (round to its first after the last): sets Step, Turn and TurnLen and restarts TurnT.
        public void NextTurn(Game g, TTurn[][] turns, float[][] times)
        {
            int stage = Convert.ToInt32(Stage);
            var list = turns[stage];
            Step = (Step + 1) % list.Length;
            Turn = list[Step];
            TurnLen = g.BossTurnLen(Convert.ToByte(Turn), times[stage][Step]);  // (the tier's rest pace: BossRules.cs)
            TurnT = 0f;
        }
    }

    public partial class Game
    {
        /// An enemy's boss state, made the first time it's asked for.
        static T BossOf<T>(Enemy e) where T : BossFight, new() => e.Boss as T ?? (T)(e.Boss = new T());
    }
}
