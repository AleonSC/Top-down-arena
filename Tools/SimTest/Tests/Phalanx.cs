using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Arena;

// Bone Phalanx (the Tank Summoner's second ultimate) and the lobby's Ultimate pick (LobbyEntry.Ult).
static partial class Program
{
    static void PhalanxTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        var cast = typeof(Game).GetMethod("HostAbility", flags);

        // --- The table ---
        var def = Upgrades.Ability(AbilityId.BonePhalanx);
        Check(def.Name == "Bone Phalanx" && def.Cooldown == 75f && def.Choice == AbilityDef.Ult && def.UltKey == 1 && def.Capstone != null,
            "Bone Phalanx is an ultimate, 75 s, on key 1 of the Ultimate row, with a capstone");
        Check(Upgrades.Ultimate(Role.Tank, Kit.Summoner) == AbilityId.Titan && Upgrades.Ultimate(Role.Tank, Kit.Summoner, 1) == AbilityId.BonePhalanx
            && Upgrades.AltUltimate(Role.Tank, Kit.Summoner, 2) == AbilityId.None, "the Tank Summoner's Ultimate row: Titan (key 0, the default), then Bone Phalanx, then locked keys");
        Check(Upgrades.AltUltimate(Role.Healer, Kit.Summoner, 1) == AbilityId.None && Upgrades.AltUltimate(Role.Dps, Kit.Summoner, 1) == AbilityId.None
            && Upgrades.AltUltimate(Role.Tank, Kit.Soldier, 1) != AbilityId.BonePhalanx, "it is only the Tank Summoner's");
        Check(Upgrades.Ultimate(Role.Wildcard, Kit.Summoner, 1) == Upgrades.Ultimate(Role.Dps, Kit.Summoner)
            && Upgrades.ClampUlt(Role.Tank, Kit.Summoner, 7) == 0 && Upgrades.ClampUlt(Role.Tank, Kit.Summoner, 1) == 1 && Upgrades.ClampUlt(Role.Healer, Kit.Summoner, 1) == 0,
            "a Wildcard ignores the pick; picks the class doesn't have fall back to 0");

        // --- The saved run keeps the pick ---
        var save = new RunSave { Wave = 4 };
        save.Seats.Add(new SeatSave { Name = "Bones", Role = Role.Tank, Kit = Kit.Summoner, Ult = 1 });
        save.Seats.Add(new SeatSave { Name = "Heals", Role = Role.Healer, Kit = Kit.Summoner, Ult = 0 });
        var back = RunSave.Parse(save.ToText());
        Check(back.Seats[0].Ult == 1 && back.Seats[1].Ult == 0, "a saved run keeps each seat's Ultimate pick");
        var old = RunSave.Parse(System.Text.RegularExpressions.Regex.Replace(save.ToText(), "class Tank Summoner 0 0 0 1", "class Tank Summoner 0 0 0"));
        Check(old.Seats[0].Ult == 0, "a save from before the pick reads as the default");

