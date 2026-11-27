using System;

namespace Arena
{
    /// Rock Gloves: the DPS Elementalist's fourth ultimate (lobby key 3 of the Ultimate row; Bullet Rain stays the default).
    /// From the mockup (claude.ai artifact "Rock Gloves"):
    ///   - Gather (GlvGather s): the ground cracks, shards of earth rise round you and fly in to build two giant floating fists.
    ///     You walk at GlvGatherMove and can't attack.
    ///   - Gloves (GlvTime s): your weapon is put away and Fire punches. Hold it for rapid jabs at the cursor, one every
    ///     GlvJabEvery s, the fists taking turns: GlvJabDamage to every enemy along the fist's path (GlvJabFrom..GlvJabReach px
    ///     ahead, GlvJabHalf px each side), a small shove.
    ///   - Haymaker: every GlvHayEvery-th punch both fists drive in together, GlvHayAhead px ahead: GlvHayDamage to everything
    ///     within GlvHayRadius, a GlvHayStun s stun and a shove. The next jab waits GlvHayRecover s.
    ///   - Crumble: when the time runs out the fists burst into ColBoulders shards that fly ColRoll px outward (the Stone
    ///     Colossus's boulders, TickBoulders), GlvShardDamage to each enemy they hit. Going down, the wave ending or the slot
    ///     changing ends it at once, with no shards.
    /// Earth: every hit cracks (Embrittle). Stuns and shoves skip heavies and bosses. All damage scales by your damage.
    /// Mastered: GlvTimeMastered s and the Haymaker GlvMasterWide wider. Rare card Granite Knuckles (Stats.GlvDmgMult, GlvReachMult).
    /// Replicated: Player.GlvT (time left, the gather included; 0 = off), GlvAge (seconds since the cast) and GlvPunch (the punch
    /// number plus its swing 0..1; even numbers are the left fist, every GlvHayEvery-th both). Drawn by Game/RockGlovesLook.cs.
    public partial class Game
    {
        public const float GlvGather = 0.6f, GlvTime = 10f, GlvTimeMastered = 13f, GlvGatherMove = 0.5f;
        public const float GlvJabEvery = 0.09f, GlvJabSwing = 0.16f, GlvJabDamage = 26f, GlvJabFrom = 30f, GlvJabReach = 130f, GlvJabHalf = 23f, GlvJabKnock = 15f;
        public const int GlvHayEvery = 8;
        public const float GlvHayAhead = 120f, GlvHayRadius = 90f, GlvHayDamage = 80f, GlvHayStun = 0.5f, GlvHayKnock = 90f;
        public const float GlvHaySwing = 0.28f, GlvHayRecover = 0.32f, GlvMasterWide = 0.25f;
        public const float GlvShardDamage = 20f;
        public const float GlvFistSide = 62f, GlvFistAhead = 18f;  // where a fist hovers at rest, from your centre
        public const float GlvCrumbleWarn = 1.5f;                  // the look: the fists shed chips over the last this many s
        public const float GlvKnucklesDmg = 0.2f, GlvKnucklesReach = 0.15f;  // the Rare card Granite Knuckles, a stack
        public static readonly Rgba GlvLava = new Rgba(1f, 0.48f, 0.18f);

