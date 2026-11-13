using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Riot Captain (Docs/FactionMinis.md): the Warden's faction mini-boss, fought in a mini-boss room (GatekeeperRoom.cs).
    /// A big officer behind a tower shield that stops shots and swings from the front (CaptainShieldArc) and never breaks:
    /// it turns slowly (CaptainTurn), so you get round it. Its attacks come in turn (CaptainOrder):
    ///   Shield charge: a shown line (CaptainWindup), then a rush (CaptainDash px/s for up to CaptainDashTime). Anyone it meets
    ///     is bashed and knocked back. If it runs into a wall it is dazed (CaptainDaze s: no shield, CaptainDazedMult damage);
    ///     otherwise it holds the shield low a moment (CaptainLow) and hits from the front land.
    ///   Buckshot: three fans over the shield's rim.
    ///   Tear gas: three canisters round you (the Gas Grenadier's lob and cloud; WardenFaction.cs).
    ///   Whistle: two Taser Guards come in at the room's corners (at most CaptainAdds at a time; they go when it falls).
    /// Below half health it attacks a quarter faster and charges twice. Its state is Phase (synced), the charge's line is
    /// Facing; nothing new is sent.
    public partial class Game
    {
        public const float CaptainShieldArc = 1.15f;   // blocks within ~65 degrees of where it faces
        public const float CaptainTurn = 1.5f;         // rad/s
        public const float CaptainWindup = 0.9f, CaptainDash = 560f, CaptainDashTime = 0.7f, CaptainLow = 1.2f, CaptainDaze = 2.2f;
        public const float CaptainDazedMult = 1.5f;
        public const float CaptainBashDamage = 18f, CaptainKnock = 220f;
        public const int CaptainAdds = 2;
        public const float CaptainGasWindup = 0.8f, CaptainGasR = 70f;

        // Phase: what it's doing.
        public const byte CapWalk = 0, CapWinding = 1, CapCharging = 2, CapDazed = 3, CapLow = 4, CapGas = 5, CapFans = 6, CapWhistle = 7;

        enum CapMove : byte { Charge, Fans, Gas, Whistle }
        static readonly CapMove[] CaptainOrder = { CapMove.Charge, CapMove.Fans, CapMove.Gas, CapMove.Charge, CapMove.Fans, CapMove.Whistle };

        static bool CaptainEnraged(Enemy e) => e.MaxHp > 0f && e.Hp < e.MaxHp * 0.5f;

        readonly Dictionary<int, V2> captainLast = new Dictionary<int, V2>();  // host: where a charging Captain was last tick (a wall stops it)

        /// Shots and swings from the front stop on the tower shield, except while it's dazed or holding the shield low.
        bool CaptainShieldBlocks(Enemy e, V2 from)
        {
            if (e.Phase == CapDazed || e.Phase == CapLow)
                return false;
            float diff = Math.Abs(MathX.Fmod((from - e.Pos).Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi);
            return diff < CaptainShieldArc;
        }

        /// A blocked hit: the shield rings, but it never breaks.
        void CaptainShieldStruck(Enemy e) =>
            BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(e.Facing) * (e.Radius + 8f), new Rgba(0.8f, 0.9f, 1f), 18f, 0.2f, Sfx.Hit);

        /// Turns slowly while walking; locked in while it winds up, charges or is dazed.
        void CaptainFace(Enemy e, float want, float dt)
        {
            if (e.Phase == CapWinding || e.Phase == CapCharging || e.Phase == CapDazed)
                return;
            float turn = CaptainTurn * (e.Phase == CapLow ? 0.5f : 1f);
            float diff = MathX.Fmod(want - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi;
            e.Facing += Math.Max(-turn * dt, Math.Min(turn * dt, diff));
        }

        V2 CaptainMove(Enemy e, V2 dir, float dist, ref float speed)
        {
            switch (e.Phase)
            {
                case CapCharging:
                    speed = CaptainDash;
                    return V2.FromAngle(e.Facing);
                case CapWalk:
                    return dist > 150f ? dir : V2.Zero;  // walks in on you, shield first
                case CapLow:
                    speed *= 0.4f;
                    return dir;
                default:
                    return V2.Zero;  // plants its feet to wind up, throw, fire or whistle
            }
        }

        void CaptainSpecial(Enemy e, V2 tpos, float dt)
        {
            bool enraged = CaptainEnraged(e);
            float aim = (tpos - e.Pos).Angle;
            e.PhaseT -= dt;
            switch (e.Phase)
            {
                case CapWalk:
                    e.T2 -= dt * (enraged ? 1.33f : 1f);
                    if (e.T2 > 0f)
                        break;
                    var move = CaptainOrder[(int)e.Ang % CaptainOrder.Length];
                    e.Ang += 1f;
                    if (move == CapMove.Whistle && CaptainAddsUp(e) >= CaptainAdds)
                        move = CapMove.Fans;  // its men are still up
                    StartCaptainMove(e, move, tpos, aim, enraged);
                    break;
                case CapWinding:
                    e.Winding = true;
                    if (e.PhaseT <= 0f)
                    {
                        e.Phase = CapCharging;
                        e.PhaseT = CaptainDashTime;
                        e.Winding = false;
                        captainLast[e.Id] = e.Pos;
                        BroadcastSound(Sfx.BossWhoosh);
                    }
                    break;
                case CapCharging:
                {
                    CaptainBash(e);
                    V2 last;
                    bool stopped = captainLast.TryGetValue(e.Id, out last) && e.PhaseT < CaptainDashTime - 0.08f && e.Pos.DistanceTo(last) < CaptainDash * dt * 0.3f;
                    captainLast[e.Id] = e.Pos;
                    if (stopped)
                    {
                        e.Phase = CapDazed;  // into a wall: dazed, shield down
                        e.PhaseT = CaptainDaze;
                        captainLast.Remove(e.Id);
                        BroadcastFx(FxKind.Ring, e.Pos + V2.FromAngle(e.Facing) * e.Radius, new Rgba(0.75f, 0.85f, 1f), 90f, 0.4f, Sfx.BossSlam);
                    }
                    else if (e.PhaseT <= 0f)
                    {
                        captainLast.Remove(e.Id);
                        if (enraged && e.SquadSlot > 0)
                        {
                            e.SquadSlot--;  // the second charge, sooner
                            e.Phase = CapWinding;
                            e.PhaseT = CaptainWindup * 0.6f;
                            e.Facing = aim;
                            BroadcastSound(Sfx.BossCharge);
                        }
                        else
                        {
                            e.Phase = CapLow;
                            e.PhaseT = CaptainLow;
                        }
                    }
                    break;
                }
                case CapDazed:
                case CapLow:
                    if (e.PhaseT <= 0f)
                    {
                        e.Phase = CapWalk;
                        e.T2 = 1.4f;
                    }
                    break;
                case CapGas:
                    e.Winding = e.PhaseT > 0f;
                    if (e.PhaseT <= 0f)
                    {
                        BroadcastSound(Sfx.Throw);
                        e.Phase = CapWalk;
                        e.T2 = 2f;
                    }
                    break;
                case CapFans:
                    if (e.PhaseT <= 0f)
                    {
                        for (int i = 0; i < 7; i++)
                            EnemyShot(e, aim + (i - 3) * 0.14f, 250f, Defs.SmallShot);
                        BroadcastSound(Sfx.ShotShotgun);
                        e.SquadSlot--;
                        e.PhaseT = 0.4f;
                        if (e.SquadSlot <= 0)
                        {
                            e.Phase = CapWalk;
                            e.T2 = 1.8f;
                        }
                    }
                    break;
                case CapWhistle:
                    if (e.PhaseT <= 0f)
                    {
                        CaptainCallsGuards(e);
                        e.Phase = CapWalk;
                        e.T2 = 2.2f;
                    }
                    break;
            }
        }

        void StartCaptainMove(Enemy e, CapMove move, V2 tpos, float aim, bool enraged)
        {
            switch (move)
            {
                case CapMove.Charge:
                    e.Phase = CapWinding;
                    e.PhaseT = CaptainWindup;
                    e.Facing = aim;
                    e.SquadSlot = enraged ? 1 : 0;  // charges still to come after this one
                    BroadcastSound(Sfx.BossCharge);
                    break;
                case CapMove.Fans:
                    e.Phase = CapFans;
                    e.PhaseT = 0.3f;
                    e.SquadSlot = enraged ? 4 : 3;
                    break;
                case CapMove.Gas:
                {
                    e.Phase = CapGas;
                    e.PhaseT = CaptainGasWindup;
                    int n = enraged ? 4 : 3;  // its rings show at once and fill through the wind-up; the clouds land GasFlight after the throw
                    for (int i = 0; i < n; i++)
                    {
                        V2 at = i == 0 ? tpos : tpos + V2.FromAngle(aim + MathX.Tau * i / n + Rand(-0.3f, 0.3f)) * Rand(80f, 130f);
                        at = ClampToArena(at, 60f);
                        BroadcastFx(FxKind.GasLob, at, new V2(e.Id, GasFlight), new Rgba(0.6f, 0.95f, 0.4f), CaptainGasR, CaptainGasWindup + GasFlight, Sfx.None);
                        SpawnPatchAt(PatchKind.Gas, at, CaptainGasWindup + GasFlight, CaptainGasWindup + GasFlight + 4f, CaptainGasR, CaptainGasR);
                    }
                    break;
                }
                case CapMove.Whistle:
                    e.Phase = CapWhistle;
                    e.PhaseT = 0.7f;
                    BroadcastFx(FxKind.Ring, e.Pos, new Rgba(0.75f, 0.91f, 1f), 160f, 0.7f, Sfx.BossSiren);
                    break;
            }
        }

        /// The charge: anyone in front of the shield is bashed and knocked on along its line (once per grace period).
        void CaptainBash(Enemy e)
        {
            V2 fwd = V2.FromAngle(e.Facing);
            foreach (var p in Players.Values)
            {
                if (p.Downed || p.SwallowT > 0f || p.BashGraceT > 0f)
                    continue;
                V2 to = p.Pos - e.Pos;
                if (to.Length > e.Radius + p.Def.BodyR + 16f || to.Normalized().Dot(fwd) < 0.3f)
                    continue;
                p.BashGraceT = RiotKnockGrace;
                if (p.ParryActive && ParryContact(p, e, CaptainBashDamage))
                    continue;
                if (p.InvulnT <= 0f)
                {
                    Damage(p, CaptainBashDamage, e.Def.Name + " (shield charge)");
                    RodTouched(p, e);
                }
                KnockPlayer(p, (fwd + to.Normalized()).Normalized(), CaptainKnock);
                BroadcastFx(FxKind.Spark, p.Pos, new Rgba(0.85f, 0.9f, 1f), 28f, 0.25f, Sfx.Clang);
            }
        }

        int CaptainAddsUp(Enemy e)
        {
            int n = 0;
            foreach (var o in Enemies.Values)
                if (o.ParentId == e.Id && o.Hp > 0f)
                    n++;
            return n;
        }

        /// Two Taser Guards come in at the far corners of its room (or round it, outside a room).
        void CaptainCallsGuards(Enemy e)
        {
            int want = CaptainAdds - CaptainAddsUp(e);
            if (want <= 0)
                return;
            var spots = new List<V2>();
            var areas = RoomsOn ? CurrentWorld.Areas : null;
            if (areas != null && e.Home > 0 && e.Home < areas.Length)
            {
                var a = areas[e.Home];
                float m = 70f;
                spots.Add(new V2(a.Min.X + m, a.Min.Y + m));
                spots.Add(new V2(a.Max.X - m, a.Min.Y + m));
                spots.Add(new V2(a.Min.X + m, a.Max.Y - m));
                spots.Add(new V2(a.Max.X - m, a.Max.Y - m));
                spots.Sort((x, y) => y.DistanceSq(e.Pos).CompareTo(x.DistanceSq(e.Pos)));  // the far ones first
            }
            else
                for (int i = 0; i < 4; i++)
                    spots.Add(ClampToArena(e.Pos + V2.FromAngle(MathX.Tau * i / 4f + 0.7f) * 260f, 40f));
            for (int i = 0; i < want && i < spots.Count; i++)
            {
                var g = SpawnEnemyAt(EnemyType.TaserGuard, spots[i], 0.6f, e.Id);
                g.Home = e.Home;
                g.FireT = 1.2f + 0.9f * i;
                BroadcastFx(FxKind.Boom, spots[i], new Rgba(0.75f, 0.91f, 1f), 60f, 0.4f, Sfx.None);
            }
        }

        /// Host: the Captain is gone (beaten or sent off with the wave): its men go too, with no kill credit.
        void ClearCaptainAdds(int captainId)
        {
            captainLast.Remove(captainId);
            foreach (var o in Enemies.Values)
                if (o.ParentId == captainId && o.Hp > 0f)
                {
                    o.LastHitBy = 0;
                    o.Hp = 0f;
                }
        }
    }
}