        // --- The lobby pick reaches the match (host and client) ---
        int port = nextPort++;
        var host = new Session { AutoStart = 0, StartWave = 3 };
        host.Host(port, "Bones", Role.Tank, 0, 0, Kit.Summoner, ult: 1);
        var client = new Session();
        client.Join("127.0.0.1", port, "Tank2", Role.Tank, 0, 0, Kit.Summoner, ult: 1);
        for (int i = 0; i < 300 && !(host.Lobby.Count == 2 && client.Lobby.Count == 2); i++) { Program.Step(host, dt); Program.Step(client, dt); Thread.Sleep(2); }
        Check(host.Lobby[1].Ult == 1 && host.Lobby[2].Ult == 1 && client.Lobby[1].Ult == 1, "the pick reaches the host's lobby and everyone's (from Host and from a joining player's hello)");
        client.RequestUlt(0);
        for (int i = 0; i < 60 && host.Lobby[2].Ult != 0; i++) { Program.Step(host, dt); Program.Step(client, dt); Thread.Sleep(2); }
        Check(host.Lobby[2].Ult == 0, "a client's Ultimate key reaches the host (Msg.UltRequest)");
        client.RequestUlt(1);
        for (int i = 0; i < 60 && client.Lobby[2].Ult != 1; i++) { Program.Step(host, dt); Program.Step(client, dt); Thread.Sleep(2); }
        Check(host.Lobby[2].Ult == 1 && client.Lobby[2].Ult == 1, "...and back to everyone");
        client.RequestKit(Kit.Ninja);  // (a class with no second Tank ultimate yet)
        for (int i = 0; i < 60 && client.Lobby[2].Kit != Kit.Ninja; i++) { Program.Step(host, dt); Program.Step(client, dt); Thread.Sleep(2); }
        Check(host.Lobby[2].Ult == 0, "changing class resets the pick");
        client.RequestUlt(1);
        for (int i = 0; i < 30; i++) { Program.Step(host, dt); Program.Step(client, dt); Thread.Sleep(2); }
        Check(host.Lobby[2].Ult == 0, "the host clamps a pick the class doesn't have");
        client.RequestKit(Kit.Summoner);
        for (int i = 0; i < 60 && client.Lobby[2].Kit != Kit.Summoner; i++) { Program.Step(host, dt); Program.Step(client, dt); Thread.Sleep(2); }
        client.RequestUlt(1);
        for (int i = 0; i < 60 && host.Lobby[2].Ult != 1; i++) { Program.Step(host, dt); Program.Step(client, dt); Thread.Sleep(2); }
        host.AutoStart = 2;  // (nobody pressed Ready)
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(new[] { host, client }, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Slots[Upgrades.UltSlot].Id == AbilityId.BonePhalanx && g.Players[2].Slots[Upgrades.UltSlot].Id == AbilityId.BonePhalanx
            && client.Game.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.BonePhalanx, "both players carry Bone Phalanx on F, on every game");
        var showcase = new Showcase();
        showcase.Show(Role.Tank, Kit.Summoner, 0, Upgrades.RoleAbility(Role.Tank, Kit.Summoner, 0), true, true, 1);
        Check(showcase.Game.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.BonePhalanx, "the lobby preview shows the picked ultimate");
        showcase.Stop();