        /// Rock Gloves's entry: key 3 of the DPS Elementalist's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] GloveUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.RockGloves, Kit = Kit.Elementalist, Role = Role.Dps, Choice = AbilityDef.Ult, UltKey = 3,
                Name = "Rock Gloves", Cooldown = 80f,
                Desc = "Earth builds two giant floating fists for 10 s: hold Fire for rapid punches, and every 8th is a both-fisted Haymaker.",
                Capstone = "Lasts 13 s and the Haymaker hits 25% wider.",
                Bot = b => b.Crowd >= 3 && b.HaveTarget && b.TargetD < GlvJabReach + 70f,
            },
        };

        /// The fists are out (the gather included).
        public static bool GlvOn(Player p) => p.GlvT > 0f && !p.Downed;
        /// Past the gather: Fire punches.
        public static bool GlvReady(Player p) => GlvOn(p) && p.GlvAge >= GlvGather;
        /// Your speed share (Player.Speed): slowed while the fists gather.
        public static float GlvSpeed(Player p) => p.GlvT > 0f && p.GlvAge < GlvGather ? GlvGatherMove : 1f;
        /// The punch number (from 1; 0 before the first) and its swing 0..1, from the replicated GlvPunch.
        public static int GlvPunchNo(Player p) => (int)Math.Floor(p.GlvPunch);
        public static bool GlvIsHay(int n) => n > 0 && n % GlvHayEvery == 0;
        public static float GlvReach(Player p) => GlvJabReach * p.Stats.GlvReachMult;
        public static float GlvHayReach(Player p) => GlvHayRadius * (1f + (p.GlvMastered ? GlvMasterWide : 0f));

        void CastRockGloves(Player p, bool mastered)
        {
            p.GlvT = GlvGather + (mastered ? GlvTimeMastered : GlvTime);
            p.GlvAge = 0f;
            p.GlvPunch = 0f;
            p.GlvStep = 0;
            p.GlvCd = 0f;
            p.GlvSwingT = -1f;
            p.GlvMastered = mastered;
            p.GlvUp = true;
            p.UltT = p.GlvT;
            BroadcastFx(FxKind.TitanSlam, p.Pos, new V2(TitanEarth + 1, 0f), ColEarth, 90f, TitanCrackTime, Sfx.StoneWind);
        }

        /// It ends: on time with the shards; going down, the wave over or the slot changed without.
        void EndRockGloves(Player p, bool shards)
        {
            if (!p.GlvUp)
                return;
            p.GlvUp = false;
            p.GlvT = 0f;
            p.GlvSwingT = -1f;
            p.GlvPunch = 0f;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.RockGloves)
                p.UltT = 0f;
            if (!shards)
                return;
            float first = RandAngle(), dmg = GlvShardDamage * p.Stats.DmgMult;
            for (int i = 0; i < ColBoulders; i++)
                boulders.Add(new Boulder { From = p.Pos, Ang = first + MathX.Tau * i / ColBoulders, Dmg = dmg, Owner = p.Id, Hit = new System.Collections.Generic.HashSet<int>() });
            BroadcastFx(FxKind.TitanSlam, p.Pos, new V2(TitanEarth + 2, 0f), ColEarth, 50f, TitanRubbleTime, Sfx.Explode);
            BroadcastFx(FxKind.TitanSlam, p.Pos, new V2(ColFxBoulders, first), ColEarth, ColRoll, ColRollTime + 0.35f, Sfx.None);
        }

        /// Host, every tick: the clock, the swing, the ends.
        void TickRockGloves(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (!p.GlvUp)
                    continue;
                p.GlvT = Math.Max(0f, p.GlvT - dt);
                p.GlvAge += dt;
                p.GlvCd -= dt;
                if (p.Slots[Upgrades.UltSlot].Id != AbilityId.RockGloves || p.Downed || !Battle)
                {
                    EndRockGloves(p, false);
                    continue;
                }
                if (p.GlvT <= 0f)
                {
                    EndRockGloves(p, true);
                    continue;
                }
                if (p.GlvSwingT >= 0f)
                {
                    p.GlvSwingT += dt;
                    float swing = GlvIsHay(p.GlvStep) ? GlvHaySwing : GlvJabSwing;
                    if (p.GlvSwingT >= swing)
                        p.GlvSwingT = -1f;
                }
                float sw = GlvIsHay(p.GlvStep) ? GlvHaySwing : GlvJabSwing;
                p.GlvPunch = p.GlvStep + (p.GlvSwingT >= 0f ? Math.Min(0.999f, p.GlvSwingT / sw) : 0f);
            }
        }

        /// TickPlayers' fire branch while the fists are out: Fire punches (the weapon is put away).
        void GloveTrigger(Player p)
        {
            if (!GlvReady(p) || !p.Firing || p.ParryActive || p.StunT > 0f || p.GlvCd > 0f || State == MatchState.GameOver)
                return;
            p.GlvStep++;
            p.GlvSwingT = 0f;
            V2 dir = V2.FromAngle(p.Aim);
            if (GlvIsHay(p.GlvStep))
            {
                p.GlvCd = GlvHayRecover;
                p.GlvHays++;
                V2 at = p.Pos + dir * GlvHayAhead;
                float reach = GlvHayReach(p), dmg = GlvHayDamage * p.Stats.DmgMult * p.Stats.GlvDmgMult;
                foreach (var e in AllFoes(by: p.Id))
                {
                    if (e.Pos.FartherOrAt(at, reach + e.Radius))
                        continue;
                    if (!Heavy(e))
                    {
                        KnockEnemy(e, AwayFrom(e.Pos, at, dir) * GlvHayKnock);
                        Stun(e, GlvHayStun);
                    }
                    HurtEnemy(e, dmg, p.Id);
                    Embrittle(e, p.Id);  // earth: cracked for the next hits (Brittle.cs)
                    p.GlvHits++;
                }
                BroadcastFx(FxKind.TitanSlam, at, new V2(TitanEarth, 0f), ColEarth, reach, TitanCrackTime, Sfx.StoneSlam);
                return;
            }
            p.GlvCd = Math.Max(p.GlvCd, -GlvJabEvery * 0.5f) + GlvJabEvery;  // (keeps the leftover, so held Fire averages GlvJabEvery whatever the tick)
            p.GlvJabs++;
            // the fist that punches (odd: right, even: left) comes in a little off your aim line, as it hovers on that side
            float side = p.GlvStep % 2 == 1 ? 1f : -1f;
            V2 off = dir.Orthogonal() * (side * 12f);
            float reachJ = GlvReach(p), dmgJ = GlvJabDamage * p.Stats.DmgMult * p.Stats.GlvDmgMult;
            V2 from = p.Pos + dir * GlvJabFrom + off;
            float len = reachJ - GlvJabFrom;
            bool any = false;
            foreach (var e in AllFoes(by: p.Id))
            {
                V2 d = e.Pos - from;
                float along = Math.Max(0f, Math.Min(len, d.Dot(dir)));
                if (e.Pos.FartherThan(from + dir * along, GlvJabHalf + e.Radius))
                    continue;
                if (!Heavy(e))
                    KnockEnemy(e, dir * GlvJabKnock);
                HurtEnemy(e, dmgJ, p.Id);
                Embrittle(e, p.Id);
                p.GlvHits++;
                any = true;
            }
            if (any)  // dust and a small crack where it lands
                BroadcastFx(FxKind.TitanSlam, from + dir * len, new V2(ColFxStomp, 0f), ColEarth, 30f, 1.2f, Sfx.KnuckleImpact);
        }

        // --- Network: the player's Rock Gloves fields (SyncPlayer) ---

        void SyncRockGloves(NetSync s, Player p)
        {
            s.Float(ref p.GlvT);  // Rock Gloves
            if (p.GlvT > 0f)
            {
                s.Float(ref p.GlvAge);
                s.Float(ref p.GlvPunch);
            }
        }
    }
}
