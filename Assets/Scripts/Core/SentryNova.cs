namespace Arena
{
    /// The Nova and Turret abilities' casts (Game/SentryNovaLook.cs draws them):
    ///   Nova    a ring of 24 heavy shots (48 mastered) round you; one `FxKind.Nova` carries the starburst (Pos = the caster,
    ///           Pos2 = (their id, shots fired), Radius = the aim the ring starts from, Dur = NovaFxTime)
    ///   Turret  a Sentry at your feet (two beside you mastered) that shoots for TurretTime; `FxKind.Sentry` carries its
    ///           drop and landing (Pos2 = (0, aim), 2 for a mastered second one, Radius = owner id, Dur = SentryDropFx) and its
    ///           power-down when the time runs out (Pos2 = (1, aim), Dur = SentryFoldFx, from TickMinions).
    ///           The model, its unfolding (the minion's first SentryDeploy seconds), tracking and recoil are drawn from the
    ///           replicated Minion (T, Duration, Aim) and the turret's own shots.
    /// Their sounds are played by each game when the effect arrives (Game/SentryNovaSoundBank.cs), not as Sfx: the Sfx enum
    /// is a byte and nearly full.
    public partial class Game
    {
        public const float NovaFxTime = 0.6f, NovaShotSpeed = 480f;
        public const float SentryDeploy = 0.5f, SentryLand = 0.16f, SentryDropFx = 0.75f, SentryFoldFx = 0.7f;
        public static readonly Rgba NovaColor = new Rgba(0.95f, 0.8f, 1f), SentryColor = new Rgba(0.6f, 0.95f, 1f);

        void CastNova(Player p, Stats s, bool cap)
        {
            int shots = cap ? 48 : 24;
            for (int i = 0; i < shots; i++)
            {
                V2 dir = V2.FromAngle(p.Aim + MathX.Tau * i / shots);
                SpawnBullet(Defs.NovaShot, p.Pos + dir * p.Def.BodyR, dir * NovaShotSpeed, 0f, 1.2f, 22f * s.DmgMult, p.Id);
            }
            BroadcastFx(FxKind.Nova, p.Pos, new V2(p.Id, shots), NovaColor, p.Aim, NovaFxTime, Sfx.None);
        }

        void CastTurret(Player p, bool cap)
        {
            if (cap)
            {
                V2 side = V2.FromAngle(p.Aim).Orthogonal() * 30f;
                DropSentry(p, p.Pos + side, true);
                DropSentry(p, p.Pos - side, false);
            }
            else
                DropSentry(p, p.Pos, true);
        }

        void DropSentry(Player p, V2 at, bool loud)
        {
            var m = SpawnMinion(MinionKind.Turret, p, at, TurretTime);
            m.Aim = p.Aim;
            BroadcastFx(FxKind.Sentry, at, new V2(loud ? 0f : 2f, p.Aim), SentryColor, p.Id, SentryDropFx, Sfx.None);  // (2: the mastered second one, which lands silently)
        }

        /// A turret's time ran out (TickMinions): it powers down and folds away.
        void SentryFolds(Minion m)
        {
            BroadcastFx(FxKind.Sentry, m.Pos, new V2(1f, m.Aim), SentryColor, m.Owner, SentryFoldFx, Sfx.None);
        }
    }
}
