// TEMPLATE for a new class (see "Adding a class" in HANDOFF.md). Copy to Assets/Scripts/Core/<YourClass>.cs, replace
// MyClass / MyWeapon* / MyAbility* / MyUlt* with real names, and fill in every TODO. Not compiled where it is.
using System;
using System.Collections.Generic;

namespace Arena
{
    /// TODO: one line on what the class is about.
    public partial class Game
    {
        /// The class's hooks into the game (Classes.cs explains each). Leave out the ones it doesn't need.
        internal static readonly KitDef MyClassKit = new KitDef
        {
            Id = Kit.MyClass, Name = "My Class", Icon = "class_myclass", HpMult = 1f, SpeedMult = 1f,
            Blurb = "TODO: one line for the lobby and the Field guide. Full health.",
            Fire = (g, p, aim, dir, muzzle, dmg, extra) => g.FireMyClassWeapon(p, aim, dir, muzzle, dmg, extra),
            Ability = (g, p, id, target) => g.MyClassAbility(p, id, target),
            Tick = (g, dt) => g.TickMyClass(dt),
            // TickEnemy = (g, e, dt) => ...,       // a mark or status it puts on enemies
            // OnShotHit = (g, b, e, hit) => ...,   // runs for every player shot that hits: check b.Kind or b.Owner first
            // OnKill = (g, e) => ...,
        };

        // --- Numbers (Balance.cs lists where each class keeps its own) ---
        const float MyAbilityTime = 4f;

        /// Three weapons per role. Speed × Life is a shot's range (Reach for a swing or beam); the aim line, the bots
        /// and the Armory read these. ExtraDesc is what each extra projectile (Multishot) does for it.
        internal static readonly WeaponDef[] MyClassWeapons =
        {
            // Tank
            new WeaponDef { Id = WeaponId.MyWeaponTank1, Kit = Kit.MyClass, Role = Role.Tank, Choice = 0, Speed = 600f, Life = 0.8f, Damage = 10f,
                FireInterval = 0.3f, Name = "TODO", Desc = "TODO", ExtraDesc = "+1 shot, fanned out", Sound = Sfx.ShotRifle },
            // TODO: Choice 1 and 2 for the Tank, then three each for Role.Healer and Role.Dps (nine in all).
            // Melee = true and Reach for a swing; AreaDesc for damage that lands in an area (Homing Rounds widen it);
            // Companion / CompanionCount for a weapon that brings a minion; BotRange for where bots like to stand.
        };

        /// Three Space abilities per role (Choice 0-2) and one ultimate each (Choice = AbilityDef.Ult).
        internal static readonly AbilityDef[] MyClassAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.MyAbilityTank1, Kit = Kit.MyClass, Role = Role.Tank, Choice = 0, Instant = true,
                Name = "TODO", Cooldown = 12f, Desc = "TODO", Capstone = "TODO: what mastering it (level 5) adds",
                Bot = b => b.Danger,  // when the smart bot casts it (BotSense, BotBrain.cs)
            },
            new AbilityDef
            {
                Id = AbilityId.MyUltTank, Kit = Kit.MyClass, Role = Role.Tank, Choice = AbilityDef.Ult,
                Name = "TODO", Cooldown = 75f, Desc = "TODO", Capstone = "TODO",
            },
            // TODO: the other Space abilities and ultimates (12 in all).
        };

        /// Fires every weapon in MyClassWeapons. dmg already includes the player's damage upgrades; extra is the
        /// number of extra projectiles (Multishot, Artillery Assault), which each weapon upgrades in its own way.
        void FireMyClassWeapon(Player p, float aim, V2 dir, V2 muzzle, float dmg, int extra)
        {
            var w = p.Weapon;
            switch (w.Id)
            {
                case WeaponId.MyWeaponTank1:
                    for (int i = 0; i <= extra; i++)
                        SpawnBullet(Defs.TankShot, muzzle, V2.FromAngle(aim + (i - extra / 2f) * 0.12f) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                    break;
                // TODO: the other eight.
            }
        }

        /// Casts the class's abilities, ultimates included. castMastered is true at mastery level 5 (the capstone),
        /// castPower scales with mastery. Return false for an id that isn't this class's.
        bool MyClassAbility(Player p, AbilityId id, V2 target)
        {
            bool cap = castMastered;
            float pw = castPower;
            switch (id)
            {
                case AbilityId.MyAbilityTank1:
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 1f, 1f), 120f, 0.4f, Sfx.Ability);
                    return true;
                case AbilityId.MyUltTank:
                    return true;
                // TODO: the rest.
            }
            return false;
        }

        /// Host only, every tick: the class's timers and lingering effects.
        void TickMyClass(float dt)
        {
        }
    }
}
