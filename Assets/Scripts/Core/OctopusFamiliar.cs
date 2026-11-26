using System;
using System.Collections.Generic;

namespace Arena
{
    /// Octopus Familiar, the Healer Summoner's 4th choice (`AbilityId.OctopusFamiliar`, `AbilityDef.Fourth`; mockup
    /// Docs/Mockups/octopus-familiar.html). Pressing it starts a pick (`OctoPickSlot`, like Guardian's Oath): click a player in
    /// reach (OctoRange), you included; alone, it is cast on you at once. A spirit octopus (`MinionKind.Octopus`, LinkTo the
    /// guarded player) pops out of an ink splash beside them and floats round them for OctoLife s:
    ///   the arms:  8 of them; each enemy within OctoReach of the guarded player gets one (so up to 8 at once): it reaches out
    ///              (`FxKind.OctoSlap`, Pos the octopus, Pos2 the enemy, Dur OctoStrike) and lands at OctoLand s: OctoDamage,
    ///              a short shove (not Heavy enemies or bosses) and OctoHeal for the guarded player; then that arm rests OctoArmCd s;
    ///   the body:  has OctoHp health (`HasHealth`, Stun.cs): enemy shots stop on it and enemies touching it bite it; at 0, or
    ///              when its time is up, it bursts into ink (`FxKind.OctoInk`). One octopus per caster: a new cast moves it.
    /// Mastered: OctoHpMastered health and OctoDamageMastered slaps. Its Rare card Three Hearts adds Stats.OctoHpMult/OctoDmgMult.
    /// Replicated as an ordinary minion (position, timer, GuardFrac = its health, LinkTo); the arms are the OctoSlap effects
    /// (Game/OctopusFamiliarLook.cs).
    public partial class Game
    {
        public const float OctoCooldown = 40f, OctoLife = 60f, OctoRange = 500f, OctoPickR = 50f;
        public const float OctoHp = 260f, OctoHpMastered = 380f;
        public const int OctoArms = 8;
        public const float OctoReach = 130f;          // from the guarded player: enemies this close get slapped
        public const float OctoDamage = 16f, OctoDamageMastered = 22f, OctoShove = 16f, OctoHeal = 3f;
        public const float OctoStrike = 0.42f, OctoLand = 0.2f, OctoArmCd = 0.75f;  // an arm's reach out and back, when it lands, its rest
        public const float OctoOrbit = 46f, OctoSpin = 1.05f, OctoLift = 14f;      // how it floats round the player: radius, rad/s, how far up
        public static readonly Rgba OctoColor = new Rgba(0.71f, 0.47f, 0.95f);

        /// Host: each octopus's arms (by minion id): seconds into a strike (0: resting), the rest left, the enemy, landed yet.
        class OctoArmSet
        {
            public readonly float[] T = new float[OctoArms], Cd = new float[OctoArms];
            public readonly int[] Foe = new int[OctoArms];
            public readonly bool[] Landed = new bool[OctoArms];
            public readonly Dictionary<int, float> Slapped = new Dictionary<int, float>();  // enemy id: the octopus's age at its last slap
        }

        readonly Dictionary<int, OctoArmSet> octoArms = new Dictionary<int, OctoArmSet>();
        readonly List<Enemy> octoNear = new List<Enemy>();

        /// Your own game: the slot of an Octopus Familiar waiting for you to click a player (-1: not picking).
        public int OctoPickSlot = -1;
        bool octoFireWas, octoFireHold;  // Fire last tick; Fire held since the click that cast (no shot until it's let go)

        /// While picking: the player a click would give the octopus (the one under the cursor, you included, within reach of
        /// `me`), or null.
        public Player OctoPickTarget(Player me, V2 aimAt)
        {
            Player best = null;
            foreach (var q in Players.Values)
                if (q.Pos.WithinReach(me.Pos, OctoRange + q.Def.BodyR) && q.Pos.WithinReach(aimAt, OctoPickR + q.Def.BodyR)
                    && (best == null || q.Pos.DistanceSq(aimAt) < best.Pos.DistanceSq(aimAt)))
                    best = q;
            return best;
        }

        /// Your own game, each tick before the ability keys: while picking, Fire picks instead of shooting. Returns true on
        /// the tick a click lands on a player (the cast); cancels the pick when you go down or the slot changes.
        bool LocalOctoPick(Player me, V2 aimAt, bool fireDown, ref bool firing)
        {
            bool click = fireDown && !octoFireWas;
            octoFireWas = fireDown;
            if (octoFireHold && !fireDown)
                octoFireHold = false;
            if (OctoPickSlot >= 0 && (me.Downed || HoldControls || State == MatchState.GameOver || me.Slots[OctoPickSlot].Id != AbilityId.OctopusFamiliar))
                OctoPickSlot = -1;
            if (OctoPickSlot < 0)
            {
                if (octoFireHold)
                    firing = false;
                return false;
            }
            firing = false;
            if (!click || OctoPickTarget(me, aimAt) == null)
                return false;
            octoFireHold = true;
            return true;
        }

