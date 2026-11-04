using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Riot Gear (RiotGear.cs, Docs/RiotGear.md): blocks one hit, a half second of grace, a 30 s recharge; burns and gas go through.
static partial class Program
{
    static void RiotGearTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var damage = typeof(Game).GetMethod("Damage", flags);
        var perksFor = typeof(Game).GetMethod("PerksFor", flags);

        int port = nextPort++;
        var host = new Session { StartWave = 3 };
        var prof = host.Game.ProfileOverride = new Profile();
        prof.BossBeaten(EnemyType.Boss, 5);
        prof.HuntEnded(EnemyType.Boss, true, 300f);
        Perks.Equip(prof, PerkId.RiotGear);
        host.Host(port, "Host", Role.Tank, 0);
        var client = new Session();
        var cprof = client.Game.ProfileOverride = new Profile();
        cprof.BossBeaten(EnemyType.Boss, 5);
        cprof.HuntEnded(EnemyType.Boss, true, 300f);
        Perks.Equip(cprof, PerkId.RiotGear);
        client.Join("127.0.0.1", port, "Friend", Role.Dps, 0);
        var s = new[] { host, client };
        WaitForLobby(host, client);
        for (int i = 0; i < 50; i++) { host.Poll(); client.Poll(); System.Threading.Thread.Sleep(2); }
        Check(host.Lobby[2].PerkIds.Length == 1 && client.Lobby[1].PerkIds.Length == 1, "a player's perks reach the host's lobby and everyone's lobby (Msg.Cosmetics, Msg.Lobby)");
        // Changing perks in the lobby tells everyone (the Perks tab calls ShareCosmetics): off, then on again.
        Perks.Unequip(cprof, PerkId.RiotGear); client.ShareCosmetics();
        for (int i = 0; i < 50; i++) { host.Poll(); client.Poll(); System.Threading.Thread.Sleep(2); }
        Check(host.Lobby[2].PerkIds.Length == 0 && client.Lobby[2].PerkIds.Length == 0, "taking a perk off in the lobby reaches the host and everyone");
        Perks.Equip(cprof, PerkId.RiotGear); client.ShareCosmetics();
        for (int i = 0; i < 50; i++) { host.Poll(); client.Poll(); System.Threading.Thread.Sleep(2); }
        Check(host.Lobby[2].PerkIds.Length == 1, "and putting it back does too");
        client.RequestReady(true); host.RequestReady(true);
        for (int i = 0; i < 50; i++) { host.Poll(); client.Poll(); System.Threading.Thread.Sleep(2); }
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];

        Action calm = () => { foreach (var e in g.Enemies.Values) e.Hp = 0f; g.Bullets.Clear(); me.Hp = me.MaxHp; ally.Hp = ally.MaxHp; };
        Action<Player, float, string, bool> hit = (p, amount, src, dot) => (dot ? typeof(Game).GetMethod("DamageOverTime", flags) : damage).Invoke(g, dot ? new object[] { p, amount, src } : new object[] { p, amount, src });
        calm();

        Check(Game.HasPerk(me, PerkId.RiotGear) && Game.HasPerk(ally, PerkId.RiotGear), "both players' equipped perks reach the host through the lobby and the match start");
        var cg = client.Game;
        Check(Game.HasPerk(cg.Players[1], PerkId.RiotGear) && Game.HasPerk(cg.Players[2], PerkId.RiotGear), "and every player's perks are known on the friend's screen too");
        ally.Perks = Perks.Empty;  // from here the friend plays without it
        Check(Game.RiotReady(me) && me.RiotT == 0f, "the shield starts ready");

        // A hit is blocked: no health lost, the recharge starts, a half second of grace.
        float before = me.Hp, taken = me.Run.DamageTaken;
        V2 shotAt = me.Pos + new V2(40f, 0f);
        g.Bullets[900001] = new Bullet { Id = 900001, Kind = 10, Pos = shotAt, Vel = new V2(-10f, 0f), Life = 3f, Radius = 4f, Dmg = 5f };
        hit(me, 20f, "Test shot", false);
        var blockFx = g.Effects.LastOrDefault(e => e.Kind == FxKind.RiotBlock);
        Check(blockFx != null && blockFx.Pos2.DistanceTo(shotAt) < 1f && blockFx.Pos.DistanceTo(me.Pos) < 1f && Math.Abs(blockFx.Radius - (me.Def.BodyR + 11f)) < 0.01f, "a block sends the RiotBlock effect, pointing at the shot that was blocked");
        Run(s, dt, 0.15f, null, calm);
        Check(cg.Effects.Any(e => e.Kind == FxKind.RiotBlock), "and the friend's screen gets it");
        me.RiotT = 0f; me.InvulnT = 0f;
        before = me.Hp; taken = me.Run.DamageTaken;
        hit(me, 20f, "Test shot", false);
        Check(me.Hp == before && me.Run.DamageTaken == taken && !me.Downed, "the first hit is blocked: no health lost and no damage counted");
        Check(Math.Abs(me.RiotT - Perks.RiotRecharge) < 0.001f && me.InvulnT >= Perks.RiotGraceT - 0.001f && !Game.RiotReady(me), "a block starts the 30 s recharge and half a second of invulnerability");

        // While it recharges hits land, and burning ground never touched it.
        hit(me, 20f, "Test shot", false);
        Check(me.Hp < before && me.RiotT > 0f, "a hit while it recharges lands");
        calm();
        me.RiotT = 0f;
        before = me.Hp;
        hit(me, 5f, "Burning Grounds", true);
        Check(me.Hp < before && Game.RiotReady(me), "a burn tick goes through and doesn't spend the shield");
        calm();

        // A teammate without the perk is hit normally.
        float allyHp = ally.Hp;
        hit(ally, 20f, "Test shot", false);
        Check(ally.Hp < allyHp, "a player without the perk takes the hit");
        calm();

        // The friend's screen sees the timer through the snapshot.
        calm();
        me.RiotT = 0f;
        hit(me, 20f, "Test shot", false);
        Run(s, dt, 1f, null, calm);
        Check(cg.Players[1].RiotT > 27f && Math.Abs(cg.Players[1].RiotT - me.RiotT) < 1.5f, "the recharge timer is in the snapshot (friend sees " + cg.Players[1].RiotT.ToString("0.0") + " s, host " + me.RiotT.ToString("0.0") + " s)");

        // The recharge: not back at 29 s, back at 30.
        me.RiotT = 0f;
        hit(me, 20f, "Test shot", false);
        Check(me.RiotT > 29.9f, "blocked again, 30 s to go");
        Run(s, dt, 29f, null, calm);
        Check(me.RiotT > 0f && me.RiotT < 1.2f && !Game.RiotReady(me), "29 s later it is still down (" + me.RiotT.ToString("0.00") + " s left)");
        Run(s, dt, 1.5f, null, calm);
        Check(me.RiotT == 0f && Game.RiotReady(me), "after 30 s it is back");
        calm();
        before = me.Hp;
        hit(me, 20f, "Test shot", false);
        Check(me.Hp == before, "and it blocks again");

        // The host decides who brings what: none where runs aren't recorded (Practice Range, Boss Lab, lobby preview).
        var entry = host.Lobby[1];
        Check(entry.ChosenPerks.Length == 1 && ((PerkId[])perksFor.Invoke(g, new object[] { entry })).Length == 1, "in a real run the lobby entry's perks are taken in");
        var oldPlan = g.Practice;
        g.Practice = new PracticePlan();
        Check(((PerkId[])perksFor.Invoke(g, new object[] { entry })).Length == 0, "the Practice Range takes the perks off");
        g.Practice = oldPlan;
        g.Unrecorded = true;
        Check(((PerkId[])perksFor.Invoke(g, new object[] { entry })).Length == 0, "the Boss Lab takes the perks off");
        g.Unrecorded = false;

        // A continued run gives each seat back the perks it was saved with, whatever the lobby says.
        var save = new RunSave();
        var seat = new SeatSave(); seat.Perks.Add("RiotGear");
        var bare = new SeatSave();
        save.Seats.Add(seat); save.Seats.Add(bare);
        g.ResumeFrom = save; g.ResumeSeats[1] = 0; g.ResumeSeats[2] = 1;
        Check(((PerkId[])perksFor.Invoke(g, new object[] { host.Lobby[1] })).Length == 1, "a continued run keeps the perk the seat was saved with");
        entry.ChosenPerks = new[] { PerkId.RiotGear };
        Check(((PerkId[])perksFor.Invoke(g, new object[] { entry })).Length == 1 && ((PerkId[])perksFor.Invoke(g, new object[] { host.Lobby[2] })).Length == 0, "and a seat saved with none can't bring one now");
        g.ResumeFrom = null; g.ResumeSeats.Clear();

        // And the lobby shows that: on a continued run each seat's perks are the saved ones, not the ones picked.
        var broadcast = typeof(Session).GetMethod("BroadcastLobby", flags);
        host.Resume = save;
        host.Lobby[1].Seat = 0; host.Lobby[2].Seat = 1;
        broadcast.Invoke(host, null);
        Check(host.Lobby[1].PerkIds.Length == 1 && host.Lobby[2].PerkIds.Length == 0 && host.Lobby[2].ChosenPerks.Length == 1, "a continued run's lobby shows each seat's saved perks, and remembers what was picked");
        host.Resume = null;
        host.Lobby[1].Seat = -1; host.Lobby[2].Seat = -1;
        broadcast.Invoke(host, null);
        Check(host.Lobby[2].PerkIds.Length == 1, "back to a fresh run, the picked perks show again");

        // The save's text keeps them, by name; a perk that's gone is skipped; the lobby message is sanitised.
        string text = save.ToText();
        Check(text.Contains("perks RiotGear"), "a saved run writes each seat's perks by name");
        Check(Perks.FromNames(new[] { "RiotGear", "NoSuchPerk", "RiotGear", "Contraband" }).SequenceEqual(new[] { PerkId.RiotGear }), "perk names that are gone or repeated, and a second perk from one boss, are skipped");
        Check(Perks.Sanitize(new byte[] { 0, 0, 1, 200, 255 }).SequenceEqual(new[] { PerkId.RiotGear }), "the host drops ids that aren't real, repeated, or a second perk from one boss");
        host.Leave(""); client.Leave("");
    }
}
