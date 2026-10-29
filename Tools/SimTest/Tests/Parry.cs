using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The parry and its upgrades.
static partial class Program
{
    // The parry: blocks a boss's fire from the front, breaks after its limit, stops you shooting,
    // reflects shots on a perfect parry, and everyone sees it.
    static void ParryTest(float dt)
    {
        var s = StartBuffed(dt, 5, 0);
        Run(s, dt, 10, () => { var b = Find(s[0], EnemyType.Boss); return b != null && b.SpawnT <= 0f; });
        var w = Find(s[0], EnemyType.Boss);
        var me = s[0].Game.Players[1];
        s[1].Game.Bot = false;
        s[0].Game.Bot = true;  // aims at the Warden and holds the trigger
        Action pin = () =>
        {
            foreach (var e in s[0].Game.Enemies.Values)
                if (!e.Def.IsBoss) e.Hp = 0f;
            var bw = Find(s[0], EnemyType.Boss);
            if (bw != null)
                me.Pos = bw.Pos + new V2(0f, 170f);  // stand in front of it
            s[1].Game.Players[2].Pos = new V2(60f, Game.Height - 60f);  // the friend stays out of the way
        };
        // Down for a moment first, then raise it just as a shot is about to land (a perfect parry).
        Run(s, dt, 1f, null, pin);
        Run(s, dt, 6f, () =>
        {
            foreach (var b in s[0].Game.Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind) && b.Pos.DistanceTo(me.Pos) < me.Def.BodyR + 45f && (b.Vel.X * (me.Pos.X - b.Pos.X) + b.Vel.Y * (me.Pos.Y - b.Pos.Y)) > 0f)
                    return true;
            return false;
        }, pin);
        float taken0 = me.Run.DamageTaken;
        int myShots = 0, reflected = 0;
        var seenShots = new HashSet<int>();
        foreach (var b in s[0].Game.Bullets.Values)
            seenShots.Add(b.Id);  // shots already in the air before raising it don't count
        bool clientSeesIt = false, clientSeesShatter = false, broke = false;
        float takenWhileUp = -1f, upFor = 0f;
        s[0].Game.HoldParry = true;
        Run(s, dt, 12f, () => broke, () =>
        {
            pin();
            foreach (var b in s[0].Game.Bullets.Values)
                if (b.Owner == 1 && seenShots.Add(b.Id))
                {
                    if (b.Kind == Defs.ArcaneShot) reflected++;
                    else if (b.Kind != Defs.GrenadeShot) myShots++;
                }
            clientSeesIt |= s[1].Game.Players[1].Parrying;
            foreach (var fx in s[1].Game.Effects)
                clientSeesShatter |= fx.Kind == FxKind.Shatter;
            if (!broke) upFor += dt;
            if (me.ParryBroken > 0f && !broke)
            {
                broke = true;
                takenWhileUp = me.Run.DamageTaken - taken0;
            }
        });
        float takenAtBreak = me.Run.DamageTaken;
        // A shot straight at you from the front, so this doesn't rest on the Warden happening to fire in time.
        var shoot = typeof(Game).GetMethod("SpawnBullet", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        shoot.Invoke(s[0].Game, new object[] { Defs.SmallShot, me.Pos + new V2(0f, -70f), new V2(0f, 300f), 0f, 2f, 10f, 0, false, 0f });
        Run(s, dt, 2f, null, pin);
        float takenAfter = me.Run.DamageTaken - takenAtBreak;
        Console.WriteLine("  parry: held {6:0.0}s against the Warden before breaking, damage taken while up {1:0}, after it broke {2:0}, own shots while up {3}, reflected {4}, client sees it {5}",
            broke, takenWhileUp, takenAfter, myShots, reflected, clientSeesIt, upFor);
        Check(broke && takenWhileUp < 20f, "the parry blocks a boss's fire from the front until it breaks");
        Check(takenAfter > 0f, "once broken, the parry stops blocking");
        Check(myShots == 0, "you can't shoot while parrying");
        Check(reflected > 0, "a perfect parry sends the shots back");
        Check(clientSeesIt, "other players see your parry shield");
        Check(clientSeesShatter, "other players see your parry shield shatter when it breaks");
        s[0].Game.HoldParry = false;
        s[0].Leave(""); s[1].Leave("");
    }

