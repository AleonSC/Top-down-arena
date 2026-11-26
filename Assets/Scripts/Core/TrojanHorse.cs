using System;
using System.IO;

namespace Arena
{
    /// Trojan Horse (Tank Knight, the second ultimate: `AbilityId.TrojanHorse`, key 1 of the lobby's Ultimate row), from the
    /// claude.ai artifact "Trojan Horse". A wooden horse on wheels rolls out from you along your aim at HorseSpeed px/s and
    /// parks where a wall, the arena's edge or a prop stops it (or after HorseMaxRoll px); a `ZoneKind.TrojanHorse` zone:
    /// Pos the horse, Aim its heading, Radius its body's half width, BlockHp/SoakMax its health (GuardFrac replicated),
    /// Marched how far it has rolled (replicated: the wheels, and parked once it stops growing), Duration HorseTime.
    ///   - rolling, it shoves regular enemies out of its path (HorseShove);
    ///   - any player within HorseBoardR presses Interact to climb in (`Msg.HorseBoard`: in or out), and again to drop out beside it.
    ///     Inside (`Player.HorseId`, replicated) you go where it goes, nothing can hurt you, enemies don't pick you, and your
    ///     weapon, its Alt, abilities and the Dodge are put away: Fire shoots arrows out of its side slits at your aim
    ///     (`Defs.HorseArrow`: HorseArrowDmg each, every HorseArrowRate s, piercing HorseArrowPierce enemies);
    ///   - its body stops every enemy shot (red aimed ones too) and enemies can't walk through it; each shot and each bite
    ///     wears it (`HorseStops`, `HorseContact`);
    ///   - while every player still up is inside, enemies have nobody to pick and go for the horse (`HorseTarget`);
    ///   - at 0 health it breaks: the riders are thrown out with HorseBreakInvuln s of untouchability. After HorseTime s it
    ///     springs open, Sally Forth: the riders burst out, regular enemies within HorseSallyR are shoved HorseSallyShove px and
    ///     the riders deal HorseSallyMult damage for HorseSallyTime s (`Player.SallyT`, replicated). It also ends when the fight
    ///     does. The cooldown starts when it comes down (TickSlots).
    /// Mastered: HorseHpMastered x health and the arrows pierce one more. Game/TrojanHorseLook.cs draws it.
    public partial class Game
    {
        public const float HorseTime = 20f, HorseSpeed = 90f, HorseMaxRoll = 1400f, HorseShowRoll = 140f;
        public const float HorseHalfW = 30f, HorseBack = 30f, HorseFront = 22f, HorseNose = 76f;  // the body: a capsule from -HorseBack to +HorseFront along the heading, HorseHalfW round
        public const float HorseBaseHp = 1000f, HorseHpPerPlayer = 200f, HorseHpMastered = 1.4f;
        public const float HorseShove = 60f, HorseBoardR = 80f, HorseExit = 56f, HorseBiteEvery = 0.5f;
        public const float HorseArrowDmg = 16f, HorseArrowRate = 0.35f, HorseArrowSpeed = 900f, HorseArrowRange = 640f, HorseSlit = 20f;
        public const int HorseArrowPierce = 1;
        public const float HorseBreakInvuln = 1f, HorseSallyR = 120f, HorseSallyShove = 110f, HorseSallyTime = 4f, HorseSallyMult = 1.25f;
        public const float HorseBotReach = 420f;
        public static readonly Rgba HorseWood = new Rgba(0.69f, 0.48f, 0.27f), HorseGold = Rgba.Gold;

