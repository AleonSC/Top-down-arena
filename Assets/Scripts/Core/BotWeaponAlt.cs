using System;

namespace Arena
{
    /// The smart bot's weapon Alt (the weapon-action key, PlayerInput.WeaponAction; LocalWeaponDash in Ninja.cs reads it):
    /// one rule per weapon with a secondary, saying when a person would press it, hold it or let go. A tap is one tick
    /// down and at least one up; holds keep it down until the rule lets go. Cooldowns the host keeps are timed here
    /// with the bot's own clock (botAltCd). Weapons with no rule (or whose Alt needs a drawn route: the Shuriken's
    /// Great Star) never press it. Each rule is its weapon's WeaponDef.BotAlt, kept with the weapon's code; WeaponDef.BotAltHold marks held Alts.
    public delegate bool BotAltFn(Game g, Game.AltView v, float dt, bool late, ref bool firing, ref V2 aimAt);

    public partial class Game
    {
        bool botAltHeld, botAltWas;
        float botAltCd, botAltHoldT, botRapierT;
        public int BotAltPresses;  // presses (tests)

        /// What the rules see this tick.
        public struct AltView
        {
            public Player Me;
            public Enemy Target, ShootAt;
            public bool InReach;
            public float ShootD;   // distance to what it shoots at (9999 without)
            public int Near;       // enemies within BotNearReach
            public int Incoming;   // noticed enemy shots on course to hit within half a second
            public Player Downed, Lowest;
        }

        bool BotWeaponAlt(Player me, float dt, Enemy target, Enemy shootAt, bool inReach, Player downed, Player lowest, ref bool firing, ref V2 aimAt)
        {
            botAltCd -= dt;
            var v = new AltView
            {
                Me = me, Target = target, ShootAt = shootAt, InReach = inReach, Downed = downed, Lowest = lowest,
                ShootD = shootAt != null ? shootAt.Pos.DistanceTo(me.Pos) : 9999f,
                Near = EnemiesNear(me.Pos, BotNearReach),
                Incoming = IncomingShots(me, 0.5f),
            };
            bool down = me.StunT <= 0f && AltRule(v, dt, ref firing, ref aimAt);
            if (down && !botAltWas)
                BotAltPresses++;
            botAltHoldT = down ? botAltHoldT + dt : 0f;
            botAltWas = down;
            botAltHeld = down && me.Weapon.BotAltHold;
            return down;
        }

        /// A press: down for one tick when `want` and the bot's own cooldown allow, then up (the key reads edges).
        bool Tap(bool want, float cd)
        {
            if (!want || botAltWas || botAltCd > 0f)
                return false;
            botAltCd = cd;
            return true;
        }

        /// Noticed enemy shots on course to hit the bot within `look` s.
        int IncomingShots(Player me, float look)
        {
            int n = 0;
            float hitR = me.Def.HitR;
            foreach (var b in botThreats)
            {
                if (!Bullets.ContainsKey(b.Id))
                    continue;
                V2 rel = b.Pos - me.Pos;
                float vv = b.Vel.LengthSq;
                if (vv < 1f)
                    continue;
                float t = -rel.Dot(b.Vel) / vv;
                if (t >= 0f && t <= look && (rel + b.Vel * t).Length <= hitR + b.Radius + 12f)
                    n++;
            }
            return n;
        }

        /// Enemies within `r` of `at` and in front of the bot (within `half` radians of its aim).
        int EnemiesAhead(Player me, float r, float half)
        {
            int n = 0;
            foreach (var e in Enemies.Values)
            {
                if (IsSpawning(e) || SoulBound(e) || e.Hp <= 0f)
                    continue;
                V2 d = e.Pos - me.Pos;
                if (d.Length - e.Radius < r && Math.Abs(MathX.WrapPi(d.Angle - me.Aim)) < half)
                    n++;
            }
            return n;
        }

        bool AltRule(AltView v, float dt, ref bool firing, ref V2 aimAt)
        {
            bool late = DashT > 0f;  // the Alts read after a dash's check would lose the press (LocalWeaponDash)
            var rule = v.Me.Weapon.BotAlt;  // each weapon registers its own rule on its WeaponDef; none: never pressed
            return rule != null && rule(this, v, dt, late, ref firing, ref aimAt);
        }

        // The rules of weapons with no file of their own; the others sit with their weapon (e.g. LanceBotAlt in Lance.cs).

        /// WeaponDef.BotAlt: Double Barrel point-blank (its kick also gets you out).
        static bool ScattergunBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var me = v.Me;
            var e = v.ShootAt;
            return !late && g.Tap(me.ScatShells > 0 && (e != null && v.ShootD < 150f || g.BotDanger > 2f && v.Near > 0), 1f);
        }