        g.DevSetGod(1, true); g.DevSetGod(2, true);
        var keep = new HashSet<Enemy>();
        Action Clear = () => { foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f; };
        Func<EnemyType, V2, float, Enemy> Mob = (type, at, hp) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.SpawnT = 0f; e.Hp = e.MaxHp = hp; e.Pos = at; e.FireT = 99f; keep.Add(e); return e;
        };
        V2 home = new V2(400f, 450f), aimAt = new V2(900f, 450f);
        Action<float> Tick = secs =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                Thread.Sleep(1);
                me.Pos = home;
                g.Minions.Clear();  // (the staff's golem would hit what the skeletons spear)
                foreach (var e in keep) e.FireT = 99f;
                host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = aimAt }); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput { AimAt = new V2(400, 700) }); client.Game.Update(dt);
                Clear();
            }
        };
        Func<Zone> Line = () => g.Zones.Values.FirstOrDefault(q => q.Kind == ZoneKind.Phalanx);
        Action Reset = () =>
        {
            foreach (var e in keep) e.Hp = 0f;
            keep.Clear();
            Mob(EnemyType.Grunt, new V2(1500f, 120f), 1e6f).FrozenT = 999f;  // an anchor far off: the wave must not end (that crumbles the line)
            Clear();
            foreach (var b in g.Bullets.Values.ToList()) g.Bullets.Remove(b.Id);
            g.Zones.Clear();
            me.Pos = home;
            g.Players[2].Pos = new V2(200f, 800f);
            Tick(0.1f);
        };
        Action Cast = () =>
        {
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
            cast.Invoke(g, new object[] { 1, Upgrades.UltSlot, aimAt, true });
        };
        Func<V2, V2, byte, int, Bullet> Shot = (at, vel, kind, owner) => (Bullet)shoot.Invoke(g, new object[] { kind, at, vel, 0f, 5f, 10f, owner, false, 0f });
        int n = Game.PhalanxCount;
        float marchAt = Game.PhalanxMarchAt(n), face0 = home.X + Game.PhalanxAhead;
        Clear(); Reset();

        // --- The march: 260 px over 6 s, the heading kept when you turn, everyone sees it ---
        Cast();
        Tick(0.3f);
        var z = Line();
        Check(z != null && z.Bones.Length == n && Math.Abs(z.Pos.X - face0) < 1f && Math.Abs(z.Pos.Y - 450f) < 1f, "the gate opens 60 px ahead, across the aim, with 8 skeletons");
        Tick(marchAt - 0.3f + 1f);
        aimAt = new V2(400f, 100f);  // turn away: the line keeps its heading
        Tick(5.2f);
        float marched = z.Marched;
        Console.WriteLine("  phalanx: marched {0:0.0} px, face at ({1:0},{2:0})", marched, z.Pos.X, z.Pos.Y);
        Check(Math.Abs(marched - Game.PhalanxMarch) < 2f && Math.Abs(z.Pos.Y - 450f) < 1f, "it marches 260 px along the aim it was cast with, even after you turn");
        var cz = client.Game.Zones.Values.FirstOrDefault(q => q.Kind == ZoneKind.Phalanx);
        Check(cz != null && cz.Bones != null && cz.Bones.Length == n && Math.Abs(cz.Marched - marched) < 12f, "clients get the line: where it is, how far it walked, each skeleton's health");
        Tick(Game.PhalanxHold + Game.PhalanxCrumble + 0.3f);
        Check(Line() == null, "it holds, crumbles and is gone on time");
        aimAt = new V2(900f, 450f);

        // --- Shots: from the front stopped (red aimed shots too), the team's and those from behind pass ---
        Reset();
        Cast();
        Tick(marchAt + 0.1f);
        z = Line();
        float face = z.Pos.X;
        var front = Shot(new V2(face + 120f, 450f), new V2(-300f, 0f), Defs.SmallShot, 0);
        var red = Shot(new V2(face + 120f, 425f), new V2(-300f, 0f), Defs.PierceShot, 0);
        var team = Shot(new V2(face - 60f, 440f), new V2(500f, 0f), Defs.TankShot, 1);
        var behind = Shot(new V2(face - 60f, 475f), new V2(300f, 0f), Defs.SmallShot, 0);
        Tick(0.6f);
        Check(!g.Bullets.ContainsKey(front.Id) && front.Pos.X > face - 10f, "a shot from the front stops at the shields");
        Check(!g.Bullets.ContainsKey(red.Id) && red.Pos.X > face - 10f, "a red aimed shot stops there too");
        Check(g.Bullets.ContainsKey(team.Id) && team.Pos.X > z.Pos.X + 40f, "your team's shots pass out through the line");
        Console.WriteLine("  phalanx: shot from behind at x {0:0} (face {1:0}), still flying {2}", behind.Pos.X, z.Pos.X, g.Bullets.ContainsKey(behind.Id));
        Check(behind.Pos.X > z.Pos.X + 20f, "an enemy shot from behind passes");
        Check(z.Bones.Count(b => b < 255) >= 2, "the skeletons that blocked lost health");

        // --- Skeleton health, the gap ---
        int lane = 2;
        float laneY = 450f + Game.PhalanxLane(lane, n);
        int shots = 0;
        while (z.Bones[lane] > 0 && shots < 200)
        {
            var b = Shot(new V2(z.Pos.X + 30f, laneY), new V2(-400f, 0f), Defs.SmallShot, 0);
            b.Dmg = 20f;
            Tick(0.15f);
            shots++;
        }
        float perShot = 20f * g.EnemyDamageScale(g.Wave);
        Console.WriteLine("  phalanx: skeleton {0} broke after {1} shots of {2:0.0}", lane, shots, perShot);
        Check(z.Bones[lane] == 0 && Math.Abs(shots - Game.PhalanxHp / perShot) <= 1.5f, "a skeleton has 120 health and breaks when the shots it blocks take it");
        Check(g.Effects.Any(e => e.Kind == FxKind.PhalanxBreak) || client.Game.Effects.Any(e => e.Kind == FxKind.PhalanxBreak), "it falls apart into a pile of bones");
        var gap = Shot(new V2(z.Pos.X + 60f, laneY), new V2(-300f, 0f), Defs.SmallShot, 0);
        var beside = Shot(new V2(z.Pos.X + 60f, laneY + Game.PhalanxSpacing), new V2(-300f, 0f), Defs.SmallShot, 0);
        Tick(0.4f);
        Check(gap.Pos.X < z.Pos.X - 30f && !g.Bullets.ContainsKey(beside.Id) && beside.Pos.X > z.Pos.X - 10f, "shots get through the gap, not beside it");
        var walker = Mob(EnemyType.Grunt, new V2(z.Pos.X + 4f, laneY), 1e6f);
        Tick(0.05f);
        Check(walker.Pos.X < z.Pos.X + Game.PhalanxFace + walker.Radius - 2f, "an enemy in the gap isn't held back");

        // --- Shoving and spearing; a Heavy enemy is shoved slower ---
        Reset();
        Cast();
        Tick(marchAt);
        z = Line();
        var g1 = Mob(EnemyType.Grunt, new V2(z.Pos.X + 12f, 450f + Game.PhalanxLane(4, n)), 1e6f);
        float x0 = z.Marched;
        g1.FrozenT = 99f;  // (held to its lane: walking onto the line between two shields, both would spear it)
        Tick(2f);
        float freePace = (z.Marched - x0) / 2f;
        Console.WriteLine("  phalanx: shoving a grunt {0:0.0} px/s, grunt took {1:0}", freePace, 1e6f - g1.Hp);
        Check(g1.Pos.X >= z.Pos.X + Game.PhalanxFace + g1.Radius - 1f && g1.Pos.X > home.X + Game.PhalanxAhead + 40f, "an enemy in front is shoved along ahead of the line");
        float spears = (1e6f - g1.Hp) / (Game.PhalanxSpear * z.DmgMult);
        Check(spears >= 3f && spears <= 5f, "it is speared for 15 (times your damage) about every 0.5 s");
        Check(Math.Abs(freePace - Game.PhalanxMarch / Game.PhalanxMarchTime) < 2f, "a light enemy doesn't slow the march");
        var jug = Mob(EnemyType.Juggernaut, new V2(z.Pos.X + 30f, 450f), 1e6f);
        Tick(0.4f);
        x0 = z.Marched;
        Tick(1f);
        float heavyPace = z.Marched - x0;
        Console.WriteLine("  phalanx: shoving a Juggernaut {0:0.0} px/s", heavyPace);
        Check(Math.Abs(heavyPace - Game.PhalanxMarch / Game.PhalanxMarchTime * Game.PhalanxHeavyPace) < 2.5f && jug.Pos.X > z.Pos.X + Game.PhalanxFace + jug.Radius - 1f,
            "a Heavy enemy is shoved more slowly");

        // --- A boss stops the line ---
        Reset();
        Cast();
        Tick(marchAt);
        z = Line();
        var boss = Mob(EnemyType.Boss, new V2(z.Pos.X + 70f, 450f), 1e6f);
        boss.FrozenT = 99f;  // (held in place: the line walks up to it)
        V2 bossAt = boss.Pos;
        Tick(2.5f);
        x0 = z.Marched;
        Tick(1f);
        Console.WriteLine("  phalanx: against a boss, marched {0:0} then {1:0}", x0, z.Marched);
        Check(z.Marched - x0 < 0.5f && x0 < 120f && boss.Pos.DistanceTo(bossAt) < 30f, "a boss isn't shoved, and the line stops at it");
        boss.Hp = 0f;

        // --- Walls stop it ---
        Reset();
        home = new V2(g.ArenaMax.X - 200f, 450f);
        aimAt = home + new V2(300f, 0f);
        Tick(0.05f);
        Cast();
        Tick(marchAt + 6f);
        z = Line();
        Console.WriteLine("  phalanx: near the wall, marched {0:0}, face at {1:0} (arena edge {2:0})", z.Marched, z.Pos.X, g.ArenaMax.X);
        Check(z.Marched < Game.PhalanxMarch - 50f && z.Pos.X + Game.PhalanxFace < g.ArenaMax.X, "the line stops at a wall");
        home = new V2(400f, 450f);
        aimAt = new V2(900f, 450f);

        // --- An enemy pinned against a wall stops it too ---
        Reset();
        home = new V2(g.ArenaMax.X - 330f, 450f);  // (with nothing in the way the march would end at the wall)
        aimAt = home + new V2(300f, 0f);
        Tick(0.05f);
        Cast();
        Tick(marchAt);
        z = Line();
        var pinned = Mob(EnemyType.Grunt, new V2(g.ArenaMax.X - 20f, 450f + Game.PhalanxLane(4, n)), 1e6f);
        pinned.FrozenT = 99f;
        pinned.Radius = 30f;  // (a big one, so the stop is well short of where the wall alone would stop it)
        pinned.Pos = new V2(g.ArenaMax.X - pinned.Radius, pinned.Pos.Y);
        Tick(6f);
        Console.WriteLine("  phalanx: an enemy against the wall, marched {0:0}, enemy at {1:0}", z.Marched, pinned.Pos.X);
        Check(z.Pos.X < g.ArenaMax.X - 2f * pinned.Radius - Game.PhalanxFace + 2f && pinned.Pos.X > z.Pos.X + Game.PhalanxFace && pinned.Pos.X <= g.ArenaMax.X - pinned.Radius + 0.5f, "an enemy pinned against a wall stops the line (it isn't pushed through)");
        home = new V2(400f, 450f);
        aimAt = new V2(900f, 450f);

        // --- It ends when you go down, and between waves ---
        Reset();
        Cast();
        Tick(marchAt + 1f);
        g.DevSetGod(1, false);
        me.Hp = 0f; me.Downed = true;
        Tick(0.1f);
        z = Line();
        Check(z == null || Game.PhalanxCrumbling(z), "it crumbles when you go down");
        Tick(Game.PhalanxCrumble + 0.2f);
        Check(Line() == null, "...and is gone");
        me.Downed = false; me.Hp = me.MaxHp; g.DevSetGod(1, true);
        Reset();
        Cast();
        Tick(marchAt + 1f);
        Check(Line() != null, "(a new line stands)");
        keep.Clear();
        foreach (var e in g.Enemies.Values) e.Hp = 0f;
        for (int i = 0; i < 600 && g.State == MatchState.Fighting; i++) { Tick(1f / 60f); foreach (var e in g.Enemies.Values) e.Hp = 0f; }
        z = Line();
        Check(g.State != MatchState.Fighting && (z == null || Game.PhalanxCrumbling(z)), "it crumbles when the wave ends");

        // --- Mastered: 10 skeletons with 180 health ---
        me.MasteryXp[AbilityId.BonePhalanx] = 99999f;
        g.Zones.Clear();
        Cast();
        z = Line();
        Check(z != null && z.Bones.Length == Game.PhalanxCapCount && Math.Abs(z.BoneMax - Game.PhalanxCapHp * Game.MasteryPower(me, AbilityId.BonePhalanx)) < 1f,
            "mastered: 10 skeletons with 180 health each");

        // --- The bot casts it at the biggest group in front ---
        g.Zones.Clear();
        keep.Clear();
        foreach (var e in g.Enemies.Values) e.Hp = 0f;
        Tick(0.05f);
        var grp = new List<Enemy>();
        for (int i = 0; i < 5; i++) grp.Add(Mob(EnemyType.Grunt, home + V2.FromAngle(-0.6f) * (220f + i * 25f) + new V2(0f, i * 6f), 1e6f));
        var lone = Mob(EnemyType.Grunt, home + V2.FromAngle(0.4f) * 150f, 1e6f);
        me.Aim = 0f;
        var aim = (V2)typeof(Game).GetMethod("PhalanxBotAim", flags).Invoke(g, new object[] { me, lone });
        float dir = (aim - me.Pos).Angle;
        Console.WriteLine("  phalanx: bot aims at {0:0.00} rad (the group is at -0.6)", dir);
        Check(Math.Abs(dir + 0.6f) < 0.25f, "a bot aims it at the biggest group in front, not the nearest enemy");

        host.Leave(""); client.Leave("");
    }
}
