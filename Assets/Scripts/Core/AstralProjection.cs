using System;
using System.Collections.Generic;

namespace Arena
{
    /// Astral Projection: the Healer Ninja's second ultimate (lobby key 1 of the Ultimate row; Blossom Storm stays the default).
    /// From its mockup (claude.ai artifact "Ninja Astral Projection"):
    ///   - Your body stays where it is for ApTime s: it can't be hurt and enemies ignore it (PickTarget skips it, and enemies
    ///     already on it look for someone else). It can't move or dodge.
    ///   - Your spirit is tethered to a teammate (the host; the nearest one at the cast). It leans toward your aim, at most
    ///     ApLean px from them. Fire shoots astral bolts from the spirit at the aim (ApDamage each, ApRate a second); the
    ///     spirit can't be hurt. The parry key (right click) sends the spirit to the next teammate (ApSwapCd s apart).
    ///   - The host heals ApHeal HP a second and moves ApSpeedMult faster while the tether holds.
    ///   - Alone (or with every teammate down), the spirit tethers to your own body: then the body is the host (it heals, moves
    ///     faster and still can't be hurt).
    /// Ends on time, when you go down, the wave ends or the slot changes. Mastered: ApTimeMastered s and the heal is
    /// ApMasterHeal times stronger; the Rare card Astral Bond adds time and bolt damage. Bolt damage scales by your damage and fire rate.
    /// Replicated: Player.ApT (time left, 0 = off), ApHost (the host's id), ApPos (the spirit) on the caster; ApLinkT on the host
    /// (it is held up while a tether is on them: Player.Speed reads it on their own game). Drawn by Game/AstralProjectionLook.cs.
    public partial class Game
    {
        public const float ApTime = 8f, ApTimeMastered = 10f, ApMasterHeal = 1.4f;
        public const float ApTether = 150f, ApLean = 110f, ApFollow = 12f;  // the tether's ring, how far the spirit leans, how fast it follows
        public const float ApHeal = 7f, ApSpeedMult = 1.35f, ApLinkHold = 0.25f;
        public const float ApDamage = 9f, ApRate = 6f, ApBoltSpeed = 620f, ApBoltLife = 0.75f;
        public const float ApSwapCd = 0.4f;
        public const float ApBondTime = 2f, ApBondDmg = 0.25f;  // the Rare card Astral Bond, a stack

        public static readonly Rgba ApSpirit = new Rgba(0.73f, 0.96f, 1f), ApViolet = new Rgba(0.71f, 0.55f, 1f), ApWard = Rgba.Gold, ApMend = new Rgba(0.44f, 0.89f, 0.6f);

