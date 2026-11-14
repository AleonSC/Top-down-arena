using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Shuriken's Great Star stance (Core/GreatStar.cs), run from FourMoreTest (WeaponsByKit.cs) with its host and joining client.
static partial class Program
{
    static void GreatStarTest(Game g, Session client, Player me, Action<float, PlayerInput> play, Func<V2, Enemy> dummy, V2 from)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var dash = typeof(Game).GetMethod("HostWeaponDash", flags);
        var throwIt = typeof(Game).GetMethod("HostGreatStarThrow", flags);
        Action swap = () => { me.StarSwitchCd = 0f; dash.Invoke(g, new object[] { me.Id, 4, me.Pos }); };
        Func<Bullet> star = () => g.Bullets.Values.FirstOrDefault(b => b.Kind == Defs.GreatStar && b.Owner == me.Id);

        // The weapon-action key: into the Great Star stance (every screen sees it), and back.
        me.StarStance = 0;
        swap();
        play(0.2f, new PlayerInput());
        bool inStance = me.StarStance == 1, clientSees = client.Game.Players[me.Id].StarStance == 1;
        swap();
        bool outAgain = me.StarStance == 0;
        swap();
        Console.WriteLine("  great star: stance on {0} (client sees it {1}), off again {2}", inStance, clientSees, outAgain);
        Check(inStance && clientSees && outAgain && me.StarStance == 1, "the weapon-action key swaps the Shuriken between the fan and the Great Star stance, on every screen");

        // A drawn route: up and over, then down to its end. It cuts what's on the route, then comes home cutting again for half.
        float full = me.Weapon.Damage * me.Stats.DmgMult * Game.GreatStarMult;
        var onA = dummy(from + new V2(200f, -100f));
        var onB = dummy(from + new V2(400f, 0f));
        var home = dummy(from + new V2(200f, 0f));    // only on the straight way home
        var off = dummy(from + new V2(200f, 160f));   // nowhere near either
        float a0 = onA.Hp, b0 = onB.Hp, h0 = home.Hp, o0 = off.Hp;
        me.StarCatchCd = 0f;
        throwIt.Invoke(g, new object[] { me, new List<V2> { from + new V2(200f, -100f), from + new V2(400f, 0f) } });
        bool thrown = star() != null;
        float drift = 0f, far = 0f;
        for (int f = 0; f < 240 && star() != null; f++)
        {
            play(1f / 60f, new PlayerInput());
            Bullet hb = star(), cb;
            if (hb != null && client.Game.Bullets.TryGetValue(hb.Id, out cb))
                drift = Math.Max(drift, cb.Pos.DistanceTo(hb.Pos));
            if (hb != null)
                far = Math.Max(far, hb.Pos.X - from.X);
        }
        bool caught = star() == null;
        float hitA = a0 - onA.Hp, hitB = b0 - onB.Hp, hitHome = h0 - home.Hp, hitOff = o0 - off.Hp;
        Console.WriteLine("  great star route: A took {0:0.0}, B (the end) {1:0.0}, on the way home {2:0.0}, off the route {3:0.0} (a pass is {4:0.0}); reached {5:0} px; caught {6}; client drift {7:0.0} px",
            hitA, hitB, hitHome, hitOff, full, far, caught, drift);
        Check(thrown && hitA >= full * 0.99f && hitB >= full * 0.99f && hitOff == 0f && Math.Abs(far - 400f) < 20f,
            "the Great Star flies the drawn route, cutting every enemy on it for 60");
        Check(Math.Abs(hitHome - full * Game.GreatStarBackShare) < 0.5f && caught, "it flies home after the route, cutting again for half, and is caught");
        Check(drift < 40f, "every screen flies the Great Star along the same route (Msg.StarThrow, Msg.Steer)");

        // In the stance, Fire draws instead of throwing the fan; letting go throws the star along what was drawn.
        g.Bullets.Clear();
        me.StarCatchCd = 0f;
        for (int f = 0; f < 20; f++)
            play(1f / 60f, new PlayerInput { Fire = true, AimAt = from + new V2(100f + f * 12f, -60f) });
        int fans = g.Bullets.Values.Count(x => x.Kind == Defs.StarShot), points = g.GreatStarRoute.Count;
        play(1f / 60f, new PlayerInput { AimAt = from + new V2(340f, -60f) });
        play(1f / 60f, new PlayerInput { AimAt = from + new V2(340f, -60f) });
        bool flew = star() != null;
        Console.WriteLine("  great star by hand: fan stars {0}, route points {1}, thrown on letting go {2}", fans, points, flew);
        Check(fans == 0 && points >= 10 && flew, "in the Great Star stance, holding Fire draws the route and letting go throws the star");

        // Out of the stance, Fire throws the fan again.
        g.Bullets.Clear();
        swap();
        me.FireCd = 0f;
        play(0.1f, new PlayerInput { Fire = true, AimAt = from + new V2(300f, 0f) });
        int fanAgain = g.Bullets.Values.Count(x => x.Kind == Defs.StarShot);
        Console.WriteLine("  great star off: fan stars {0}", fanAgain);
        Check(me.StarStance == 0 && fanAgain >= 3, "back in the fan stance, Fire throws stars as before");
        foreach (var e in new[] { onA, onB, home, off }) e.Hp = 0f;
    }
}
