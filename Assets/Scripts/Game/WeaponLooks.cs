using System;

namespace Arena
{
    /// A melee weapon's held pose now (MeleeRest, MeleeLooks.cs): turns `ang` from the aim, sets or scales `reach`, sets `bend`.
    /// False: no held pose now.
    delegate bool MeleeRestFn(Player p, ref float ang, ref float reach, ref float bend);

    /// How each weapon looks on the front end, beside its WeaponDef (Core) the way KitLook (ClassLooks.cs) stands beside a class:
    /// the model's held weapon and grip (CharacterModels.cs) and a melee weapon's energy colour and resting pose (MeleeLooks.cs).
    /// A new weapon adds its L line here. Any field can be left out.
    class WeaponLook
    {
        public Action<ArenaApp, Player, float, int> Held;  // (player, time, hold): the weapon in the hand's frame (MFrame; DrawHeld)
        public int Hold;                // 0 one hand, 1 two hands, 2 a gun in each hand, 3 an open palm, a staff or a floating prop (HoldOf)
        public float HoldOffX;          // two hands: where the second one grips along the weapon
        public Func<ArenaApp, Player, bool> Arms;  // true when the weapon's own look draws the arms, gloves and weapon now, held and
                                                   // swung, so the model leaves its own out (LookDrawsArms); null: never
        public bool Pauldrons;          // that look also puts a pauldron on each shoulder while it draws the arms (LookDrawsPauldrons)
        public Rgba? MeleeColor;        // a melee weapon's energy colour (MeleeColor); null: the effect's own
        public MeleeRestFn Rest;        // a melee weapon drawn by DrawMeleeModel: its held pose (MeleeRest); null: none
    }

    public partial class ArenaApp
    {
        static WeaponLook[] weaponLooks;

        /// A weapon's look; null for one with none (the Engineer's robot weapons draw their own).
        static WeaponLook LookOf(WeaponId id) => (weaponLooks ?? BuildWeaponLooks())[(int)id];

        static void L(WeaponId id, WeaponLook look)
        {
            if (weaponLooks[(int)id] != null)
                throw new InvalidOperationException("WeaponLooks.cs has two looks for WeaponId." + id);
            weaponLooks[(int)id] = look;
        }

