using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Boss approaches (Approaches.cs) and the Warden's faction (WardenFaction.cs): the themed share, waves built with it,
// the Riot Shield (its shield, its bash and knockback on the friend's own game, the Warden's pair in the searchlight
// turn) and the Gas Grenadier's tear gas with no Warden about.
static partial class Program
{
    static readonly System.Reflection.BindingFlags NP = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    /// A host and a friend (no bots), the match started at `wave`, with the difficulty given.
    static Session[] StartPlain(float dt, int wave, Action<Session> setup = null)
    {
        int port = nextPort++;
        var host = HostAs(new Session { StartWave = wave % 5 == 0 ? -wave : wave }, port, false, "Host", Role.Tank);  // (a multiple of 5 starts at its boss)
        var client = JoinAs(port, false, "Friend", Role.Dps);
        Action Sync = () => PollBoth(host, client);
        WaitForLobby(host, client);
        Sync();
        setup?.Invoke(host);
        Sync();
        client.RequestReady(true); host.RequestReady(true); Sync();
        host.StartMatch();
        WaitForGame(host, client, dt);
        return new[] { host, client };
    }

    static void ApproachesTest(float dt)
    {
        // --- The share, wave by wave --------------------------------------------------------------------------------
        var g0 = new Session().Game;
        g0.Diff.Set(DiffKnob.Campaign, 0f);  // (these checks are the Boss approach setting; the campaign buildup has its own test below)
        Func<int, float> share = w => g0.ApproachShare(w);
        bool normal = Math.Abs(share(1) - 0.34f) < 1e-4f && Math.Abs(share(2) - 0.51f) < 1e-4f && Math.Abs(share(3) - 0.68f) < 1e-4f && Math.Abs(share(4) - 0.85f) < 1e-4f && Math.Abs(share(5) - 0.85f) < 1e-4f;
        g0.Wave = 5; g0.BossSlot = true;  // the boss after wave 5 has no approach
        normal &= share(5) == 0f;
        g0.Wave = 0; g0.BossSlot = false;
        Console.WriteLine("  Blend (Normal): waves 1-5 = {0}", string.Join(", ", Enumerable.Range(1, 5).Select(w => share(w).ToString("0.00"))));
        Check(normal, "Blend (the default): the Warden's approach builds up 34%, 51%, 68%, 85%, 85%; nothing on his boss");
        Check(share(6) == 0f && share(9) == 0f && BossSchedule.Campaign.BossAt(BossSchedule.Campaign.NextBossWaveFrom(9)) == EnemyType.HiveQueen && BossSchedule.Campaign.BossAt(BossSchedule.Campaign.NextBossWaveFrom(51)) == EnemyType.Boss,
            "a boss with no faction yet has no approach; the waves after 50 lead to the bosses again");
        g0.Diff.Set(DiffKnob.ApproachShare, 1f);
        g0.Diff.Set(DiffKnob.ApproachRamp, 0f);
        Check(Enumerable.Range(1, 5).All(w => share(w) == 1f) && share(56) == 0f && share(51) == 1f, "Fully themed (100%, no ramp): every approach wave is all faction");
        g0.Diff.Set(DiffKnob.ApproachShare, 0f);
        Check(Enumerable.Range(1, 60).All(w => share(w) == 0f), "0%: no approach at all");
        var hard = Difficulty.Of(DifficultyLevel.Hard);
        hard.Set(DiffKnob.ApproachShare, 1f);
        hard.Set(DiffKnob.ApproachRamp, 0f);
        var back = Difficulty.Parse(hard.ToText());
        Console.WriteLine("  Hard, fully themed, as text: \"" + hard.ToText() + "\"");
        Check(hard.Level == DifficultyLevel.Hard && back.Level == DifficultyLevel.Hard && back[DiffKnob.ApproachShare] == 1f && back[DiffKnob.ApproachRamp] == 0f
            && Difficulty.Of(DifficultyLevel.Nightmare)[DiffKnob.ApproachShare] == 0.85f && Difficulty.Knobs[(int)DiffKnob.ApproachRamp].Format(1f) == "On",
            "the approach settings don't make a preset Custom, and save and read back with the difficulty");
        hard.SetLevel(DifficultyLevel.Normal);
        Check(hard[DiffKnob.ApproachShare] == 1f && Difficulty.Of(DifficultyLevel.Normal).ToText() == "Normal", "picking another preset keeps the approach setting");

        // --- Waves built with it (host only, no fighting) --------------------------------------------------------------
        var faction = new HashSet<EnemyType>(Approaches.For(EnemyType.Boss).Members.Select(m => m.Type));
        var build = typeof(Game).GetMethod("BuildWave", NP);
        var queueF = typeof(Game).GetField("spawnQueue", NP);
        var squadsF = typeof(Game).GetField("squadQueue", NP);
        Func<Game, int, List<EnemyType>> wave = (gd, n) =>
        {
            Game.SeedForTests(4242 + n);
            var g = new Session().Game;  // a fresh game, so its rolls start from the same seed
            g.Diff = gd.Diff.Copy();
            build.Invoke(g, new object[] { n });
            var all = new List<EnemyType>((List<EnemyType>)queueF.GetValue(g));
            foreach (var s in (List<SquadDef>)squadsF.GetValue(g))
                all.AddRange(s.Members);
            return all;
        };
        var gb = new Session().Game;
        gb.Diff.Set(DiffKnob.Campaign, 0f);
        gb.Diff.Set(DiffKnob.ApproachShare, 0f);
        var off6 = wave(gb, 6);
        var off2 = wave(gb, 2);
        gb.Diff.Set(DiffKnob.ApproachShare, 0.6f);
        gb.Diff.Set(DiffKnob.ApproachRamp, 1f);
        var on6 = wave(gb, 6);
        var w4 = wave(gb, 4);
        var w5 = wave(gb, 5);  // the last wave before the Warden
        int themed4 = w4.Count(t => faction.Contains(t) && t != EnemyType.Rioter) + w4.Count(t => t == EnemyType.Rioter);
        Console.WriteLine("  wave 4 at Blend: {0} enemies, {1} of the Warden's ({2})", w4.Count, themed4,
            string.Join(" ", w4.Where(faction.Contains).GroupBy(t => t).Select(gr => gr.Key + "x" + gr.Count())));
        Check(off6.SequenceEqual(on6) && !off2.Any(faction.Contains), "with no approach the waves are built as before (same roll, same wave)");
        Check(themed4 >= w4.Count * 0.45f && themed4 <= w4.Count * 0.75f, "Blend: about 60% of the last wave before the Warden is his faction");
        gb.Diff.Set(DiffKnob.ApproachShare, 1f);
        gb.Diff.Set(DiffKnob.ApproachRamp, 0f);
        var full1 = wave(gb, 1);
        Console.WriteLine("  wave 1 fully themed: " + string.Join(" ", full1.GroupBy(t => t).Select(gr => gr.Key + "x" + gr.Count())));
        Check(full1.Count > 0 && full1.All(faction.Contains) && !full1.Contains(EnemyType.RiotShield) && !full1.Contains(EnemyType.GasGrenadier),
            "Fully themed: wave 1 is all prisoners and guards (Riot Shields and Grenadiers join from wave 2)");

        // --- XP: a themed wave pays like a regular one for its health (Approaches.cs KillXp) -----------------------------
        Func<List<EnemyType>, float> xpPerHp = l => l.Sum(t => (float)Defs.Enemies[(int)t].Xp) / l.Sum(t => Defs.Enemies[(int)t].Hp);
        gb.Diff.Set(DiffKnob.ApproachShare, 0f);
        float plainRate = Enumerable.Range(1, 4).Average(n => xpPerHp(wave(gb, n)));
        gb.Diff.Set(DiffKnob.ApproachShare, 1f);
        float themedRate = Enumerable.Range(1, 4).Average(n => xpPerHp(wave(gb, n)));
        Console.WriteLine("  XP per health, waves 1-4: regular {0:0.000}, fully themed {1:0.000}", plainRate, themedRate);
        Check(themedRate >= plainRate * 0.8f, "the Warden's approach waves give about as much XP for their health as regular waves");
        var killXp = typeof(Game).GetMethod("KillXp", NP);
        var gx = new Session().Game;
        var rioter = new Enemy { Type = EnemyType.Rioter, Def = Defs.Enemies[(int)EnemyType.Rioter] };
        gx.Wave = 3; gx.BossSlot = false;
        float approachXp = (float)killXp.Invoke(gx, new object[] { rioter });
        gx.Wave = 5; gx.BossSlot = true;
        float riotXp = (float)killXp.Invoke(gx, new object[] { rioter });
        Check(approachXp == Defs.Enemies[(int)EnemyType.Rioter].Xp && riotXp == Game.RioterFodderXp && riotXp < approachXp,
            "a Rioter pays full XP in the approach, but the Warden's own riot keeps its fodder XP");

        // --- The Lich's approach: zombies, ramping with the usual mix ------------------------------------------------------
        var dead = new HashSet<EnemyType>(Approaches.For(EnemyType.Lich).Members.Select(m => m.Type));
        gb.Diff.Set(DiffKnob.ApproachShare, 0.85f);
        gb.Diff.Set(DiffKnob.ApproachRamp, 1f);
        Func<int, int> zombies = n => wave(gb, n).Count(Zombies.Is);
        int z31 = zombies(31), z35 = zombies(35);
        Console.WriteLine("  Lich approach at Blend: zombies in waves 30-36 = {0}", string.Join(", ", Enumerable.Range(30, 7).Select(zombies)));
        Check(BossSchedule.Campaign.BossAt(BossSchedule.Campaign.NextBossWaveFrom(32)) == EnemyType.Lich && zombies(30) == 0 && zombies(36) == 0 && z31 > 0 && z35 > z31,
            "the Lich's approach (waves 31-35) brings zombies, more each wave; none before it or after his boss");
        var w35 = wave(gb, 35);
        Check(w35.Count(t => t == EnemyType.Mender) >= 2 && w35.Any(t => !Zombies.Is(t)), "wave 35 has the Dead march and still has the usual mix");
        gb.Diff.Set(DiffKnob.ApproachShare, 1f);
        gb.Diff.Set(DiffKnob.ApproachRamp, 0f);
        var full31 = wave(gb, 31);
        Check(full31.Count > 0 && full31.All(dead.Contains) && full31.All(t => t == EnemyType.Shambler), "Fully themed: wave 31 is all Shamblers (the other kinds join from wave 32)");

        // --- The Riot Shield --------------------------------------------------------------------------------------------
        var s2 = StartPlain(dt, 2);
        var host = s2[0]; var client = s2[1];
        var g2 = host.Game; var c2 = client.Game;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", NP);
        var hurt = typeof(Game).GetMethod("HurtEnemy", NP);
        var blocks = typeof(Game).GetMethod("BlocksShot", NP);
        var wear = typeof(Game).GetMethod("WearShield", NP);
        Run(s2, dt, 6f, () => g2.State == MatchState.Fighting);
        Action quiet = () =>
        {
            ((List<EnemyType>)queueF.GetValue(g2)).Clear(); ClearSquads(g2);
            foreach (var e in g2.Enemies.Values.ToList()) if (e.ParentId != -7 && e.Hp > 0f) g2.Enemies.Remove(e.Id);
            foreach (var b in g2.Bullets.Values) b.Life = 0f;
            foreach (var pt in g2.Patches.Values.ToList()) g2.Patches.Remove(pt.Id);
        };
        // A frozen dummy in a corner keeps the wave going (spawned first, so the wave never empties).
        var anchor = (Enemy)spawn.Invoke(g2, new object[] { EnemyType.Grunt, new V2(60f, 60f), 0f, -7, false });
        anchor.MaxHp = anchor.Hp = 1e6f;
        Action hold = () => { anchor.FrozenT = 1f; anchor.FireT = 1e6f; };
        Run(s2, dt, 0.3f, null, () => { quiet(); hold(); });
        Run(s2, dt, 0.2f, null, () => { quiet(); hold(); });

        var shield = (Enemy)spawn.Invoke(g2, new object[] { EnemyType.RiotShield, new V2(400f, 450f), 0f, -7, false });
        Check(Math.Abs(shield.ShieldMax - shield.MaxHp * Game.RiotShieldShare) < 0.01f, "a Riot Shield's shield holds 4x its health");
        shield.Facing = 0f;
        Check((bool)blocks.Invoke(g2, new object[] { shield, new V2(600f, 450f) }) && !(bool)blocks.Invoke(g2, new object[] { shield, new V2(200f, 450f) }),
            "its shield stops shots from the front, not from behind");

        // A Katana dash-cut that runs past the shield cuts it from behind; one that stops short is blocked and wears it.
        {
            var newSwing = typeof(Game).GetMethod("NewSwing", NP);
            var land = typeof(Game).GetMethod("LandSwing", NP);
            var kHost = g2.Players[1];
            Func<float, float, Tuple<float, float>> dash = (reach, startX) =>
            {
                shield.Pos = new V2(400f, 450f); shield.Facing = 0f; shield.Hp = shield.MaxHp; shield.ShieldHp = shield.ShieldMax;
                var sw = (Swing)newSwing.Invoke(g2, new object[] { kHost, 3.14159265f, reach, 20f, 22f });  // dashing left, at the shield's face (it faces +x)
                sw.Reach = reach; sw.HalfWidth = 32f; sw.FromSet = true; sw.From = new V2(startX, 450f); sw.KatanaMove = 1; sw.NoFx = true; sw.Silent = true;
                land.Invoke(g2, new object[] { kHost, sw });
                return Tuple.Create(shield.Hp, shield.ShieldHp);
            };
            var through = dash(120f, 440f);  // from x=440 to x=320: passes its centre
            var short1 = dash(30f, 440f);    // x=440 to x=410: stops in front of it
            Check(through.Item1 < shield.MaxHp && through.Item2 == shield.ShieldMax, "a Katana dash through a Riot Shield cuts it from behind (the shield doesn't stop it)");
            Check(short1.Item1 == shield.MaxHp && short1.Item2 < shield.ShieldMax, "a dash that stops in front of the shield is blocked and wears it");
            shield.Pos = new V2(400f, 450f); shield.Facing = 0f; shield.Hp = shield.MaxHp; shield.ShieldHp = shield.ShieldMax;
        }

        // The bash: the friend stands in front of it; their own game moves them back about 160 px.
        var friendH = g2.Players[2];
        var friendC = c2.Me;
        V2 stand = new V2(400f + shield.Radius + friendH.Def.BodyR + 20f, 450f);
        friendC.Pos = friendC.Disp = stand;
        Run(s2, dt, 0.3f, null, () => { hold(); friendC.Pos = stand; shield.TargetId = 2; shield.Pos = new V2(400f, 450f); shield.Facing = 0f; });
        float hp0 = friendH.Hp;
        bool wound = false, lunged = false;
        V2 before = friendC.Pos;
        Run(s2, dt, 1.2f, null, () =>
        {
            hold();
            shield.TargetId = 2;
            wound |= Game.RiotState(shield) == Game.RiotWinding;
            lunged |= Game.RiotState(shield) == Game.RiotLunging;
        });
        float moved = friendC.Pos.X - before.X;
        Console.WriteLine("  bash: wound up {0}, lunged {1}; the friend took {2:0.#} and was knocked {3:0} px on their own game", wound, lunged, hp0 - friendH.Hp, moved);
        Check(wound && lunged && hp0 - friendH.Hp > 0f, "a Riot Shield winds up (its shield flashes), lunges and bashes the player in front of it");
        Check(moved > Game.RiotKnock * 0.75f && moved < Game.RiotKnock * 1.35f, "the bash knocks the friend about 160 px back, on their own game");
        Check(Game.RiotState(shield) == Game.RiotLow || Game.RiotState(shield) == Game.RiotWalk, "after a bash it holds its shield low for a moment");

        // Two bashes in a row can't land: the grace.
        friendH.BashGraceT = 0.5f;
        var bash = typeof(Game).GetMethod("RiotBash", NP);
        friendC.Pos = stand;
        Run(s2, dt, 0.15f, null, () => { hold(); friendC.Pos = stand; });
        float hp1 = friendH.Hp;
        shield.Pos = new V2(400f, 450f); shield.Facing = 0f;
        bash.Invoke(g2, new object[] { shield });
        Check(friendH.Hp == hp1, "a player just bashed can't be bashed again straight away");

        // Breaking it: the shield wears, cracks, shatters; the guard staggers, then flails and takes more damage.
        wear.Invoke(g2, new object[] { shield, shield.ShieldMax * 0.6f });
        byte crack = Game.RiotCrack(shield);
        wear.Invoke(g2, new object[] { shield, shield.ShieldMax });
        Check(crack == 2 && Game.RiotShieldBroken(shield) && Game.RiotState(shield) == Game.RiotStaggered && !(bool)blocks.Invoke(g2, new object[] { shield, new V2(600f, 450f) }),
            "the shield cracks as it wears, then shatters; the guard staggers and blocks nothing");
        Run(s2, dt, Game.RiotStagger + 0.2f, null, hold);
        float h0 = shield.Hp;
        hurt.Invoke(g2, new object[] { shield, 10f, 1 });
        Check(Game.RiotState(shield) == Game.RiotFlail && Math.Abs(h0 - shield.Hp - 10f * Game.RiotBrokenMult) < 0.01f, "broken, it flails with its baton and takes 50% more damage");
        Run(s2, dt, 0.2f, null, () => { hold(); });
        Check(c2.Enemies.ContainsKey(shield.Id) && Game.RiotShieldBroken(c2.Enemies[shield.Id]), "the friend's game sees the shield broken");
        shield.Hp = 0f;

        // --- The Gas Grenadier: tear gas hurts with no Warden about --------------------------------------------------------
        var gren = (Enemy)spawn.Invoke(g2, new object[] { EnemyType.GasGrenadier, new V2(900f, 450f), 0f, -7, false });
        var hostMe = g2.Players[1];
        V2 spot = new V2(600f, 300f);
        g2.Me.Pos = g2.Me.Disp = spot;
        gren.FireT = 0f;
        gren.TargetId = 1;  // at you from its first throw (it fires on the first step, before the hold below runs)
        gren.RetargetT = 99f;
        Run(s2, dt, 0.1f, null, () => { hold(); g2.Me.Pos = spot; gren.TargetId = 1; });
        int gas = g2.Patches.Values.Count(pt => pt.Kind == PatchKind.Gas);
        float hpg = hostMe.Hp;
        Run(s2, dt, 2.5f, null, () => { hold(); g2.Me.Pos = spot; gren.FireT = 99f; });
        Console.WriteLine("  grenadier: {0} gas cloud(s); standing in it for 1.5 s took {1:0.#}", gas, hpg - hostMe.Hp);
        Check(gas == 1 && hostMe.Hp < hpg && c2.Patches.Values.Any(pt => pt.Kind == PatchKind.Gas), "a Gas Grenadier lobs tear gas near you, and it hurts with no Warden about (the friend sees it too)");
        // --- The Gas Grenadier's overhand lob: it winds up first, the ring shows the spot, the cloud lands there --------------
        var lobber = (Enemy)spawn.Invoke(g2, new object[] { EnemyType.GasGrenadier, new V2(900f, 450f), 0f, -7, false });
        lobber.FireT = 2f;
        lobber.TargetId = 1;
        lobber.RetargetT = 99f;
        int patchesBefore = g2.Patches.Count;
        float windAt = -1f, patchAt = -1f, lt = 0f, ringDur = 0f;
        V2 ringAt = V2.Zero, patchPos = V2.Zero, plantedFrom = V2.Zero;
        bool clientRing = false, movedWinding = false;
        Run(s2, dt, 3.5f, null, () =>
        {
            hold(); g2.Me.Pos = spot; lobber.TargetId = 1; lobber.RetargetT = 99f;
            lt += dt;
            if (lobber.Winding && windAt < 0f) { windAt = lt; plantedFrom = lobber.Pos; }
            if (lobber.Winding && windAt >= 0f && lobber.Pos.DistanceTo(plantedFrom) > 1f) movedWinding = true;
            if (ringDur == 0f)
                foreach (var fx in g2.Effects)
                    if (fx.Kind == FxKind.GasLob && fx.Pos2.Y > 0f) { ringDur = fx.Dur; ringAt = fx.Pos; }
            clientRing |= c2.Effects.Any(fx => fx.Kind == FxKind.GasLob);
            if (patchAt < 0f && g2.Patches.Count > patchesBefore)
            {
                patchAt = lt;
                patchPos = g2.Patches.Values.Last().Pos;
                lobber.FireT = 99f;  // one lob is enough
            }
        });
        Console.WriteLine("  grenadier lob: wind-up at {0:0.00} s, ring {1:0.00} s long, cloud queued at {2:0.00} s, ring/cloud {3:0.#} px apart", windAt, ringDur, patchAt, ringAt.DistanceTo(patchPos));
        Check(windAt >= 0f && patchAt - windAt > 0.8f && patchAt - windAt < 1.4f, "a Gas Grenadier winds up for about 1 s before the throw (Enemy.Winding)");
        Check(ringDur > 1.5f && ringAt.DistanceTo(patchPos) < 1f, "the ring is shown from the start of the wind-up and the cloud lands on that spot");
        Check(clientRing, "the friend's game gets the lob effect too");
        Check(!movedWinding, "it plants its feet while it winds up");
        lobber.Hp = 0f;
        gren.Hp = 0f;

        // --- The Taser Guard: it charges (Winding), then a Fork Bolt that stuns you; the friend's game sees the bolt -------
        foreach (var e in g2.Enemies.Values) e.Hp = 0f;
        Run(s2, dt, 0.3f, null, () => { hold(); g2.Me.Pos = spot; });
        var taser = (Enemy)spawn.Invoke(g2, new object[] { EnemyType.TaserGuard, new V2(900f, 300f), 0f, -7, false });
        taser.FireT = 1.2f;
        taser.TargetId = 1;
        taser.RetargetT = 99f;
        float hpT = hostMe.Hp, stunMax = 0f;
        bool tWound = false, seenBolt = false, clientBolt = false;
        float firstWind = -1f, boltAt = -1f, t0 = 0f;
        Run(s2, dt, 9f, null, () =>
        {
            hold(); g2.Me.Pos = spot; taser.TargetId = 1; taser.RetargetT = 99f;
            t0 += dt;
            if (taser.Winding && !tWound) { tWound = true; firstWind = t0; }
            if (!seenBolt && g2.Bullets.Values.Any(b => b.Kind == Defs.TaserBolt)) { seenBolt = true; boltAt = t0; }
            clientBolt |= c2.Bullets.Values.Any(b => b.Kind == Defs.TaserBolt);
            stunMax = Math.Max(stunMax, hostMe.StunT);
            if (stunMax > 0f) taser.FireT = 99f;  // one bolt is enough
        });
        Console.WriteLine("  taser guard: charged {0:0.00} s before the bolt, bolt seen {1} (friend {2}), stun {3:0.0} s, health {4:0.#} -> {5:0.#}", boltAt - firstWind, seenBolt, clientBolt, stunMax, hpT, hostMe.Hp);
        Check(tWound && boltAt - firstWind > 0.5f && boltAt - firstWind < 0.9f, "a Taser Guard glows for its 0.7 s charge before it fires");
        Check(seenBolt && clientBolt, "a Taser Guard fires a Fork Bolt, and the friend's game sees it");
        Check(stunMax > 0.9f && hostMe.Hp < hpT, "a Fork Bolt that lands stuns you for a second and hurts a little");
        Check(Approaches.For(EnemyType.Boss).Members.Any(m => m.Type == EnemyType.TaserGuard && m.From >= 2), "the Warden's faction includes the Taser Guard, from its third wave");
        taser.Hp = 0f;

        // --- The Security Guard: a magazine of seven tracer rounds that lead you, then a reload; the friend sees it all -----------
        foreach (var e in g2.Enemies.Values) e.Hp = 0f;
        Run(s2, dt, 0.3f, null, () => { hold(); g2.Me.Pos = spot; });
        var sec = (Enemy)spawn.Invoke(g2, new object[] { EnemyType.SecurityGuard, new V2(900f, 300f), 0f, -7, false });
        sec.FireT = 0.6f;
        sec.TargetId = 1;
        sec.RetargetT = 99f;
        Check(sec.Phase == Game.SecurityMag, "a Security Guard starts with a full magazine");
        float hpS = hostMe.Hp, tS = 0f, firstWindS = -1f, firstShotS = -1f, lastShotS = -1f, reloadStart = -1f, reloadEnd = -1f;
        int shotsS = 0;
        bool clientSaw = false, clientPhase = false, leads = false, damaged = false;
        var seenBullets = new HashSet<int>();
        Run(s2, dt, 14f, null, () =>
        {
            hold(); sec.TargetId = 1; sec.RetargetT = 99f;
            tS += dt;
            g2.Me.Pos = new V2(spot.X, 300f + 110f * (float)Math.Sin(tS * 0.9f));  // moving up and down, so it has to lead
            if (sec.Winding && firstWindS < 0f) firstWindS = tS;
            foreach (var b in g2.Bullets.Values.Where(b => b.Kind == Defs.TracerRound && b.Shooter == sec.Id).ToList())
                if (seenBullets.Add(b.Id))
                {
                    shotsS++;
                    if (firstShotS < 0f) firstShotS = tS;
                    lastShotS = tS;
                    V2 direct = (g2.Me.Pos - sec.Pos).Normalized();
                    V2 vel = b.Vel.Normalized();
                    float motionY = hostMe.Motion.Y;
                    // a shot leads when it is aimed past where you stand, the way you're moving
                    if (Math.Abs(motionY) > 40f && (vel.Y - direct.Y) * motionY > 0f) leads = true;
                }
            if (reloadStart < 0f && sec.Phase == 0) reloadStart = tS;
            if (reloadStart > 0f && reloadEnd < 0f && sec.Phase == Game.SecurityMag) reloadEnd = tS;
            clientSaw |= c2.Bullets.Values.Any(b => b.Kind == Defs.TracerRound);
            Enemy ce;
            if (c2.Enemies.TryGetValue(sec.Id, out ce) && ce.Phase == 0) clientPhase = true;
            if (shotsS >= 8) sec.FireT = 99f;
        });
        damaged = hostMe.Hp < hpS;
        Console.WriteLine("  security guard: first lock-on {0:0.00} s, {1} rounds (first {2:0.00} s, 7th ~{3:0.00} s), reloaded from {4:0.00} s to {5:0.00} s ({6:0.00} s), leads {7}, friend sees {8}/{9}",
            firstWindS, shotsS, firstShotS, lastShotS, reloadStart, reloadEnd, reloadEnd - reloadStart, leads, clientSaw, clientPhase);
        Check(firstWindS > 0f && firstShotS - firstWindS > 0.15f && firstShotS - firstWindS < 0.4f, "a Security Guard locks on (Winding) for about 0.25 s before each round");
        Check(shotsS >= 8 && reloadStart > 0f, "it fires seven rounds, then the magazine runs dry and it reloads");
        Check(reloadEnd - reloadStart > 1.9f && reloadEnd - reloadStart < 2.6f, "the reload takes about 2.2 s");
        Check(leads, "its rounds are aimed ahead of where you are heading");
        Check(clientSaw && clientPhase, "the friend's game sees the tracer rounds and the reload (its ammo is Enemy.Phase)");
        Check(damaged, "a tracer round that lands hurts");
        Check(Approaches.For(EnemyType.Boss).Members.Any(m => m.Type == EnemyType.SecurityGuard && m.From <= 2), "the Warden's faction includes the Security Guard, from its second wave");
        sec.Hp = 0f;

        // --- The Riot Line (the last wave before the Warden): shields march in a wall, guards behind, then charge --------
        var finale = Approaches.For(EnemyType.Boss).Finale;
        Check(finale != null && w5.Count(t => t == EnemyType.RiotShield) >= 4 && w5.Count(t => t == EnemyType.Guard) >= 5,
            "the last wave before the Warden always brings the Riot Line (four Riot Shields, five Guards)");
        foreach (var e in g2.Enemies.Values) e.Hp = 0f;
        Run(s2, dt, 0.3f, null, () => { hold(); g2.Me.Pos = spot; });
        typeof(Game).GetMethod("SpawnSquad", NP).Invoke(g2, new object[] { finale });
        var lineIds = g2.Enemies.Values.Where(e => e.SquadId != 0 && (e.Type == EnemyType.RiotShield || e.Type == EnemyType.Guard)).Select(e => e.Id).ToList();
        Action still = () => { hold(); g2.Me.Pos = spot; foreach (var p in g2.Players.Values) p.InvulnT = 1f; };
        Run(s2, dt, 2.5f, null, still);
        Func<EnemyType, float> avgDist = t => lineIds.Where(g2.Enemies.ContainsKey).Select(id => g2.Enemies[id]).Where(e => e.Type == t).Select(e => e.Pos.DistanceTo(spot)).DefaultIfEmpty(0f).Average();
        float dShield = avgDist(EnemyType.RiotShield), dGuard = avgDist(EnemyType.Guard);
        var broke = (System.Collections.Generic.HashSet<int>)typeof(Game).GetField("riotLineBroke", NP).GetValue(g2);
        bool marching = broke.Count == 0;
        Run(s2, dt, 20f, () => broke.Count > 0, still);
        float dGuardAfter = avgDist(EnemyType.Guard);
        Run(s2, dt, 1.5f, null, still);
        Console.WriteLine("  riot line: shields {0:0} px from you, guards {1:0} px (marching {2}); broke {3}, guards then {4:0} -> {5:0} px",
            dShield, dGuard, marching, broke.Count > 0, dGuardAfter, avgDist(EnemyType.Guard));
        Check(marching && dShield > 0f && dGuard > dShield + 40f, "the Riot Line marches as a wall of shields with the guards close behind");
        Check(broke.Count > 0 && (avgDist(EnemyType.Guard) < dGuardAfter || dGuardAfter < 120f), "as the wall closes in, the guards break out and rush you");
        foreach (var id in lineIds) if (g2.Enemies.ContainsKey(id)) g2.Enemies[id].Hp = 0f;
        host.Leave(""); client.Leave("");

        // --- The Warden's pair in his searchlight turn ---------------------------------------------------------------------
        var s5 = StartPlain(dt, 5);
        var g5 = s5[0].Game;
        Run(s5, dt, 8f, () => g5.State == MatchState.Fighting && Find(s5[0], EnemyType.Boss) != null && !Find(s5[0], EnemyType.Boss).Spawning);
        var warden = Find(s5[0], EnemyType.Boss);
        typeof(Game).GetMethod("SendRiotShields", NP).Invoke(g5, new object[] { warden });
        typeof(Game).GetMethod("SendRiotShields", NP).Invoke(g5, new object[] { warden });
        var pair = g5.Enemies.Values.Where(e => e.Type == EnemyType.RiotShield && e.ParentId == warden.Id).ToList();
        Check(pair.Count == 2, "the Warden sends two Riot Shields into his searchlight turn (and no more while they're up)");
        // While the light hunts, a shield heads for your far side from the light.
        var ws = (WardenState)typeof(Game).GetMethod("WardenOf", NP).Invoke(g5, new object[] { warden });
        ws.LightMode = WardenLight.Follow;
        var target = g5.Players[1];
        target.Pos = new V2(800f, 450f);
        ws.Light = new V2(800f, 250f);
        pair[0].TargetId = 1;
        var herd = (V2)typeof(Game).GetMethod("HerdSpot", NP).Invoke(g5, new object[] { pair[0], target.Pos });
        Console.WriteLine("  herding: player at ({0:0},{1:0}), light at ({2:0},{3:0}), the shield heads for ({4:0},{5:0})", target.Pos.X, target.Pos.Y, ws.Light.X, ws.Light.Y, herd.X, herd.Y);
        Check(herd.Y > target.Pos.Y + 20f && Math.Abs(herd.X - target.Pos.X) < 5f, "while the searchlight hunts, a Riot Shield works round to your side away from the light");
        warden.Hp = 0f;
        Run(s5, dt, 0.3f);
        Check(!g5.Enemies.Values.Any(e => e.Type == EnemyType.RiotShield), "the Warden's Riot Shields go with him");
        s5[0].Leave(""); s5[1].Leave("");

        // --- A fully themed approach wave plays out and clears with no Warden ------------------------------------------------
        var sb = StartBuffed(dt, 3, 8);
        var gq = sb[0].Game;
        sb[0].Game.Diff.Set(DiffKnob.ApproachShare, 1f);  // the host builds the waves
        sb[0].Game.Diff.Set(DiffKnob.ApproachRamp, 0f);
        Run(sb, dt, 8f, () => gq.State == MatchState.Fighting);
        int startWave = gq.Wave;
        var seen = new HashSet<EnemyType>();
        Run(sb, dt, 120f, () => gq.Wave > startWave + 1, () => { foreach (var e in gq.Enemies.Values) seen.Add(e.Type); });
        Console.WriteLine("  bots played from wave {0} to {1}; enemies seen: {2}", startWave, gq.Wave, string.Join(", ", seen));
        Check(gq.Wave > startWave && seen.Contains(EnemyType.RiotShield) && seen.Contains(EnemyType.Rioter), "a fully themed approach wave (Rioters, Guards, Riot Shields, Grenadiers) plays out and clears with no Warden");
        sb[0].Leave(""); sb[1].Leave("");
    }
}