    // The parry upgrades: Reinforced, Wide and Light Guard, Riposte, Counterstance, Mirror Guard.
    static void ParryUpgradeTest(float dt)
    {
        var s = StartBuffed(dt, 5, 0);
        Run(s, dt, 10, () => { var b = Find(s[0], EnemyType.Boss); return b != null && b.SpawnT <= 0f; });
        var me = s[0].Game.Players[1];
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        s[0].Game.Bot = s[1].Game.Bot = false;

        // Wide Guard: a point 75 degrees off your aim is only covered with it.
        me.Aim = 0f;
        V2 side = me.Pos + V2.FromAngle(1.3f) * (me.Def.BodyR + 10f);
        bool narrowBefore = !Game.InParryArc(me, side, 2f);
        for (int k = 0; k < 2; k++) s[0].Game.GrantUpgrade(1, U("Wide Guard"));
        bool wideAfter = Game.InParryArc(me, side, 2f);
        Check(narrowBefore && wideAfter, "Wide Guard widens the parry arc");

        // Reinforced Guard: +25% durability per stack.
        for (int k = 0; k < 4; k++) s[0].Game.GrantUpgrade(1, U("Reinforced Guard"));
        Check(Math.Abs(Game.ParryMax(me) - Game.ParryCapacity * 2f) < 0.01f, "Reinforced Guard x4 doubles parry durability");

        // Light Guard: full speed while parrying.
        Func<float> speedWhileParrying = () =>
        {
            s[0].Game.HoldParry = true;
            V2 start = me.Pos;
            for (int i = 0; i < 30; i++)
            {
                s[0].Poll();
                s[0].Game.FixedStep(dt, new PlayerInput { Move = new V2(1f, 0f), AimAt = me.Pos + new V2(0f, -100f) });
                foreach (var p in s[0].Game.Players.Values) p.InvulnT = 1f;
            }
            s[0].Game.HoldParry = false;
            float dx = Math.Abs(me.Pos.X - start.X);
            return Math.Min(dx, Game.Width - dx) / (30 * dt);
        };
        float slow = speedWhileParrying();
        s[0].Game.GrantUpgrade(1, U("Light Guard"));
        float fast = speedWhileParrying();
        Console.WriteLine("  parry move speed: {0:0} -> {1:0} px/s (normal {2:0})", slow, fast, me.Speed);
        Check(slow < me.Speed * 0.8f && fast > me.Speed * 0.95f, "Light Guard removes the parry slowdown");

        // Mirror Guard, Riposte and Counterstance: block the Warden's fire and look at what comes back.
        for (int k = 0; k < 3; k++) s[0].Game.GrantUpgrade(1, U("Mirror Guard"));
        s[0].Game.GrantUpgrade(1, U("Riposte"));
        s[0].Game.GrantUpgrade(1, U("Counterstance"));
        int reflected = 0;
        float biggestReflect = 0f;
        bool counter = false;
        var seen = new HashSet<int>();
        foreach (var b in s[0].Game.Bullets.Values) seen.Add(b.Id);
        Action pin = () =>
        {
            foreach (var e in s[0].Game.Enemies.Values)
                if (!e.Def.IsBoss) e.Hp = 0f;
            var w = Find(s[0], EnemyType.Boss);
            if (w != null)
                me.Pos = w.Pos + new V2(0f, 170f);
            s[1].Game.Players[2].Pos = new V2(60f, Game.Height - 60f);
            foreach (var b in s[0].Game.Bullets.Values)
                if (b.Owner == 1 && b.Kind == Defs.ArcaneShot && seen.Add(b.Id))
                {
                    reflected++;
                    biggestReflect = Math.Max(biggestReflect, b.Dmg);
                }
            counter |= me.CounterT > 0f;
        };
        Run(s, dt, 1f, null, pin);  // shield down long enough that raising it is a fresh parry
        s[0].Game.Bot = true;       // aims at the Warden
        s[0].Game.HoldParry = true;
        Run(s, dt, 8f, () => reflected > 10 && counter, pin);  // up to 8 s: the Warden rests between its attacks
        s[0].Game.HoldParry = false;
        float plain = 12f * me.Stats.DmgMult;
        Console.WriteLine("  mirror/riposte: {0} shots reflected, strongest {1:0.0} (plain reflect {2:0.0}), counterstance {3}", reflected, biggestReflect, plain, counter);
        Check(reflected > 10, "Mirror Guard sends blocked shots back");
        Check(biggestReflect >= plain * 1.99f, "Riposte doubles reflected shot damage");
        Check(counter, "Counterstance kicks in after a perfect parry");
        s[0].Leave(""); s[1].Leave("");
    }
}
