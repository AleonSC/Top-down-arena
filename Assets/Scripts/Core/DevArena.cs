using System;

namespace Arena
{
    /// The F1 dock's Dev Arena: a sandbox match (no waves arrive, the wave never clears, everyone in god mode) where you
    /// spawn enemies by hand and can take one over ("play as") to try its attacks yourself: WASD moves it, the mouse is
    /// what it goes for (its own AI aims, lunges and charges at the cursor), Fire attacks on its real cadence, Parry
    /// attacks at once (skipping the cooldown), the role ability lets go. Host only and local: the possessed enemy syncs
    /// like any enemy, so nothing new goes over the network. Hooked into TickEnemies (EnemyAI.cs) and the Fighting
    /// spawner (Waves.cs); drawn by Game/DevArenaUi.cs.
    public partial class Game
    {
        /// Host: the sandbox is on (set after the match starts; HostStart's reset turns it off).
        public bool DevArena;

        /// Host: the enemy being played (its id), or -1.
        public int PossessId = -1;

        V2 possessMove, possessAim;
        bool possessAttack, possessNow;

        /// The enemy being played, or null (a dead one lets go by itself).
        public Enemy Possessed
        {
            get
            {
                Enemy e;
                if (PossessId < 0)
                    return null;
                if (!Enemies.TryGetValue(PossessId, out e) || e.Hp <= 0f)
                {
                    PossessId = -1;
                    return null;
                }
                return e;
            }
        }

        public void DevSetArena(bool on)
        {
            if (!IsHost || State == MatchState.Idle || DevArena == on)
                return;
            DevArena = on;
            DevUsed("dev arena " + (on ? "on" : "off"));
            if (!on)
            {
                PossessId = -1;
                return;
            }
            DevFreeze = false;
            DevClearArena();
            foreach (var p in Players.Values)
                DevSetGod(p.Id, true);
        }

        /// Dev Arena: every enemy, boss, hazard and enemy shot gone; downed players back up.
        public void DevClearArena()
        {
            if (!IsHost || State == MatchState.Idle)
                return;
            PossessId = -1;
            spawnQueue.Clear();
            squadQueue.Clear();
            Enemies.Clear();
            ResetBossState();
            Zones.Clear();
            Hazards.Clear();
            Cages.Clear();
            ClearEnemyBullets();
            session.Broadcast(Proto.End(Proto.Begin(Msg.ClearEnemyBullets)));
            foreach (var p in Players.Values)
                if (p.Downed)
                    RevivePlayer(p, 1f);
        }

        /// Dev Arena: spawn one of this enemy in front of player `id` and play as it. True when it worked.
        public bool DevPossess(int id, EnemyType type)
        {
            if (!IsHost || !DevArena || State != MatchState.Fighting)
                return false;
            var had = new System.Collections.Generic.HashSet<int>(Enemies.Keys);
            if (DevSpawnEnemy(id, type, 1, false) == 0)
                return false;
            Enemy pick = null;
            foreach (var e in Enemies.Values)
                if (!had.Contains(e.Id) && e.Type == type)
                {
                    pick = e;
                    break;
                }
            if (pick == null)
                return false;
            PossessId = pick.Id;
            pick.SpawnT = Math.Min(pick.SpawnT, 0.3f);
            possessAim = pick.Pos;
            possessMove = V2.Zero;
            possessAttack = possessNow = false;
            DevUsed("playing as " + pick.Def.Name);
            return true;
        }

        public void DevRelease()
        {
            if (PossessId >= 0)
                DevUsed("let go of the enemy");
            PossessId = -1;
        }

        /// The host's controls for the enemy being played, once a fixed step (ArenaApp.FixedUpdate). `now` is a press.
        public void SetPossessInput(V2 move, V2 aimAt, bool attack, bool now)
        {
            possessMove = move.LimitLength(1f);
            possessAim = aimAt;
            possessAttack = attack;
            possessNow |= now;
        }

        /// TickEnemies: is this the enemy being played?
        bool IsPossessed(Enemy e) => e.Id == PossessId && DevArena;

        /// TickEnemies, for the enemy being played: its AI attacks this tick (held Fire, or an "attack now" press).
        bool PossessAttacking => possessAttack || possessNow;

        /// Moves the enemy being played: your keys when you steer; otherwise its own movement (a lunge, a charge,
        /// keeping its range from the cursor) while it attacks; otherwise it stands still.
        void PossessMoveEnemy(Enemy e, V2 tpos, float dt)
        {
            if (possessMove.Length > 0.05f)
            {
                float speed = Math.Max(60f, e.Speed * BruteSpeedMult(e));
                if (e.SlowT > 0f && !Heavy(e))
                    speed *= e.SlowMult;
                e.Pos = ClampToArena(e.Pos + possessMove * speed * dt, e.Radius);
                if ((Props.Count > 0 || LayoutOn) && !e.Def.IsBoss)
                    e.Pos = PushOutOfProps(e.Pos, e.Radius * 0.8f);
            }
            else if (PossessAttacking)
                MoveEnemy(e, tpos, dt);
        }

        /// TickEnemies, before the enemy being played counts down to its next attack: an "attack now" press skips the
        /// cooldown (and a melee enemy's rest between lunges).
        void PossessFireNow(Enemy e)
        {
            if (!possessNow)
                return;
            possessNow = false;
            e.FireT = Math.Min(e.FireT, 0f);
            if (e.Phase == 0)
                e.PhaseT = Math.Min(e.PhaseT, 0f);
        }
    }
}
