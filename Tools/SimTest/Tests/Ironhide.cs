using System;
using System.Threading;
using Arena;

// The Tank role passive Ironhide and its cards (Iron Plates, Quick Refit, Shared Plating; a 25 shield that takes half of each hit, the rest reaching health, refilling at 25/s after 3 s without damage; empty when
// downed; Tanks only; in the snapshot) and the Healer aura's falloff (17 HP/s next to the Healer down to 5 at the rim).
static partial class Program
{
    static void IronhideTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var damage = typeof(Game).GetMethod("Damage", flags);
        var tick = typeof(Game).GetMethod("TickIronhide", flags);
        Game.IronhideOff = false;
        try { IronhideChecks(dt, flags, damage, tick); } finally { Game.IronhideOff = true; }
    }

    static void IronhideChecks(float dt, System.Reflection.BindingFlags flags, System.Reflection.MethodInfo damage, System.Reflection.MethodInfo tick)
    {

        Func<Role, Session> Make = role =>
        {
            var host = new Session { AutoStart = 1 };
            host.Host(nextPort++, "Iron", role, 0, 0, Kit.Soldier);
            host.Game.Bot = false;
            host.StartMatch();
            for (int f = 0; f < 360 && host.Game.State != MatchState.Fighting; f++) Step(host, dt);
            return host;
        };

        var tank = Make(Role.Tank); var g = tank.Game; var p = g.Players[1];
        Check(p.MaxHp <= 170f + 0.01f && Defs.Get(Role.Tank).Hp == 170f, "a Tank has 170 health");
        Check(Math.Abs(Game.Ironhide(p) - Game.IronhideMax) < 0.01f, "Ironhide starts full (" + Game.IronhideMax + ")");

        float hp0 = p.Hp;
        damage.Invoke(g, new object[] { p, 10f, "test" });
        float took = Game.IronhideMax - Game.Ironhide(p);
        Check(Math.Abs(took - 10f * Game.IronhideShare) < 0.01f && Math.Abs(hp0 - p.Hp - 10f * (1f - Game.IronhideShare)) < 0.01f, "a hit is split between the shield and health");
        Check(Math.Abs(p.IronWait - Game.IronhideDelay) < 0.01f, "a hit starts the " + Game.IronhideDelay + " s wait");

        for (int f = 0; f < (int)((Game.IronhideDelay - 0.3f) / dt); f++) tick.Invoke(g, new object[] { p, dt });
        Check(Math.Abs(Game.IronhideMax - Game.Ironhide(p) - took) < 0.01f, "no refill during the wait");
        for (int f = 0; f < (int)(1.3f / dt); f++) tick.Invoke(g, new object[] { p, dt });
        Check(Math.Abs(Game.Ironhide(p) - Game.IronhideMax) < 0.01f, "then it refills");

        p.IronLost = Game.IronhideMax - 20f;  // 20 left
        damage.Invoke(g, new object[] { p, 1000f, "test" });
        Check(Game.Ironhide(p) <= 0.01f, "a big hit breaks it");

        p.Downed = false; p.Hp = p.MaxHp; p.Wound = 0f; p.InvulnT = 0f;
        p.IronLost = Game.IronhideMax - 4f;  // 4 left: a 20 hit takes the 4, the other 16 reaches health
        damage.Invoke(g, new object[] { p, 20f, "test" });
        Check(Game.Ironhide(p) <= 0.01f && Math.Abs(p.MaxHp - p.Hp - 16f) < 0.01f, "a nearly empty shield takes only what it has left");
        Check(p.Wound > 0f, "the share that reaches health still wounds");

        p.Downed = false; p.Hp = p.MaxHp;  // (the big hit put them down)
        p.IronLost = 20f; p.IronWait = 0f;
        for (int f = 0; f < (int)(0.5f / dt); f++) tick.Invoke(g, new object[] { p, dt });
        Check(Math.Abs(p.IronLost - (20f - Game.IronhideRegen * 0.5f)) < 0.6f, "it refills at " + Game.IronhideRegen + " a second");

        p.IronLost = 0f; p.Downed = true; tick.Invoke(g, new object[] { p, dt }); p.Downed = false;
        Check(Game.Ironhide(p) <= 0.01f && p.IronWait > 0f, "going down empties it");

        // Round-trip over the snapshot (a client draws the plates and the HUD).
        p.IronLost = 12f; p.IronWait = 1.5f;
        var sync = typeof(Game).GetMethod("SyncSnapshot", flags);
        var ms = new System.IO.MemoryStream();
        sync.Invoke(g, new object[] { new NetSync(new System.IO.BinaryWriter(ms)) });
        p.IronLost = 0f; p.IronWait = 0f;
        ms.Position = 0;
        sync.Invoke(g, new object[] { new NetSync(new System.IO.BinaryReader(ms)) });
        Check(Math.Abs(p.IronLost - 12f) < 0.001f && Math.Abs(p.IronWait - 1.5f) < 0.001f, "the shield travels in the snapshot");

        var dps = Make(Role.Dps); var dg = dps.Game; var dp = dg.Players[1];
        float dhp = dp.Hp;
        damage.Invoke(dg, new object[] { dp, 10f, "test" });
        Check(Game.Ironhide(dp) == 0f && dp.Hp < dhp, "a DPS has no Ironhide");

        // The cards (Tank only): Iron Plates, Quick Refit, Shared Plating.
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        Check(Upgrades.All[U("Iron Plates")].Role == Role.Tank && Upgrades.All[U("Quick Refit")].Role == Role.Tank && Upgrades.All[U("Shared Plating")].Role == Role.Tank, "the Ironhide cards are Tank cards");
        var tick2 = typeof(Game).GetMethod("TickIronhide", flags);
        p.Downed = false; p.Hp = p.MaxHp; p.IronLost = 0f; p.IronWait = 0f;
        g.GrantUpgrade(1, U("Iron Plates")); g.GrantUpgrade(1, U("Iron Plates"));
        tick2.Invoke(g, new object[] { p, dt });
        Check(Math.Abs(p.IronCap - (Game.IronhideMax + 2f * Game.IronPlatesBonus)) < 0.01f && Math.Abs(Game.Ironhide(p) - p.IronCap) < 0.01f, "two Iron Plates: Ironhide holds " + p.IronCap);
        g.GrantUpgrade(1, U("Quick Refit"));
        p.InvulnT = 0f; damage.Invoke(g, new object[] { p, 10f, "test" });
        Check(Math.Abs(p.IronWait - (Game.IronhideDelay - Game.IronRefitCut)) < 0.01f, "Quick Refit: the wait is " + Game.IronRefitCut + " s shorter");
        p.IronLost = 20f; p.IronWait = 0f;
        tick2.Invoke(g, new object[] { p, 0.1f });
        Check(Math.Abs(p.IronLost - (20f - Game.IronhideRegen * (1f + Game.IronRefitRegen) * 0.1f)) < 0.01f, "...and the refill is 25% faster");

        // Shared Plating: a host Tank and a DPS friend.
        int port = nextPort++;
        var sh = new Session { AutoStart = 2 };
        sh.Host(port, "Iron", Role.Tank, 0, 0, Kit.Soldier);
        sh.Game.Bot = false;
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps, 0);
        WaitForGame(sh, client, dt);
        var hg = sh.Game; var tankP = hg.Players[1]; var friend = hg.Players[2];
        Step(sh, dt);
        Check(friend.IronCap == 0f, "a DPS has no shield before Shared Plating");
        hg.GrantUpgrade(1, U("Iron Plates"));
        hg.GrantUpgrade(1, U("Shared Plating"));
        for (int f = 0; f < 5; f++) { Step(sh, dt); Step(client, dt); }
        Check(Math.Abs(tankP.IronCap - (Game.IronhideMax + Game.IronPlatesBonus) * Game.IronShareKeep) < 0.01f, "Shared Plating halves the Tank's Ironhide, Iron Plates too (" + tankP.IronCap + ")");
        Check(Math.Abs(friend.IronCap - Game.IronShareGift) < 0.01f, "every other player gets a " + Game.IronShareGift + " shield");
        hg.GrantUpgrade(1, U("Iron Plates"));
        for (int f = 0; f < 3; f++) Step(sh, dt);
        Check(Math.Abs(tankP.IronCap - (Game.IronhideMax + 2f * Game.IronPlatesBonus) * Game.IronShareKeep) < 0.01f && Math.Abs(friend.IronCap - Game.IronShareGift) < 0.01f, "later Iron Plates count half for the Tank and never change the gift");
        friend.Hp = friend.MaxHp; friend.InvulnT = 0f; friend.IronLost = 0f;
        float fhp = friend.Hp;
        damage.Invoke(hg, new object[] { friend, 10f, "test" });
        Check(Math.Abs(friend.IronLost - 10f * Game.IronhideShare) < 0.01f && Math.Abs(fhp - friend.Hp - 10f * (1f - Game.IronhideShare)) < 0.01f && Math.Abs(friend.IronWait - Game.IronhideDelay) < 0.01f, "the gift splits hits like Ironhide, with the base wait");
        for (int f = 0; f < 60; f++) { Step(sh, dt); Step(client, dt); Nap(2); }
        Check(Math.Abs(client.Game.Players[2].IronCap - Game.IronShareGift) < 0.01f, "the friend's game knows the shield (for the plates and the HUD)");

        // The Knight's Chainmail: 15 on the same shield, stacking with a Tank Knight's Ironhide; 105% health.
        Func<Role, Session> MakeKnight = role =>
        {
            var kh = new Session { AutoStart = 1 };
            kh.Host(nextPort++, "Mail", role, 0, 0, Kit.Knight);
            kh.Game.Bot = false;
            kh.StartMatch();
            for (int f = 0; f < 360 && kh.Game.State != MatchState.Fighting; f++) Step(kh, dt);
            return kh;
        };
        var dk = MakeKnight(Role.Dps); var dkp = dk.Game.Players[1];
        Check(Math.Abs(dkp.BaseHp - Defs.Get(Role.Dps).Hp * 1.05f) < 0.01f, "a Knight has 105% health");
        Check(Math.Abs(dkp.IronCap - Game.KnightMail) < 0.01f && Math.Abs(Game.Ironhide(dkp) - Game.KnightMail) < 0.01f, "a DPS Knight's Chainmail holds " + Game.KnightMail + " and starts full");
        float khp = dkp.Hp;
        damage.Invoke(dk.Game, new object[] { dkp, 10f, "test" });
        Check(Math.Abs(dkp.IronLost - 10f * Game.IronhideShare) < 0.01f && Math.Abs(khp - dkp.Hp - 10f * (1f - Game.IronhideShare)) < 0.01f, "Chainmail splits hits like Ironhide");
        var tk = MakeKnight(Role.Tank); var tkp = tk.Game.Players[1];
        Check(Math.Abs(tkp.IronCap - (Game.IronhideMax + Game.KnightMail)) < 0.01f, "a Tank Knight's Chainmail stacks with Ironhide (" + (Game.IronhideMax + Game.KnightMail) + ")");
        Check(Game.StartShield(Role.Tank, Kit.Knight) == Game.IronhideMax + Game.KnightMail && Game.StartShield(Role.Healer, Kit.Soldier) == 0f, "the lobby's starting shield");

        // The Healer aura's falloff.
        var heal = Make(Role.Healer); var hp = heal.Game.Players[1];
        float mult = hp.Stats.AuraHealMult;
        Check(Math.Abs(Game.AuraHealAt(hp, hp.Pos) - Game.AuraHealNear * mult) < 0.01f, "the aura heals " + Game.AuraHealNear + " HP/s next to the Healer");
        Check(Math.Abs(Game.AuraHealAt(hp, hp.Pos + new V2(hp.AuraRange * 0.5f, 0f)) - (Game.AuraHealNear + Game.AuraHealRim) / 2f * mult) < 0.01f, "half way out, half way down");
        Check(Math.Abs(Game.AuraHealAt(hp, hp.Pos + new V2(hp.AuraRange - 0.01f, 0f)) - Game.AuraHealRim * mult) < 0.01f, Game.AuraHealRim + " HP/s at the rim");
        Check(Game.AuraHealAt(hp, hp.Pos + new V2(hp.AuraRange + 1f, 0f)) == 0f, "nothing outside");
    }
}