        /// WeaponDef.BotAlt: a proximity mine where the enemies are coming from.
        static bool FlakCannonBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            return !late && v.ShootAt != null && v.ShootD < 320f && g.Tap(g.MineLocalCd <= 0f, 2f);
        }

        /// WeaponDef.BotAlt: tear gas on a crowd or the boss.
        static bool RiotRepeaterBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var e = v.ShootAt;
            return !late && e != null && v.InReach && g.Tap(g.GasLocalCd <= 0f && (e.Def.IsBoss || g.EnemiesNear(e.Pos, 100f) >= 3), 2f);
        }

        /// WeaponDef.BotAlt: Heat Dump on a crowd in front once it's hot, or on anything in its face.
        static bool RailgunBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var me = v.Me;
            return g.Tap(me.RailHeat >= 60 && g.EnemiesAhead(me, DumpReach(me.RailHeat) * 0.8f, DumpHalf) >= 2 || me.RailHeat >= DumpMin && g.EnemiesAhead(me, 110f, DumpHalf) >= 1, DumpCd);
        }

        /// WeaponDef.BotAlt: a loaded grenade at a crowd or the boss.
        static bool TwinRifleBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var e = v.ShootAt;
            return !late && e != null && v.InReach && g.Tap(v.Me.TwinNades > 0 && (e.Def.IsBoss || g.EnemiesNear(e.Pos, 90f) >= 2), 0.6f);
        }

        /// WeaponDef.BotAlt: Scatter at a crowd in front, not mid-draw.
        static bool MendersBowBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            return !late && v.ShootAt != null && v.ShootD < ScatterReach * 0.8f && g.bowDraw < 0f && g.Tap(g.EnemiesAhead(v.Me, ScatterReach * 0.8f, ScatterFan * 0.5f) >= 3, ScatterCd);
        }

        /// WeaponDef.BotAlt: Undertow: pull a spread-out group in, then let go and blast (held).
        static bool RepulsionCannonBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var me = v.Me;
            if (g.botAltWas)
                return g.botAltHoldT < 2f && me.RepelMeter > 5 && g.EnemiesAhead(me, 70f, 1f) == 0 && v.Incoming < 3;
            return g.Tap(me.RepelMeter >= 30 && g.EnemiesAhead(me, 300f, 0.6f) >= 2 && g.EnemiesAhead(me, 90f, 0.6f) == 0, 3f);
        }

        /// WeaponDef.BotAlt: Forked Volley whenever it's up.
        static bool ArcLightningBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            return v.ShootAt != null && v.InReach && g.Tap(g.ForkLocalCd <= 0f, 0.5f);
        }

        /// WeaponDef.BotAlt: Orbit up close or under small shots, Throw at range.
        static bool FumaShurikenBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            bool orbit = v.ShootD < 190f || v.Incoming >= 3;
            return g.Tap((v.Me.FumaStance == 1) != orbit && (orbit || v.Incoming == 0), 1.5f);
        }

        /// WeaponDef.BotAlt: blink to a mark near the target when it's safe there, or away from danger.
        static bool KunaiBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var me = v.Me;
            var e = v.ShootAt;
            return !late && me.KunaiMarkT > 0f && g.Tap(e != null && me.KunaiMark.CloserThan(e.Pos, 140f) && g.BotDanger < 1f || g.BotDanger > 2.5f && me.KunaiMark.FartherThan(me.Pos, 120f), 0.5f);
        }

        /// WeaponDef.BotAlt: lock on the target (and let go of a lock that's lost).
        static bool TwinRifleAutoBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var me = v.Me;
            var e = v.ShootAt;
            bool lost = me.TwinLock != 0 && !g.Enemies.ContainsKey(me.TwinLock);
            if (me.TwinLock == 0 && e != null && v.InReach)
            {
                if (!g.Tap(true, 1f))
                    return false;
                aimAt = e.Pos;
                return true;
            }
            return !late && g.Tap(lost, 1f);
        }

        /// Is one of the bot's thrown Embolus Spears within `r` of `at`?
        static bool EsNear(Player me, V2 at, float r)
        {
            for (int i = 0; i < 2; i++)
                if (EsOut(me.EsSt[i]) && me.EsPos[i].CloserThan(at, r))
                    return true;
            return false;
        }

        static int CountBits(int m)
        {
            int n = 0;
            for (; m != 0; m &= m - 1)
                n++;
            return n;
        }
    }
}