        /// Its entry: key 1 of the Tank Knight's Ultimate row (Last Stand stays the default). Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] HorseUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.TrojanHorse, Kit = Kit.Knight, Role = Role.Tank, Choice = AbilityDef.Ult, UltKey = 1,
                Name = "Trojan Horse", Cooldown = 75f,
                Desc = "A wooden horse rolls out to the arena's edge. Players press Interact to climb in: safe inside, they shoot arrows out of it. When everyone's in, enemies go for the horse.",
                Capstone = "40% more health, and the arrows pierce one more enemy.",
                Bot = b => b.HaveTarget,
            },
        };

        /// The horse's health for this many players.
        public static float HorseHpFor(int players, bool mastered) => (HorseBaseHp + HorseHpPerPlayer * Math.Max(1, players)) * (mastered ? HorseHpMastered : 1f);

        /// Inside a Trojan Horse (the host's word, replicated).
        public static bool Riding(Player p) => p != null && p.HorseId != 0 && !p.Downed;

        /// The horse with this id, still standing, or null.
        public Zone HorseById(int id)
        {
            Zone z;
            return id != 0 && Zones.TryGetValue(id, out z) && z.Kind == ZoneKind.TrojanHorse && z.T > 0f ? z : null;
        }

        /// The horse this player is in, or null.
        public Zone HorseOf(Player p) => Riding(p) ? HorseById(p.HorseId) : null;

        /// Is this player's own horse out (the cooldown waits for it: TickSlots)?
        public bool HorseOut(Player p)
        {
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.TrojanHorse && z.Owner == p.Id && z.T > 0f)
                    return true;
            return false;
        }

        /// Seconds since the horse was cast.
        public static float HorseAge(Zone z) => z.Duration - z.T;

        /// The nearest horse within boarding reach of this point, or null.
        public Zone HorseNear(V2 pos)
        {
            Zone best = null;
            float bestD = HorseBoardR * HorseBoardR;
            foreach (var z in Zones.Values)
            {
                if (z.Kind != ZoneKind.TrojanHorse || z.T <= 0f)
                    continue;
                float d = z.Pos.DistanceSq(pos);
                if (d <= bestD)
                {
                    bestD = d;
                    best = z;
                }
            }
            return best;
        }

        /// Can this player press Interact on a horse now (in one, or by one)?
        public bool HorseInteractable(Player p) => p != null && !p.Downed && Battle && (Riding(p) || HorseNear(p.Pos) != null);

        /// How many players are in this horse.
        public int HorseRiders(Zone z)
        {
            int n = 0;
            foreach (var p in Players.Values)
                if (Riding(p) && p.HorseId == z.Id)
                    n++;
            return n;
        }

        /// The closest point on the body's spine to `pos`.
        public static V2 HorseSpine(Zone z, V2 pos)
        {
            V2 f = V2.FromAngle(z.Aim);
            float u = Math.Max(-HorseBack, Math.Min(HorseFront, (pos - z.Pos).Dot(f)));
            return z.Pos + f * u;
        }

        /// Does a circle of this radius at pos touch the horse's body?
        public static bool HorseTouches(Zone z, V2 pos, float r) => pos.CloserThan(HorseSpine(z, pos), HorseHalfW + r);

        /// Your own game: Interact by a horse, or in one. The host toggles you in or out.
        public void HorseInteract()
        {
            var me = Me;
            if (!HorseInteractable(me))
                return;
            bool board = !Riding(me);
            if (IsHost)
            {
                SetRide(me, board);
                return;
            }
            var w = Proto.Begin(Msg.HorseBoard);
            w.Write(board);  // (what the press means, so a repeat sent before the snapshot catches up can't undo it)
            session.SendToHost(Proto.End(w));
        }

        /// Host: a player's Interact press on a horse.
        public void OnHorseBoard(int peer, BinaryReader r)
        {
            bool board = r.ReadBoolean();
            Player p;
            if (IsHost && Players.TryGetValue(peer, out p))
                SetRide(p, board);
        }

        /// Host: climb in (board) or drop out.
        void SetRide(Player p, bool board)
        {
            if (p.Downed || !Battle)
                return;
            var z = HorseOf(p);
            if (z != null)
            {
                if (board)
                    return;  // (already in)
                GetOffHorse(p, z, V2.FromAngle(z.Aim + MathX.Pi / 2f * (p.Id % 2 == 0 ? 1f : -1f)));
                return;
            }
            z = board ? HorseNear(p.Pos) : null;
            if (z == null)
                return;
            p.HorseId = z.Id;
            p.HorseCd = 0f;
            p.HorseBoards++;
            MovePlayer(p, z.Pos);
            BroadcastFx(FxKind.Ring, z.Pos, HorseWood, 44f, 0.3f, Sfx.CoverDeploy);
        }

        /// Host: out of the horse, put down beside it in this direction.
        void GetOffHorse(Player p, Zone z, V2 dir)
        {
            p.HorseId = 0;
            V2 at = z.Pos + dir * HorseExit;
            at = KeepInArena(at, p.Def.HitR);
            if (Props.Count > 0 || LayoutOn)
                at = PushOutOfProps(at, p.Def.HitR + PropPad);
            MovePlayer(p, at);
        }

        /// Host: the horse rolls out.
        void CastHorse(Player p, bool mastered)
        {
            var z = AddZone(ZoneKind.TrojanHorse, p, p.Pos, HorseHalfW, HorseTime);
            z.Aim = p.Aim;
            z.SoakMax = z.BlockHp = HorseHpFor(Players.Count, mastered);
            z.GuardFrac = 1f;
            z.Cap = mastered;
            z.Marched = 0f;
            z.HitAt = new System.Collections.Generic.Dictionary<int, float>();
            p.UltT = HorseTime;
            BroadcastFx(FxKind.Ring, p.Pos, HorseGold, 70f, 0.45f, Sfx.CoverDeploy);
        }

        /// Host, from TickZones: the roll, the shove, and the riders kept on board.
        void TickHorse(Zone z, float dt)
        {
            if (!Battle)
            {
                EndHorse(z, false, quiet: true);
                return;
            }
            if (!z.Quiet)
            {
                V2 dir = V2.FromAngle(z.Aim);
                V2 want = z.Pos + dir * HorseSpeed * dt;
                V2 nose = want + dir * HorseNose;
                bool blocked = ClampToArena(want, HorseHalfW + 8f).FartherThan(want, 0.5f) || ClampToArena(nose, 6f).FartherThan(nose, 0.5f)  // the map's edge
                    || Walled && (KeepInArena(want, HorseHalfW).FartherThan(want, 0.5f) || KeepInArena(nose, 6f).FartherThan(nose, 0.5f));  // a wall
                if (!blocked && (Props.Count > 0 || LayoutOn))
                    blocked = PushOutOfProps(want, HorseHalfW).FartherThan(want, 0.5f) || PushOutOfProps(nose, 6f).FartherThan(nose, 0.5f);  // a prop
                float limit = ShowcaseMode ? HorseShowRoll : HorseMaxRoll;
                if (blocked || z.Marched >= limit)
                {
                    z.Quiet = true;  // parked
                    BroadcastFx(FxKind.Ring, z.Pos, HorseWood, 60f, 0.35f, Sfx.Clang);
                }
                else
                {
                    z.Marched += want.DistanceTo(z.Pos);
                    z.Pos = want;
                    foreach (var e in Enemies.Values)
                        if (Targetable(e) && !Heavy(e) && HorseTouches(z, e.Pos, e.Radius))
                        {
                            V2 side = dir.Orthogonal();
                            float s = (e.Pos - z.Pos).Dot(side) >= 0f ? 1f : -1f;
                            KnockEnemy(e, side * s * HorseShove);
                        }
                }
            }
            foreach (var p in Players.Values)
                if (p.HorseId == z.Id)
                {
                    if (p.Downed)
                        p.HorseId = 0;
                    else
                        p.Pos = z.Pos;
                }
        }

        /// Host: the horse comes down. `broke`: worn to nothing (riders thrown out, untouchable a moment); otherwise Sally Forth
        /// (riders burst out stronger, enemies near it shoved). `quiet`: the fight ended, everyone just steps out.
        void EndHorse(Zone z, bool broke, bool quiet = false)
        {
            if (z.T <= 0f && z.BlockHp <= 0f)
                return;
            int k = 0;
            foreach (var p in Players.Values)
            {
                if (p.HorseId != z.Id)
                    continue;
                float a = z.Aim + MathX.Pi / 2f + k++ * 2.1f;
                GetOffHorse(p, z, V2.FromAngle(a));
                if (quiet)
                    continue;
                if (broke)
                    p.InvulnT = Math.Max(p.InvulnT, HorseBreakInvuln);
                else
                    p.SallyT = HorseSallyTime;
            }
            Player owner;
            if (Players.TryGetValue(z.Owner, out owner) && owner.Slots[Upgrades.UltSlot].Id == AbilityId.TrojanHorse)
                owner.UltT = 0f;
            if (!quiet && !broke)
                foreach (var e in FoesInCircle(z.Pos, HorseSallyR, by: z.Owner))
                    if (!Heavy(e))
                        ShoveFrom(e, z.Pos, HorseSallyShove);
            if (!quiet)
                BroadcastFx(FxKind.TrojanEnd, z.Pos, z.Pos + V2.FromAngle(z.Aim), HorseWood, broke ? 1f : 0f, 1.2f, broke ? Sfx.CoverBreak : Sfx.Roar);
            z.BlockHp = 0f;
            z.GuardFrac = 0f;
            z.T = 0f;
        }

        /// Host, every tick: riders whose horse is gone (the zones cleared) step out where they are; Sally Forth runs down.
        void TickHorseRiders(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.SallyT = Math.Max(0f, p.SallyT - dt);
                if (p.HorseId != 0 && HorseById(p.HorseId) == null)
                    p.HorseId = 0;
            }
        }

        /// Wear the horse; at 0 it breaks.
        void WearHorse(Zone z, float amount)
        {
            if (z.T <= 0f || amount <= 0f)
                return;
            z.BlockHp -= amount;
            z.GuardFrac = z.SoakMax > 0f ? Math.Max(0f, z.BlockHp / z.SoakMax) : 0f;
            if (z.BlockHp <= 0f)
                EndHorse(z, true);
        }

        /// Host, from the enemy-bullet loop: an enemy shot that touches a horse stops there and wears it (red aimed ones too).
        bool HorseStops(Bullet b)
        {
            foreach (var z in Zones.Values)
            {
                if (z.Kind != ZoneKind.TrojanHorse || z.T <= 0f || !HorseTouches(z, b.Pos, b.Radius))
                    continue;
                WearHorse(z, b.Dmg * EnemyDamageScale(Math.Max(1, Wave)));
                BroadcastFx(FxKind.Spark, b.Pos, HorseWood, 8f, 0.2f, Sfx.None);
                return true;
            }
            return false;
        }

        /// Host, from TickEnemies: an enemy's body against a horse stays outside it, and bites it every HorseBiteEvery s.
        void HorseContact(Enemy e, float contact)
        {
            foreach (var z in Zones.Values)
            {
                if (z.Kind != ZoneKind.TrojanHorse || z.T <= 0f || !HorseTouches(z, e.Pos, e.Radius))
                    continue;
                V2 spine = HorseSpine(z, e.Pos), d = e.Pos - spine;
                e.Pos = ClampToArena(spine + (d.LengthSq > 0.01f ? d.Normalized() : V2.FromAngle(z.Aim).Orthogonal()) * (HorseHalfW + e.Radius), e.Radius);
                float age = HorseAge(z), last;
                if (contact > 0f && z.HitAt != null && (!z.HitAt.TryGetValue(e.Id, out last) || age - last >= HorseBiteEvery))
                {
                    z.HitAt[e.Id] = age;
                    WearHorse(z, contact * EnemyDamageScale(Math.Max(1, Wave)));
                }
            }
        }

        /// Host, from TickEnemies: an enemy with nobody to pick (everyone up is in a horse) goes for the nearest horse with riders.
        V2 HorseTarget(Enemy e, Player target, V2 tpos)
        {
            if (target != null && !Riding(target))
                return tpos;
            Zone best = null;
            float bestD = float.MaxValue;
            foreach (var z in Zones.Values)
            {
                if (z.Kind != ZoneKind.TrojanHorse || z.T <= 0f || HorseRiders(z) == 0)
                    continue;
                float d = z.Pos.DistanceSq(e.Pos);
                if (d < bestD)
                {
                    bestD = d;
                    best = z;
                }
            }
            return best != null ? best.Pos : tpos;
        }

        /// Are enemies going for this horse (it has riders and nobody up is left outside)?
        public bool HorseHunted(Zone z)
        {
            if (z == null || HorseRiders(z) == 0)
                return false;
            foreach (var p in Players.Values)
                if (!p.Downed && !Riding(p))
                    return false;
            return true;
        }

        /// Host, from TickPlayers: a rider's Fire shoots an arrow out of the slit on the side nearer the aim.
        void TickHorseBow(Player p, float dt)
        {
            p.HorseCd -= dt;
            var z = HorseOf(p);
            if (z == null || !p.Firing || p.ParryActive || p.HorseCd > 0f || State == MatchState.GameOver)
                return;
            p.HorseCd = HorseArrowRate;
            V2 dir = V2.FromAngle(p.Aim), head = V2.FromAngle(z.Aim), side = head.Orthogonal();
            float s = dir.Dot(side) >= 0f ? 1f : -1f;
            V2 from = z.Pos + side * (s * HorseSlit);
            var b = SpawnBullet(Defs.HorseArrow, from, dir * HorseArrowSpeed, 0f, HorseArrowRange / HorseArrowSpeed, HorseArrowDmg * p.Stats.DmgMult, p.Id);
            b.Pierce = HorseArrowPierce + (z.Cap ? 1 : 0);
            p.HorseArrows++;
        }

        /// Sally Forth's damage boost for this attacker (1 when it isn't on).
        float SallyMult(int attacker)
        {
            Player p;
            return attacker > 0 && Players.TryGetValue(attacker, out p) && p.SallyT > 0f ? HorseSallyMult : 1f;
        }

        float horsePressT;

        /// A bot: walk to a horse within HorseBotReach and climb in; stay in.
        void BotHorse(Player me, ref V2 move, float dt)
        {
            horsePressT -= dt;
            if (me.Downed || Riding(me) || !Battle)
                return;
            Zone best = null;
            float bestD = HorseBotReach * HorseBotReach;
            foreach (var z in Zones.Values)
            {
                if (z.Kind != ZoneKind.TrojanHorse || z.T <= 2f || z.GuardFrac < 0.25f)
                    continue;
                float d = z.Pos.DistanceSq(me.Pos);
                if (d < bestD)
                {
                    bestD = d;
                    best = z;
                }
            }
            if (best == null)
                return;
            if (me.Pos.CloserThan(best.Pos, HorseBoardR * 0.8f))
            {
                if (horsePressT <= 0f)
                {
                    horsePressT = 0.5f;
                    HorseInteract();
                }
                move = V2.Zero;
                return;
            }
            move = (best.Pos - me.Pos).Normalized();
        }

        // --- Network: the player's Trojan Horse fields (SyncPlayer) ---

        void SyncTrojanHorse(NetSync s, Player p)
        {
            s.Int(ref p.HorseId);  // Trojan Horse
            s.Float(ref p.SallyT);
        }
    }
}
