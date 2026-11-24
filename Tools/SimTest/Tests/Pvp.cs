using System;
using System.Linq;
using System.Reflection;
using Arena;

// PvP in the Hangout (Core/Pvp.cs, Docs/Targeting.md step 3): players' attacks hurt each other at PvpDamage, never themselves, not
// in a normal run; every weapon (fire and Alt) and every ability can be turned on a player without breaking anything.
static partial class Program
{
    static void PvpTest(float dt)
    {
        int port = nextPort++;
        var host = new Session();
        host.Host(port, "Host", Role.Dps);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Tank);
        WaitForLobby(host, client);
        PollBoth(host, client, 80);
        client.StartHangout();
        WaitForGame(host, client, dt);
        var both = new[] { host, client };
        Run(both, dt, 1f);
        var g = host.Game;
        var me = g.Me;
        var them = g.Players[client.Game.MyId];
        Check(g.PvpOn && g.HangoutOn, "PvP is on in the Hangout");
        g.Enemies.Clear();  // (the dummies)
        var spots = typeof(Game).GetField("dummySpots", BindingFlags.NonPublic | BindingFlags.Instance);
        ((System.Collections.IList)spots.GetValue(g)).Clear();

        Action place = () =>
        {
            me.Pos = me.Disp = new V2(500f, 450f);
            them.Pos = them.Disp = new V2(600f, 450f);
            client.Game.Me.Pos = client.Game.Me.Disp = them.Pos;
        };
        // The host's own gun on the client: it hurts them, at PvpDamage, and never the shooter.
        place();
        float hp0 = them.Hp, mine0 = me.Hp;
        for (int f = 0; f < 90 && them.Hp >= hp0; f++)
        {
            place();
            host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = true, AimAt = them.Pos }); g.Update(dt);
            Step(client, dt);
        }
        Check(them.Hp < hp0 || them.WarpT > 0f, "a player's shots hurt another player (" + (hp0 - them.Hp).ToString("0.0") + ")");
        Check(me.Hp >= mine0 - 0.01f, "...and never the shooter");
        Check(Game.PvpDamage > 0f && Game.PvpDamage < 1f, "at a share of the weapon's damage (" + Game.PvpDamage + ")");

        // Every weapon, fire and Alt, at a player: nothing throws, and most of them land.
        int hurtBy = 0, tried = 0;
        var missed = new System.Collections.Generic.List<string>();
        string broke = null;
        foreach (var w in Defs.Weapons)
        {
            tried++;
            me.Weapon = w;
            them.Hp = them.MaxHp; them.InvulnT = 0f; them.WarpT = 0f;
            float before = them.Hp, beams = 0f;
            try
            {
                for (int f = 0; f < 150; f++)
                {
                    place();
                    them.InvulnT = 0f;
                    host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = f % 60 < 45, WeaponAction = f % 30 == 5, AimAt = them.Pos }); g.Update(dt);
                    Step(client, dt);
                    beams = Math.Max(beams, them.WarpT);
                }
            }
            catch (Exception ex) { broke = w.Name + ": " + ex.GetType().Name + " " + ex.Message; break; }
            if (them.Hp < before || beams > 0f) hurtBy++; else missed.Add(w.Name);
            g.Bullets.Clear(); g.Minions.Clear(); g.Zones.Clear();
        }
        Check(broke == null, "every weapon fires and uses its Alt on a player without breaking" + (broke != null ? " (" + broke + ")" : ""));
        Console.WriteLine("  no PvP hit yet: " + string.Join(", ", missed));
        Check(hurtBy > tried / 2, "most weapons hurt a player (" + hurtBy + "/" + tried + ")");

        // Every ability cast at a player: nothing throws.
        var cast = typeof(Game).GetMethod("CastAbility", BindingFlags.NonPublic | BindingFlags.Instance);
        broke = null;
        int casts = 0;
        foreach (var ab in Upgrades.Abilities)
        {
            if (ab == null || ab.Id == AbilityId.None) continue;
            me.Slots[1].Id = ab.Id; me.Slots[1].Charges = me.Slots[1].MaxCharges = 1;
            them.Hp = them.MaxHp; them.WarpT = 0f;
            try
            {
                place();
                cast.Invoke(g, new object[] { me, 1, them.Pos });
                for (int f = 0; f < 40; f++)
                {
                    place();
                    them.InvulnT = 0f;
                    host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = them.Pos }); g.Update(dt);
                    Step(client, dt);
                }
                casts++;
            }
            catch (Exception ex) { var inner = ex.InnerException ?? ex; broke = ab.Name + ": " + inner.GetType().Name + " " + inner.Message; break; }
            g.Bullets.Clear(); g.Minions.Clear(); g.Zones.Clear(); g.Enemies.Clear();
            me.Downed = false; them.Downed = false;
        }
        Check(broke == null && casts > 40, "every ability cast at a player without breaking (" + casts + ")" + (broke != null ? ": " + broke : ""));
        Check(!them.Downed && !me.Downed, "nobody goes down: PvP hits beam you out at 0 like anything else");
        host.Leave("");
        client.Leave("");

        // Not in a normal run: a query with an attacker gives only enemies.
        var plain = new Session().Game;
        Check(!plain.PvpOn, "no PvP outside the Hangout");
    }
}
