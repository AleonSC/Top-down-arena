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
        WaitForPractice(host, client, dt);
        var both = new[] { host, client };
        Run(both, dt, 1f);
        var g = host.Game;
        var me = g.Me;
        var them = g.Players[client.Game.MyId];
        Check(g.PvpOn && g.HangoutOn && client.Game.PvpOn, "PvP is always on in Practice, on every screen");
        Check(g.Enemies.Count == 0, "no target dummies in Practice");

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

        // Status effects: what an attack leaves on a player's stand-in becomes their own, halved and capped (TickPvp).
        {
            var F = BindingFlags.NonPublic | BindingFlags.Instance;
            var standOf = typeof(Game).GetMethod("StandOf", F);
            Func<Enemy> S = () => (Enemy)standOf.Invoke(g, new object[] { them });
            Action<int> tick = n => { for (int i = 0; i < n; i++) { place(); host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = them.Pos }); g.Update(dt); Step(client, dt); } };
            them.StunT = 0f; them.PvpSlowT = 0f; them.Hp = them.MaxHp;
            typeof(Game).GetMethod("Stun", F).Invoke(g, new object[] { S(), 4f });
            tick(1);
            Check(them.StunT > 0.9f && them.StunT <= Game.PvpStunCap + 0.01f, "a stun on a player: halved and capped (" + them.StunT.ToString("0.00") + " s)");
            tick((int)(Game.PvpStunCap / dt) + 5);
            g.Slow(S(), 4f, 0.2f);
            tick(2);
            Check(them.PvpSlowT > 1f && them.PvpSlowMult >= Game.PvpSlowFloor - 0.01f && them.PvpSlowMult < 1f, "a slow: halved, never below " + Game.PvpSlowFloor + " speed (" + them.PvpSlowMult.ToString("0.00") + ")");
            Check(client.Game.Me.PvpSlowT > 0.5f && client.Game.Me.Speed < client.Game.Me.Def.Speed * 0.9f, "...and the slowed player's own game moves them slower");
            tick(220);
            V2 before = client.Game.Me.Pos;
            typeof(Game).GetMethod("KnockEnemy", F).Invoke(g, new object[] { S(), new V2(120f, 0f) });
            for (int i = 0; i < 40; i++) { host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = them.Pos }); g.Update(dt); Step(client, dt); }
            Check(client.Game.Me.Pos.X > before.X + 30f, "a push knocks the player back on their own screen (" + (client.Game.Me.Pos.X - before.X).ToString("0") + " px)");
            them.Hp = them.MaxHp;
            float hpB = them.Hp;
            var bleed = S(); bleed.BleedT = 2f; bleed.BleedDps = 20f; bleed.BleedBy = me.Id;
            tick(60);
            Check(them.Hp < hpB - 3f && !them.Downed, "a bleed from another player ticks on them (" + (hpB - them.Hp).ToString("0.0") + ")");
            var mine = (Enemy)standOf.Invoke(g, new object[] { me });
            float myHp = me.Hp;
            mine.BleedT = 2f; mine.BleedDps = 20f; mine.BleedBy = me.Id;
            tick(60);
            Check(me.Hp >= myHp - 0.01f, "...but never your own on yourself");

            // Marks and other states.
            var hurt = typeof(Game).GetMethod("HurtEnemy", F);
            Func<float> hitOnce = () => { them.Hp = them.MaxHp; them.InvulnT = 0f; float h0 = them.Hp; hurt.Invoke(g, new object[] { S(), 40f, me.Id }); return h0 - them.Hp; };
            S().MarkT = 0f;
            float unmarked = hitOnce();
            var mk = S(); mk.MarkT = 3f; mk.MarkMult = 1.5f;
            float marked = hitOnce();
            Check(unmarked > 0f && Math.Abs(marked / unmarked - 1.5f) < 0.05f, "a mark on a player raises the hits they take, as on an enemy (" + unmarked.ToString("0.0") + " -> " + marked.ToString("0.0") + ")");
            S().MarkT = 0f;
            tick((int)((Game.PvpStunCap + Game.PvpStunImmune) / dt) + 10);
            them.StunT = 0f;
            S().GraspT = 2f;
            tick(1);
            Check(them.StunT > 0.5f, "a hold (Leech Tendrils' grasp) stuns the player (" + them.StunT.ToString("0.00") + " s)");
            them.StunT = 0f;
            S().GraspT = 2f;
            tick(1);
            Check(them.StunT <= 0f, "...and right after a stun, another can't chain onto them (PvpStunImmune)");
            tick((int)((Game.PvpStunCap + Game.PvpStunImmune) / dt) + 10);
            them.PvpSlowT = 0f;
            S().WetT = 3f;
            tick(2);
            Check(them.PvpSlowT > 0f && them.PvpSlowMult < 1f, "wet (Tsunami, Bullet Rain) slows a player too");
            S().WetT = 0f;
            them.Hp = them.MaxHp;
            float hpV = them.Hp;
            var vn = S(); vn.VenomT = 2f; vn.VenomDps = 20f; vn.VenomBy = me.Id;
            tick(60);
            Check(them.Hp < hpV - 3f, "venom (Poison Dart) ticks on a player (" + (hpV - them.Hp).ToString("0.0") + ")");
        }

        // Walls: another player's Blood Wall and Ossuary Wall keep you off them and hurt you; your own never touch you.
        {
            var F = BindingFlags.NonPublic | BindingFlags.Instance;
            Action<int, PlayerInput> tickIn = (n, inp) => { for (int i = 0; i < n; i++) { host.Poll(); g.FixedStep(dt, inp); g.Update(dt); Step(client, dt); } };
            g.Enemies.Clear(); g.Zones.Clear();
            // Blood Wall: the host lays one walking right along y = 300, then the client stands on it.
            me.Pos = me.Disp = new V2(300f, 300f);
            typeof(Game).GetMethod("OpenBloodWall", F).Invoke(g, new object[] { me, 1f, false });
            for (int i = 0; i < 90; i++) tickIn(1, new PlayerInput { Move = new V2(1f, 0f), AimAt = new V2(900f, 300f) });
            int nodes = g.Zones.Values.Count(z => z.Kind == ZoneKind.BloodWall);
            them.Hp = them.MaxHp; them.InvulnT = 0f;
            V2 onWall = new V2(420f, 300f);
            them.Pos = them.Disp = client.Game.Me.Pos = client.Game.Me.Disp = onWall;
            float hpW = them.Hp;
            // (held on it every tick: the wall shoves you off within a few frames and bleeds only on its 0.25 s contact tick)
            for (int i = 0; i < 40; i++) { them.InvulnT = 0f; them.Pos = them.Disp = client.Game.Me.Pos = client.Game.Me.Disp = onWall; tickIn(1, new PlayerInput { AimAt = new V2(900f, 300f) }); }
            Check(nodes > 2 && them.Hp < hpW, "another player's Blood Wall bleeds you (" + nodes + " nodes, " + (hpW - them.Hp).ToString("0.0") + ")");
            tickIn(8, new PlayerInput { AimAt = new V2(900f, 300f) });  // (let go: the shove lands)
            Check(client.Game.Me.Pos.DistanceTo(onWall) > 8f, "...and pushes you off it (" + client.Game.Me.Pos.DistanceTo(onWall).ToString("0") + " px)");
            me.Pos = me.Disp = onWall;
            tickIn(40, new PlayerInput { AimAt = new V2(900f, 300f) });
            Check(me.Pos.DistanceTo(onWall) < 1f, "your own Blood Wall never pushes you (its health cost is the drain, as before)");
            typeof(Game).GetMethod("EndBloodWall", F).Invoke(g, new object[] { me });
            g.Zones.Clear();
            // Ossuary Wall: the host throws one across the client.
            me.Weapon = Defs.WeaponOf(WeaponId.BoneGuard); me.BoneWallCd = 0f;
            me.Pos = me.Disp = new V2(400f, 600f);
            V2 at = new V2(620f, 600f);
            typeof(Game).GetMethod("HostOssuaryWall", F).Invoke(g, new object[] { me, at });
            tickIn((int)((Game.BoneWallFlight + 0.2f) / dt), new PlayerInput { AimAt = at });
            them.Hp = them.MaxHp;
            them.Pos = them.Disp = client.Game.Me.Pos = client.Game.Me.Disp = at;
            float hpO = them.Hp;
            for (int i = 0; i < 90; i++) { them.InvulnT = 0f; me.Pos = me.Disp = new V2(400f, 600f); tickIn(1, new PlayerInput { AimAt = at }); }
            Check(g.Zones.Values.Any(z => z.Kind == ZoneKind.OssuaryWall) && them.Hp < hpO, "another player's Ossuary Wall darts you (" + (hpO - them.Hp).ToString("0.0") + ")");
            Check(client.Game.Me.Pos.DistanceTo(at) > 8f, "...and pushes you off it (" + client.Game.Me.Pos.DistanceTo(at).ToString("0") + " px)");
            g.Zones.Clear();
        }

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
