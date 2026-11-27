using System;
using System.Collections.Generic;

namespace Arena
{
    /// Conduit Web: the Healer Elementalist's fourth ultimate (lobby key 3 of the Ultimate row; Sanctum stays the default,
    /// Static Bubble is key 1, Eye of Calm key 2). From its mockup (claude.ai artifact "Conduit Web"), drawn like Static Shock.
    ///   - A field of static bursts out round you (to CwRadius px over CwGrow s) and moves with you for CwTime s.
    ///   - Every enemy inside it, and any that walk in, is wired into one circuit (CwCircuit): your hand feeds CwArms conduits
    ///     and each enemy joins the closest point already in the circuit, so it branches out in CwArms arms.
    ///   - A wired enemy is held stunned while it is wired (not bosses), takes CwFirstDmg when it is first wired, and is shocked
    ///     every CwPulse s for CwDmg, growing to twice that over CwRamp s wired. When the web ends, the enemies in it stay
    ///     stunned CwStunAfter s more.
    ///   - Every CwPulse s each teammate in the field (not you) heals CwHeal.
    /// Ends when time runs out, you go down, the wave ends or the slot changes. Mastered: the field reaches CwRadiusMastered px
    /// and lasts CwTimeMastered s. Damage scales by your damage.
    /// Replicated: Player.CwT (time left, 0 = off), CwLen (its length) and CwR (the field's full reach); every screen works the
    /// circuit out itself from where the enemies are (CwCircuit), as Static Shock does. Drawn by Game/ConduitWebLook.cs.
    public partial class Game
    {
        public const float CwTime = 4f, CwTimeMastered = 6f, CwRadius = 330f, CwRadiusMastered = 420f, CwGrow = 0.35f;
        public const float CwPulse = 0.5f, CwDmg = 8f, CwFirstDmg = 12f, CwRamp = 3f, CwHeal = 5f, CwStunAfter = 1f;
        public const float CwHold = 0.15f, CwForget = 0.3f;  // the stun refreshed while wired; how long off the web before its shock starts over
        public const int CwArms = 3;