        /// Every weapon's look, by WeaponId.
        static WeaponLook[] BuildWeaponLooks()
        {
            weaponLooks = new WeaponLook[256];  // (WeaponId is a byte)
            // ---- Soldier
            L(WeaponId.Scattergun, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldScattergun(p, t, hold),
                                                    Hold = 1, HoldOffX = 14f });
            L(WeaponId.FlakCannon, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldFlakCannon(p, t, hold),
                                                    Hold = 1, HoldOffX = 10f });
            L(WeaponId.RiotRepeater, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldRiotRepeater(p, t, hold),
                                                      Hold = 1, HoldOffX = 14f });
            L(WeaponId.RepulsionCannon, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldRepulsionCannon(p, t, hold),
                                                         Hold = 1, HoldOffX = 8f });
            L(WeaponId.Revolver, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldRevolver(p, t, hold) });
            L(WeaponId.MendersBow, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldMendersBow(p, t, hold),
                                                    Hold = 1, HoldOffX = 18f });
            L(WeaponId.TwinPistols, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldTwinPistols(p, t, hold),  // TwinPistolsLook.cs
                                                     Hold = 2 });
            L(WeaponId.TwinRifle, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldTwinRifle(p, t, hold),
                                                   Hold = 2 });
            L(WeaponId.TwinRifleAuto, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldTwinRifle(p, t, hold),
                                                       Hold = 2 });
            L(WeaponId.OutOfAmmo, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldTwinRifle(p, t, hold),
                                                   Hold = 2 });
            L(WeaponId.GrenadeLauncher, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldGrenadeLauncher(p, t, hold),  // the front hand sits on the foregrip: GrenadeLauncherLook.cs GlFrontHand
                                                         Hold = 1, HoldOffX = 31f });
            L(WeaponId.DoubleBarrel, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldDoubleBarrel(p, t, hold),
                                                      Hold = 1, HoldOffX = 15f });
            L(WeaponId.Railgun, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldRailgun(p, t, hold),
                                                 Hold = 1, HoldOffX = 18f });
            L(WeaponId.BurstSmg, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldBurstSmg(p, t, hold),
                                                  Hold = 1, HoldOffX = 8f });
            // ---- Elementalist
            L(WeaponId.Flamethrower, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldFlamethrower(p, t, hold),
                                                      Hold = 1, HoldOffX = 12f });
            L(WeaponId.WaterGun, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldWaterGun(p, t, hold),  // ElementGunLook.cs
                                                  Hold = 1, HoldOffX = 12f });
            L(WeaponId.GaleGun, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldGaleGun(p, t, hold),  // ElementGunLook.cs
                                                 Hold = 1, HoldOffX = 12f });
            L(WeaponId.FrostLance, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldFrostLance(p, t, hold),
                                                    Hold = 3 });
            L(WeaponId.StormHammer, new WeaponLook { Held = (a, p, t, hold) => a.DrawStormHammerHeld(p, t) });  // StormHammerLook.cs: raised, thrown, slammed and re-formed with the throw and the slam
            L(WeaponId.RadiantOrb, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldRadiantOrb(p, t, hold),
                                                    Hold = 3 });
            L(WeaponId.ChainLight, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldChainLight(p, t, hold) });
            L(WeaponId.Nimbus, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldNimbus(p, t, hold),
                                                Hold = 3 });
            L(WeaponId.Fireball, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldFireball(p, t, hold),
                                                  Hold = 3 });
            L(WeaponId.Waterball, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldWaterball(p, t, hold),  // ElementBallLook.cs
                                                   Hold = 3 });
            L(WeaponId.Earthball, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldEarthball(p, t, hold),  // ElementBallLook.cs
                                                   Hold = 3 });
            L(WeaponId.ArcLightning, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldArcLightning(p, t, hold) });
            L(WeaponId.EmbolusSpears, new WeaponLook { Held = (a, p, t, hold) => a.DrawEsHeld(p, a.mHeldTube, t),
                                                       Hold = 2 });
            L(WeaponId.Stonespear, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldStonespear(p, t, hold),
                                                    Hold = 3 });
            L(WeaponId.WindClash, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldWindClash(p, t, hold),
                                                   Hold = 3 });
            L(WeaponId.StaticShock, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldStaticShock(p, t, hold),  // a crackling spark on the palm
                                                     Hold = 3 });
            L(WeaponId.TwinTessen, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldTessen(p, t, hold),  // a war fan in each hand (TwinTessenLook.cs)
                                                    Hold = 2 });
            L(WeaponId.HiveCenser, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldHiveCenser(p, t, hold),  // a small censer with insects round it (HiveCenserLook.cs)
                                                    Hold = 3 });
            L(WeaponId.IceShards, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldIceShards(p, t, hold),
                                                   Hold = 3 });
            // ---- Summoner
            L(WeaponId.GolemStaff, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldGolemStaff(p, t, hold),
                                                    Hold = 3 });
            L(WeaponId.BoneGuard, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldBoneGuard(p, t, hold) });
            L(WeaponId.WyvernStaff, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldWyvernStaff(p, t, hold),
                                                     Hold = 3 });
            L(WeaponId.BearTotem, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldBearTotem(p, t, hold) });
            L(WeaponId.SpriteStaff, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldSpriteStaff(p, t, hold),
                                                     Hold = 3 });
            L(WeaponId.TreantRod, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldTreantRod(p, t, hold) });
            L(WeaponId.SpiritChalk, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldSpiritChalk(p, t, hold),
                                                     Hold = 3 });
            L(WeaponId.WolfWhistle, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldWolfWhistle(p, t, hold) });
            L(WeaponId.WispLantern, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldWispLantern(p, t, hold) });
            L(WeaponId.HoneybeeStaff, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldHoneybeeStaff(p, t, hold),
                                                       Hold = 3 });
            L(WeaponId.ImpCaller, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldImpCaller(p, t, hold) });
            // ---- Knight
            L(WeaponId.Warhammer, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldWarhammer(p, t, hold),
                                                   Hold = 1, HoldOffX = 12f,
                                                   Arms = (a, p) => a.fxOn && !p.Downed,  // MeleeLooks.cs
                                                   MeleeColor = new Rgba(1f, 0.64f, 0.42f) });
            L(WeaponId.BulwarkBarrier, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldBulwarkBarrier(p, t, hold) });
            L(WeaponId.Lance, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldLance(p, t, hold),
                                               Hold = 1, HoldOffX = -4f,
                                               Arms = (a, p) => true, Pauldrons = true,  // LanceLook.cs
                                               MeleeColor = new Rgba(0.62f, 0.88f, 1f) });
            L(WeaponId.Halberd, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldHalberd(p, t, hold),
                                                 Hold = 1, HoldOffX = 10f,
                                                 Arms = (a, p) => a.fxOn, Pauldrons = true,  // HalberdLook.cs
                                                 MeleeColor = new Rgba(0.56f, 0.94f, 0.78f) });
            L(WeaponId.Quarterstaff, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldQuarterstaff(p, t, hold),
                                                      Hold = 1, HoldOffX = 8f,
                                                      Arms = (a, p) => a.fxOn && !p.Downed,  // QuarterstaffLook.cs
                                                      MeleeColor = new Rgba(1f, 0.88f, 0.54f),
                                                      Rest = (Player p, ref float ang, ref float reach, ref float bend) => { ang -= 0.3f; reach *= 0.85f; return true; } });
            L(WeaponId.Lightbringer, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldLightbringer(p, t, hold),
                                                      Arms = (a, p) => true, Pauldrons = true,  // LightbringerLook.cs
                                                      MeleeColor = new Rgba(1f, 0.94f, 0.62f) });
            L(WeaponId.Censer, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldCenser(p, t, hold),
                                                Arms = (a, p) => a.fxOn && !p.Downed, Pauldrons = true,  // CenserLook.cs
                                                MeleeColor = new Rgba(1f, 0.8f, 0.5f) });
            L(WeaponId.TwinBlades, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldTwinBlades(p, t, hold),
                                                    Hold = 2,
                                                    Arms = (a, p) => true, Pauldrons = true,  // TwinBladesLook.cs
                                                    MeleeColor = new Rgba(1f, 0.82f, 0.48f) });
            L(WeaponId.Greatsword, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldGreatsword(p, t, hold),
                                                    Hold = 1, HoldOffX = -8f,
                                                    Arms = (a, p) => a.fxOn && !p.Downed,  // GreatswordLook.cs
                                                    MeleeColor = new Rgba(0.78f, 0.88f, 1f),  // Moonlit Crescent (Docs/Mockups/greatsword-sweep.html)
                                                    Rest = (Player p, ref float ang, ref float reach, ref float bend) => { ang -= 0.45f; bend = 1f; return p.FangOut == 0; } });  // no rest while the Moonwheel is thrown
            L(WeaponId.Rapier, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldRapier(p, t, hold),
                                                Arms = (a, p) => a.fxOn && !p.Downed,  // MeleeLooks.cs
                                                MeleeColor = new Rgba(0.8f, 0.72f, 1f),
                                                Rest = (Player p, ref float ang, ref float reach, ref float bend) => { ang -= 0.2f; reach = 78f; return true; } });
            // ---- Ninja
            L(WeaponId.Kusarigama, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldKusarigama(p, t, hold),
                                                    Arms = (a, p) => a.fxOn && !p.Downed,  // MeleeLooks.cs
                                                    MeleeColor = new Rgba(0.7f, 0.64f, 1f),
                                                    Rest = (Player p, ref float ang, ref float reach, ref float bend) => { ang -= 0.35f; reach = 36f; return true; } });  // the sickle in hand, its chain in
            L(WeaponId.BoStaff, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldBoStaff(p, t, hold),
                                                 Hold = 1, HoldOffX = 10f,
                                                 Arms = (a, p) => a.fxOn && !p.Downed,  // BoStaffLook.cs
                                                 MeleeColor = new Rgba(0.78f, 0.9f, 0.62f) });
            L(WeaponId.FumaShuriken, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldFumaShuriken(p, t, hold) });
            L(WeaponId.AcupunctureNeedles, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldAcupunctureNeedles(p, t, hold) });
            L(WeaponId.SpiritFan, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldSpiritFan(p, t, hold) });
            L(WeaponId.Ofuda, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldOfuda(p, t, hold) });
            L(WeaponId.Shuriken, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldShuriken(p, t, hold) });
            L(WeaponId.Katana, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldKatana(p, t, hold),
                                                Arms = (a, p) => a.fxOn && !p.Downed,  // KatanaLook.cs, MeleeLooks.cs
                                                MeleeColor = new Rgba(0.745f, 0.87f, 1f),  // Moon Steel (Docs/Mockups/katana-reach.html)
                                                Rest = (Player p, ref float ang, ref float reach, ref float bend) => { ang -= p.KatanaStance == 0 ? 1.15f : 0.12f; reach *= p.KatanaStance == 0 ? 0.72f : 0.78f; bend = 0.4f; return true; } });  // Hayate: low and out to the side; Senbon: forward (KatanaLook.cs)
            L(WeaponId.Kunai, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldKunai(p, t, hold) });
            // ---- Hemomancer
            L(WeaponId.BloodLash, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldBloodLash(p, t, hold),
                                                   Arms = (a, p) => a.fxOn && !p.Downed,  // MeleeLooks.cs
                                                   MeleeColor = new Rgba(1f, 0.32f, 0.38f),
                                                   Rest = (Player p, ref float ang, ref float reach, ref float bend) => { ang -= 0.55f; reach = 52f; return true; } });  // the whip hanging slack beside you
            L(WeaponId.GoreMortar, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldGoreMortar(p, t, hold),
                                                    Hold = 3 });
            L(WeaponId.LeechTendrils, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldLeechTendrils(p, t, hold) });
            L(WeaponId.TransfusionDarts, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldTransfusionDarts(p, t, hold) });
            L(WeaponId.SanguineOrb, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldSanguineOrb(p, t, hold),
                                                     Hold = 3 });
            L(WeaponId.VeinTether, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldVeinTether(p, t, hold) });
            L(WeaponId.NeedleSwarm, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldNeedleSwarm(p, t, hold) });
            L(WeaponId.Implosion, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldImplosion(p, t, hold),
                                                   Hold = 3 });
            L(WeaponId.BloodBrawler, new WeaponLook { Held = (a, p, t, hold) => a.DrawBsHand(t),  // an open, bleeding hand (BloodBrawlerLook.cs)
                                                      Hold = 3 });
            // ---- Arcanist
            L(WeaponId.OrbitShards, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldArcGlyph(p, t, hold),  // the Arcanist casts from an open palm (ArcanistLook.cs)
                                                     Hold = 3 });
            L(WeaponId.DancingBlades, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldArcGlyph(p, t, hold),  // the Arcanist casts from an open palm (ArcanistLook.cs)
                                                       Hold = 3 });
            L(WeaponId.GlyphStylus, new WeaponLook { Held = (a, p, t, hold) => a.DrawHeldGlyphStylus(p, t, hold) });  // ArcanistLook.cs
            // ---- No held model of their own (their looks draw the weapon)
            L(WeaponId.ArcKnuckles, new WeaponLook { Arms = (a, p) => true });  // ArcKnucklesLook.cs
            L(WeaponId.Nunchaku, new WeaponLook { Arms = (a, p) => !p.Downed });  // NunchakuLook.cs
            L(WeaponId.Nodachi, new WeaponLook { Arms = (a, p) => !p.Downed });  // NodachiLook.cs
            L(WeaponId.Vowguard, new WeaponLook { Arms = (a, p) => !p.Downed, Pauldrons = true });  // VowguardLook.cs
            return weaponLooks;
        }
    }
}