        /// Host: the cast. The octopus goes to the player nearest `target` within reach of the caster (the caster when
        /// nobody is), replacing the caster's last one.
        void CastOctopus(Player p, V2 target, bool cap)
        {
            Player linked = p;
            foreach (var q in Players.Values)
                if (q.Pos.WithinReach(p.Pos, OctoRange + q.Def.BodyR) && q.Pos.DistanceSq(target) < linked.Pos.DistanceSq(target))
                    linked = q;
            var old = new List<Minion>();
            foreach (var m0 in Minions.Values)
                if (m0.Owner == p.Id && m0.Kind == MinionKind.Octopus)
                    old.Add(m0);
            foreach (var m0 in old)
                OctoGone(m0);
            var m = SpawnMinion(MinionKind.Octopus, p, linked.Pos + new V2(0f, -OctoOrbit), OctoLife);
            m.LinkTo = linked.Id;
            m.Ang = -MathX.Pi / 2f;  // (starts over their head)
            m.Hp = m.MaxHp = (cap ? OctoHpMastered : OctoHp) * p.Stats.OctoHpMult;
            m.Mastered = cap;
            octoArms[m.Id] = new OctoArmSet();
            BroadcastFx(FxKind.OctoInk, m.Pos, OctoColor, 0f, 0.6f, Sfx.Summon);  // Radius 0: the splash it pops out of
        }

        /// Host: the octopus bursts into ink and goes (its time is up, it was broken, or a new one replaced it).
        void OctoGone(Minion m)
        {
            Minions.Remove(m.Id);
            OctoBurst(m);
        }

        /// Host: the ink burst and the arms forgotten, leaving the minion itself to the caller (the minion loop removes it
        /// after its pass).
        void OctoBurst(Minion m)
        {
            octoArms.Remove(m.Id);
            BroadcastFx(FxKind.OctoInk, m.Pos, OctoColor, 1f, 0.8f, Sfx.WwSplash);  // Radius 1: the burst
        }

        /// Host, each tick (Minions.cs). Returns false once the player it guards has gone.
        bool TickOctopus(Minion m, Player owner, float dt)
        {
            Player linked;
            if (!Players.TryGetValue(m.LinkTo, out linked))
                return false;
            m.Ang += OctoSpin * dt;
            V2 goal = linked.Pos + new V2((float)Math.Cos(m.Ang) * OctoOrbit, (float)Math.Sin(m.Ang) * OctoOrbit * 0.75f - OctoLift);
            m.Pos = ClampToArena(m.Pos + (goal - m.Pos) * Math.Min(1f, 6f * dt), m.Def.Radius);
            OctoArmSet arms;
            if (!octoArms.TryGetValue(m.Id, out arms))
                octoArms[m.Id] = arms = new OctoArmSet();
            float dmg = (m.Mastered ? OctoDamageMastered : OctoDamage) * owner.Stats.DmgMult * owner.Stats.OctoDmgMult * m.Power;
            for (int i = 0; i < OctoArms; i++)
            {
                if (arms.T[i] <= 0f)
                {
                    arms.Cd[i] -= dt;
                    continue;
                }
                arms.T[i] += dt;
                if (!arms.Landed[i] && arms.T[i] >= OctoLand)
                {
                    arms.Landed[i] = true;
                    Enemy e;
                    if (Enemies.TryGetValue(arms.Foe[i], out e) && Targetable(e) && e.Pos.WithinReach(linked.Pos, OctoReach + e.Radius + 40f))
                        OctoSlapLands(m, owner, linked, e, dmg);
                }
                if (arms.T[i] >= OctoStrike)
                {
                    arms.T[i] = 0f;
                    arms.Cd[i] = OctoArmCd;
                    arms.Foe[i] = 0;
                }
            }
            if (!Battle)
                return true;
            // Each enemy in reach not already being slapped gets a resting arm.
            octoNear.Clear();
            foreach (var e in AllFoes(by: owner.Id))
            {
                if (!Targetable(e) || !e.Pos.WithinReach(linked.Pos, OctoReach + e.Radius))
                    continue;
                bool taken = false;
                for (int i = 0; i < OctoArms && !taken; i++)
                    taken = arms.T[i] > 0f && arms.Foe[i] == e.Id;
                if (!taken)
                    octoNear.Add(e);
            }
            // Whoever went longest without a slap first (so more than 8 all get their turn), then the nearest.
            float age = m.Duration - m.T;
            if (arms.Slapped.Count > 64)
                arms.Slapped.Clear();
            octoNear.Sort((a, b) =>
            {
                float la, lb;
                la = arms.Slapped.TryGetValue(a.Id, out la) ? la : -1f;
                lb = arms.Slapped.TryGetValue(b.Id, out lb) ? lb : -1f;
                return la != lb ? la.CompareTo(lb) : a.Pos.DistanceSq(linked.Pos).CompareTo(b.Pos.DistanceSq(linked.Pos));
            });
            int next = 0;
            foreach (var e in octoNear)
            {
                while (next < OctoArms && (arms.T[next] > 0f || arms.Cd[next] > 0f))
                    next++;
                if (next >= OctoArms)
                    break;
                arms.T[next] = 1e-4f;
                arms.Foe[next] = e.Id;
                arms.Landed[next] = false;
                arms.Slapped[e.Id] = age;
                BroadcastFx(FxKind.OctoSlap, m.Pos, e.Pos, OctoColor, next, OctoStrike, Sfx.None);  // (Radius: the arm)
                m.Aim = (e.Pos - m.Pos).Angle;  // its eyes on what it reaches for
            }
            return true;
        }

        /// Host: an arm lands: the slap's damage and shove, and a little health for the guarded player.
        void OctoSlapLands(Minion m, Player owner, Player linked, Enemy e, float dmg)
        {
            HurtEnemy(e, dmg, owner.Id);
            if (!Heavy(e))
            {
                V2 away = e.Pos - m.Pos;
                KnockEnemy(e, (away.IsZero ? V2.FromAngle(RandAngle()) : away.Normalized()) * OctoShove);
            }
            if (!linked.Downed)
                Heal(linked, OctoHeal * m.Power, owner);
            BroadcastFx(FxKind.Spark, e.Pos, e.Pos, new Rgba(0.95f, 0.85f, 1f), e.Radius + 6f, 0.25f, Sfx.Hit);  // the smack
        }
    }
}