        /// Conduit Web's entry: key 3 of the Healer Elementalist's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] ConduitUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.ConduitWeb, Kit = Kit.Elementalist, Role = Role.Healer, Choice = AbilityDef.Ult, UltKey = 3,
                Name = "Conduit Web", Cooldown = 80f,
                Desc = "A 330 px field of static round you for 4 s: three conduits from your hand chain through every enemy in it, holding them stunned and shocking them harder the longer they're wired. Teammates in it heal 5 every half second.",
                Capstone = "The field reaches 420 px and lasts 6 s.",
                Bot = b => b.Danger || b.Crowd >= JuggBotCrowd,
            },
        };

        /// The web is up.
        public static bool CwOn(Player p) => p.CwT > 0f && !p.Downed;
        /// The field's reach now: it bursts out over CwGrow s (eased) to its full CwR.
        public static float CwReach(Player p)
        {
            float u = Math.Min(1f, Math.Max(0f, (p.CwLen - p.CwT) / CwGrow)), k = 1f - u;
            return p.CwR * (1f - k * k * k);
        }
        /// A wired enemy's shock for how long it has been wired: CwDmg, growing to twice that over CwRamp s.
        public static float CwShock(float wired) => CwDmg * (1f + Math.Min(1f, wired / CwRamp));

        /// The circuit: every enemy within `reach` of `centre` (ok ones), each wired (from[i]: -1 the hand, else the index of
        /// the enemy it wires to) to the closest point already in it; the hand feeds at most `arms` of them. pos: where each enemy
        /// is (the host's Pos, a screen's Disp). Shared by the host's shocks and every screen's drawing.
        public static void CwCircuit(V2 hand, V2 centre, float reach, IEnumerable<Enemy> enemies, Func<Enemy, V2> pos, Func<Enemy, bool> ok,
            int arms, List<Enemy> into, List<int> from)
        {
            into.Clear();
            from.Clear();
            var inside = cwScratch;
            inside.Clear();
            foreach (var e in enemies)
                if (ok(e) && pos(e).CloserThan(centre, reach + e.Radius))
                    inside.Add(e);
            int n = inside.Count;
            if (n == 0)
                return;
            // Prim's tree, O(n^2): each enemy's closest point in the circuit so far (the hand while it still has arms free).
            if (cwBest.Length < n)
            {
                cwBest = new float[n * 2];
                cwFrom = new int[n * 2];
                cwDone = new bool[n * 2];
            }
            for (int i = 0; i < n; i++)
            {
                cwBest[i] = pos(inside[i]).DistanceSq(hand);
                cwFrom[i] = -1;
                cwDone[i] = false;
            }
            int roots = 0;
            for (int added = 0; added < n; added++)
            {
                int bi = -1;
                for (int i = 0; i < n; i++)
                    if (!cwDone[i] && (bi < 0 || cwBest[i] < cwBest[bi]))
                        bi = i;
                cwDone[bi] = true;
                if (cwFrom[bi] < 0)
                    roots++;
                into.Add(inside[bi]);
                from.Add(cwFrom[bi] < 0 ? -1 : cwFrom[bi]);
                int at = into.Count - 1;
                V2 c = pos(inside[bi]);
                bool full = roots >= arms;
                for (int i = 0; i < n; i++)
                {
                    if (cwDone[i])
                        continue;
                    V2 q = pos(inside[i]);
                    if (full && cwFrom[i] < 0)
                    {
                        // the hand has no arms left: this one's best is now the closest enemy already in
                        cwBest[i] = float.MaxValue;
                        for (int j = 0; j < into.Count; j++)
                        {
                            float dj = q.DistanceSq(pos(into[j]));
                            if (dj < cwBest[i]) { cwBest[i] = dj; cwFrom[i] = j; }
                        }
                        continue;
                    }
                    float d = q.DistanceSq(c);
                    if (d < cwBest[i]) { cwBest[i] = d; cwFrom[i] = at; }
                }
            }
        }
        static readonly List<Enemy> cwScratch = new List<Enemy>();
        static float[] cwBest = new float[16];
        static int[] cwFrom = new int[16];
        static bool[] cwDone = new bool[16];

        readonly List<Enemy> cwIn = new List<Enemy>();
        readonly List<int> cwLinks = new List<int>();
        readonly Dictionary<int, float> cwWiredT = new Dictionary<int, float>(), cwSeenAt = new Dictionary<int, float>();  // host: each enemy's time wired, and when it last was
        readonly HashSet<long> cwFirst = new HashSet<long>();  // host: (caster, enemy) pairs that took the first-wire hit this cast
        readonly List<int> cwGone = new List<int>();
        float cwClock;

        void CastConduitWeb(Player p, bool mastered)
        {
            p.CwLen = p.CwT = mastered ? CwTimeMastered : CwTime;
            p.CwR = mastered ? CwRadiusMastered : CwRadius;
            p.CwPulseT = CwPulse;
            p.CwMastered = mastered;
            p.CwUp = true;
            p.UltT = p.CwT;
            cwFirst.RemoveWhere(k => (int)(k >> 32) == p.Id);
            BroadcastFx(FxKind.Ring, p.Pos, StaticBlue, p.CwR, CwGrow, Sfx.SpellChainArc);
        }

        /// It ends (time, down, the wave over, the slot changed): the enemies still wired stay stunned CwStunAfter s more.
        void EndConduitWeb(Player p)
        {
            if (!p.CwUp)
                return;
            if (!p.Downed && Battle)
            {
                CwCircuit(StaticHand(p, p.Pos), p.Pos, CwReach(p), Enemies.Values, e => e.Pos, Targetable, CwArms, cwIn, cwLinks);
                foreach (var e in cwIn)
                    if (!e.Def.IsBoss)
                        Stun(e, CwStunAfter);
            }
            p.CwUp = false;
            p.CwT = 0f;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.ConduitWeb)
                p.UltT = 0f;
            BroadcastFx(FxKind.Ring, p.Pos, StaticBlue, p.CwR, 0.3f, Sfx.None);
        }

        /// Host, every tick: the clock, the circuit, the stun, the shocks and the heal.
        void TickConduitWeb(float dt)
        {
            cwClock += dt;
            cwGone.Clear();
            foreach (var kv in cwSeenAt)
                if (cwClock - kv.Value > CwForget || !Enemies.ContainsKey(kv.Key))
                    cwGone.Add(kv.Key);
            foreach (int id in cwGone)
            {
                cwSeenAt.Remove(id);
                cwWiredT.Remove(id);
            }
            foreach (var p in Players.Values)
            {
                if (!p.CwUp)
                    continue;
                p.CwT = Math.Max(0f, p.CwT - dt);
                if (p.Downed || !Battle || p.Slots[Upgrades.UltSlot].Id != AbilityId.ConduitWeb || p.CwT <= 0f)
                {
                    EndConduitWeb(p);
                    continue;
                }
                float reach = CwReach(p);
                CwCircuit(StaticHand(p, p.Pos), p.Pos, reach, Enemies.Values, e => e.Pos, Targetable, CwArms, cwIn, cwLinks);
                p.CwPulseT -= dt;
                bool pulse = p.CwPulseT <= 0f;
                if (pulse)
                    p.CwPulseT += CwPulse;
                float dmg = p.Stats.DmgMult;
                p.CwMostWired = Math.Max(p.CwMostWired, cwIn.Count);
                foreach (var e in cwIn)
                {
                    float w;
                    cwWiredT.TryGetValue(e.Id, out w);
                    w += dt;
                    cwWiredT[e.Id] = w;
                    cwSeenAt[e.Id] = cwClock;
                    if (!e.Def.IsBoss)
                        Stun(e, CwHold);
                    if (cwFirst.Add(((long)p.Id << 32) | (uint)e.Id))
                    {
                        HurtEnemy(e, CwFirstDmg * dmg, p.Id);
                        Charge(e, p.Id);  // Charged.cs
                        p.CwWired++;
                    }
                    if (pulse)
                        HurtEnemy(e, CwShock(w) * dmg, p.Id);
                }
                if (!pulse)
                    continue;
                p.CwPulses++;
                if (cwIn.Count > 0)
                    BroadcastSound(Sfx.SpellZap);
                foreach (var q in Players.Values)
                    if (q != p && !q.Downed && q.Pos.CloserThan(p.Pos, reach + q.Def.BodyR))
                        p.CwHealed += Heal(q, CwHeal, p);
            }
        }

        // --- Network: the player's Conduit Web fields (SyncPlayer) ---

        void SyncConduitWeb(NetSync s, Player p)
        {
            s.Float(ref p.CwT);  // Conduit Web
            if (p.CwT > 0f)
            {
                s.Float(ref p.CwLen);
                s.Float(ref p.CwR);
            }
        }
    }
}