        /// Astral Projection's entry: key 1 of the Healer Ninja's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] AstralUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.AstralProjection, Kit = Kit.Ninja, Role = Role.Healer, Choice = AbilityDef.Ult, UltKey = 1,
                Name = "Astral Projection", Cooldown = 70f,
                Desc = "Leave your body safe and ignored for 8 s; your spirit rides a teammate, shoots astral bolts, and heals them and speeds them up. Right click: switch teammate.",
                Capstone = "Lasts 10 s and heals 40% more.",
                Bot = b => b.AllyHurt || b.Danger,
            },
        };

        /// The spirit is out.
        public static bool ApOn(Player p) => p.ApT > 0f && !p.Downed;
        /// The body is left behind (the spirit rides someone else): it can't move.
        public static bool ApAway(Player p) => ApOn(p) && p.ApHost != p.Id;
        /// Your speed share (Player.Speed): rooted while your spirit is away, faster while a tether is on you.
        public static float ApSpeed(Player p) => ApAway(p) ? 0f : p.ApLinkT > 0f ? ApSpeedMult : 1f;

        void CastAstral(Player p, bool mastered)
        {
            p.ApT = (mastered ? ApTimeMastered : ApTime) + p.Stats.ApBonusTime;
            p.ApMastered = mastered;
            p.ApUp = true;
            p.ApFireCd = 0f;
            p.ApSwapCd = ApSwapCd;
            p.ApParryWas = true;  // (a parry held at the cast doesn't switch at once)
            p.ApHost = ApNearest(p);
            p.ApPos = p.Pos;
            p.UltT = p.ApT;
            ApLoseThem(p);
            BroadcastFx(FxKind.Ring, p.Pos, p.Pos, ApSpirit, 70f, 0.6f, Sfx.NinjaVanish);
        }

        /// Enemies after the caster's body look for someone else.
        void ApLoseThem(Player p)
        {
            foreach (var e in Enemies.Values)
                if (e.TargetId == p.Id)
                    e.RetargetT = 0f;
        }

        /// A teammate the spirit can ride: up and in the run.
        bool ApRideable(Player p, Player h) => h != null && h.Id != p.Id && !h.Downed && !Jailed(h);

        /// The nearest teammate to ride (none: your own body).
        int ApNearest(Player p)
        {
            Player best = null;
            foreach (var h in Players.Values)
                if (ApRideable(p, h) && (best == null || h.Pos.DistanceSq(p.Pos) < best.Pos.DistanceSq(p.Pos)))
                    best = h;
            return best != null ? best.Id : p.Id;
        }

        readonly List<int> apOrder = new List<int>();

        /// The next teammate after the current host, by id, round the team (none: your own body).
        int ApNext(Player p)
        {
            apOrder.Clear();
            foreach (var h in Players.Values)
                if (ApRideable(p, h))
                    apOrder.Add(h.Id);
            if (apOrder.Count == 0)
                return p.Id;
            apOrder.Sort();
            foreach (int id in apOrder)
                if (id > p.ApHost)
                    return id;
            return apOrder[0];
        }

        /// The host now (the caster's own body when it rides nobody).
        public Player ApHostOf(Player p)
        {
            Player h;
            return Players.TryGetValue(p.ApHost, out h) ? h : p;
        }

        /// The spirit snaps back to the body.
        void EndAstral(Player p)
        {
            if (!p.ApUp)
                return;
            p.ApUp = false;
            p.ApT = 0f;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.AstralProjection)
                p.UltT = 0f;
            BroadcastFx(FxKind.Ring, p.ApPos, p.ApPos, ApSpirit, 50f, 0.45f, Sfx.None);
            BroadcastFx(FxKind.Ring, p.Pos, p.Pos, ApSpirit, 60f, 0.5f, Sfx.NinjaBlink);
        }

        /// Host, every tick: the clock, the host (switching and falling), the spirit's lean, the heal and the bolts' cooldown.
        void TickAstral(float dt)
        {
            foreach (var h in Players.Values)
                h.ApLinkT = Math.Max(0f, h.ApLinkT - dt);
            foreach (var p in Players.Values)
            {
                if (!p.ApUp)
                    continue;
                p.ApT = Math.Max(0f, p.ApT - dt);
                if (p.ApT <= 0f || p.Downed || !Battle || p.Slots[Upgrades.UltSlot].Id != AbilityId.AstralProjection)
                {
                    EndAstral(p);
                    continue;
                }
                p.ApFireCd -= dt;
                p.ApSwapCd = Math.Max(0f, p.ApSwapCd - dt);
                bool press = p.Parrying && !p.ApParryWas;
                p.ApParryWas = p.Parrying;
                Player host;
                if (p.ApHost != p.Id && (!Players.TryGetValue(p.ApHost, out host) || !ApRideable(p, host)))
                    p.ApHost = ApNext(p);  // the host went down or left: on to the next
                else if (press && p.ApSwapCd <= 0f)
                {
                    int next = ApNext(p);
                    if (next != p.ApHost)
                    {
                        p.ApHost = next;
                        p.ApSwapCd = ApSwapCd;
                        p.ApSwaps++;
                        BroadcastFx(FxKind.Ring, p.ApPos, p.ApPos, ApViolet, 30f, 0.35f, Sfx.NinjaBlink);
                    }
                }
                else if (p.ApHost == p.Id && press && p.ApSwapCd <= 0f)
                    p.ApHost = ApNext(p);  // riding your own body: a teammate may be up again
                host = ApHostOf(p);
                if (p.ApHost != p.Id)
                    ApLoseThem(p);  // (an enemy that picked the body while it was host)
                V2 lean = (p.AimAt - host.Pos).LimitLength(ApLean);
                V2 goal = host.Pos + lean;
                p.ApPos = p.ApPos + (goal - p.ApPos) * Math.Min(1f, ApFollow * dt);
                host.ApLinkT = ApLinkHold;
                float heal = ApHeal * (p.ApMastered ? ApMasterHeal : 1f) * dt;
                p.ApHealed += Heal(host, heal, p);
            }
        }

        /// TickPlayers' fire branch while the spirit is out: Fire shoots astral bolts from the spirit (the weapon is put away).
        void AstralTrigger(Player p)
        {
            if (!p.Firing || p.ApFireCd > 0f || State == MatchState.GameOver)
                return;
            p.ApFireCd = 1f / (ApRate * FireRate(p));
            V2 d = p.AimAt - p.ApPos;
            d = d.Length < 1f ? V2.FromAngle(p.Aim) : d.Normalized();
            SpawnBullet(Defs.AstralBolt, p.ApPos + d * 10f, d * ApBoltSpeed, 0f, ApBoltLife, ApDamage * p.Stats.DmgMult * p.Stats.ApDmgMult, p.Id);
            p.ApBolts++;
        }

        // --- Network: the player's Astral Projection fields (SyncPlayer) ---

        void SyncAstralProjection(NetSync s, Player p)
        {
            s.Float(ref p.ApT);  // Astral Projection
            if (p.ApT > 0f)
            {
                s.Int(ref p.ApHost);
                s.Vec(ref p.ApPos);
            }
            s.Float(ref p.ApLinkT);
        }
    }
}
