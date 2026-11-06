using System;
using System.Collections.Generic;

namespace Arena
{
    /// The lobby's loadout preview: a private, offline copy of the real game (nothing is sent anywhere) where
    /// your character uses the chosen weapon on a few target dummies, and casts an ability on a loop while
    /// the lobby asks for it. Because it runs the real game code, every weapon and ability looks exactly
    /// as it does in a match.
    public class Showcase
    {
        public const float AbilityEvery = 3.5f;  // seconds between demo casts (cooldowns are skipped)
        public const float UltimateEvery = 9f;   // ...and between ultimate casts (most last several seconds)
        const float FirstCast = 1.2f;            // the dummies are up by then (Mark for Death needs a target)

        readonly Session session = new Session();
        public Game Game => session.Game;
        public Player Me => Game.Me;

        Role role;
        Kit kit;
        int weapon = -1;
        AbilityId ability = AbilityId.None;
        bool demoAbility, ultimate;
        float t, castT;
        bool castHeld;
        bool allies, reviveDemo, prepped;  // teammates present; a downed one is part of the demo; they're hurt for the next cast
        int castCount, aimTicks;
        const float PrepBefore = 0.9f;     // teammates get hurt this long before a cast, so the heal is seen

        static readonly Player probe = new Player { Hp = 100f, MaxHp = 100f };

        /// Does this ability act on teammates (heals, shields, buffs them)? Read from the bot's rule for it: it
        /// fires when an ally is hurt or down, but not when nobody is. Adrenaline Shot picks a player by cursor.
        public static bool TargetsAlly(AbilityId id)
        {
            if (id == AbilityId.None)
                return false;
            if (id == AbilityId.Adrenaline)
                return true;
            var bot = Upgrades.Ability(id).Bot;
            if (bot == null)
                return false;
            var calm = new BotSense { Me = probe, Players = 3, HaveTarget = true, TargetD = 300f };
            var hurt = calm;
            hurt.AllyHurt = hurt.AllyDown = true;
            return !bot(calm) && bot(hurt);
        }

        /// Does it only matter when a teammate is down (a revive)?
        static bool RevivesAlly(AbilityId id)
        {
            var bot = Upgrades.Ability(id).Bot;
            if (bot == null)
                return false;
            var calm = new BotSense { Me = probe, Players = 3, TargetD = 9999f };
            var down = calm;
            down.AllyDown = true;
            return !bot(calm) && bot(down);
        }

        /// Where your character stands; the dummies are straight ahead (up), Distance away.
        public static readonly V2 Home = new V2(Game.Width / 2f, Game.Height / 2f + 150f);
        const float BoStaffDemoEvery = 5.2f, BoStaffDemoShove = 3.3f;  // the Bo Staff demo: two full chains and a third cut short by the shove
        public float Distance { get; private set; } = 250f;

        /// How far ahead the view should reach: the dummies' distance, growing to fit when a weapon
        /// shoves them further (the Lance), so the preview can zoom out to keep them in view.
        public float Extent { get; private set; } = 250f;

        /// Show this loadout. Anything new (weapon, role, class, the ability shown, or switching to the
        /// ultimate) restarts the scene, so nothing from the last demo plays on. demo: cast the ability every
        /// few seconds; ultimate: cast your ultimate (F) instead.
        public void Show(Role role, Kit kit, int weapon, AbilityId ability, bool demo, bool ultimate = false)
        {
            if (!Game.Running || role != this.role || kit != this.kit || weapon != this.weapon || ability != this.ability || ultimate != this.ultimate)
            {
                this.role = role;
                this.kit = kit;
                this.weapon = weapon;
                this.ability = ability;
                this.ultimate = ultimate;
                var shown = ultimate ? Upgrades.Ultimate(role, kit) : ability;
                allies = TargetsAlly(shown);
                reviveDemo = allies && (shown == AbilityId.MendPulse || RevivesAlly(shown));
                prepped = false;
                castCount = 0;
                aimTicks = 0;
                session.HostOffline(role, kit, weapon, 0, allies ? 2 : 0);
                Game.ShowcaseMode = true;
                Game.HostStart();
                Game.ShowcaseStart(ability);
                float reach = Game.WeaponRange(Me);
                Distance = Me.Weapon.Melee ? reach * 0.75f : Math.Min(reach * 0.8f, Math.Max(200f, Math.Min(320f, reach * 0.55f)));
                Game.ShowcaseDummies(Home, Distance);
                Extent = Distance;
                t = 0f;
                castT = FirstCast;
            }
            if (demo && !demoAbility)
                castT = Math.Min(castT, Math.Max(0.3f, FirstCast - t));
            demoAbility = demo;
        }

        public void Step(float dt)
        {
            if (!Game.Running || Me == null)
                return;
            t += dt;
            var me = Me;
            var input = new PlayerInput { AimAt = Home + new V2(0f, -Distance) };
            if (allies && demoAbility)
            {
                if (!prepped && castT <= PrepBefore)
                {
                    Game.ShowcaseHurtAllies(reviveDemo && (castCount % 2 == 0 || ultimate));
                    prepped = true;
                }
                Game.ShowcaseHoldAllies();
            }
            // Walk back home after a dash or leap.
            if (me.Pos.DistanceTo(Home) > 10f && Game.DashT <= 0f)
                input.Move = (Home - me.Pos).Normalized();
            // The bow draws and lets go; everything else just holds the trigger.
            input.Fire = !Game.ChargeWeapon(me.Weapon.Id) || MathX.Fmod(t, 1.3f) < 1.05f;
            // The Bo Staff: fire runs the slash, jab, spin chain; every few seconds the weapon-action key shoves, partway into a chain so the
            // reset shows (BoStaff.cs).
            if (me.Weapon.Id == WeaponId.BoStaff)
            {
                float phase = MathX.Fmod(t, BoStaffDemoEvery);
                input.WeaponAction = phase >= BoStaffDemoShove && phase - dt < BoStaffDemoShove;
            }
            if (demoAbility)
            {
                castT -= dt;
                if (castHeld)
                    castHeld = false;
                else if (castT <= 0f)
                {
                    Game.ShowcaseRefill(ultimate ? Upgrades.UltSlot : 0);
                    castHeld = true;
                    castT = ultimate ? UltimateEvery : AbilityEvery;
                    prepped = false;
                    castCount++;
                    aimTicks = 4;  // (held abilities cast on release, so the cursor stays put a few ticks)
                }
                if (ultimate)
                    input.Ultimate = castHeld;
                else
                    input.Ability = castHeld;
            }
            if (allies && aimTicks > 0)
            {
                input.AimAt = Game.ShowcaseAllyPos(castCount % 2);  // the cursor on a teammate (Adrenaline Shot, Medkit, Healing Rain...), alternating
                aimTicks--;
            }
            Game.FixedStep(dt, input);
            Game.Update(dt);
            float far = Distance;
            foreach (var e in Game.Enemies.Values)
                far = Math.Max(far, Home.DistanceTo(e.Pos) + e.Radius);
            if (far > Extent)
                Extent += (far - Extent) * Math.Min(1f, dt * 4f);
            Game.Sounds.Clear();  // the preview is silent
        }

        public void Stop() => session.Leave("");
    }

    /// The Game side of the loadout preview (Showcase): target dummies instead of waves.
    public partial class Game
    {
        public bool ShowcaseMode;
        readonly List<V2> dummySpots = new List<V2>();
        readonly List<int> dummyIds = new List<int>();
        readonly List<float> dummyRespawn = new List<float>();
        const float DummyHp = 260f, DummyRespawn = 1f, DummyReturn = 320f;

        public void ShowcaseStart(AbilityId ability)
        {
            State = MatchState.Fighting;
            lastState = State;
            if (Me != null)
                Me.Pos = Me.Disp = Showcase.Home;
            ShowcaseSetAbility(ability);
            DevSetGod(MyId, true);
            // Stand-in teammates (Showcase.TargetsAlly): either side of you, apart so they can't revive each other.
            int k = 0;
            foreach (var p in Players.Values)
                if (p.Id != MyId)
                {
                    p.Pos = p.Disp = AllySpot(k++);
                    DevSetGod(p.Id, true);
                }
        }

        static V2 AllySpot(int k) => Showcase.Home + new V2(k == 0 ? -130f : 130f, 35f);

        List<Player> ShowcaseAllies()
        {
            var list = new List<Player>();
            foreach (var p in Players.Values)
                if (p.Id != MyId)
                    list.Add(p);
            return list;
        }

        public V2 ShowcaseAllyPos(int k)
        {
            var a = ShowcaseAllies();
            return k < a.Count ? a[k].Pos : Showcase.Home;
        }

        /// Hurt the teammates (35% health) so a heal shows; with `down`, the second one is knocked down for a revive.
        public void ShowcaseHurtAllies(bool down)
        {
            var a = ShowcaseAllies();
            for (int k = 0; k < a.Count; k++)
            {
                var p = a[k];
                if (p.Downed)
                    RevivePlayer(p, 0.1f);
                p.Hp = p.MaxHp * 0.35f;
                if (down && k == 1)
                {
                    p.Hp = 0f;
                    p.Downed = true;
                    p.Revive = 0f;
                }
            }
        }

        /// Keep the teammates where they stand, and nobody walks over to revive them on their own.
        public void ShowcaseHoldAllies()
        {
            int k = 0;
            foreach (var p in ShowcaseAllies())
            {
                p.Pos = p.Disp = AllySpot(k++);
                if (p.Downed)
                    p.Revive = 0f;
            }
        }

        public void ShowcaseSetAbility(AbilityId ability)
        {
            var me = Me;
            if (me == null)
                return;
            me.RoleAbility = ability;
            Recalc(me);
        }

        /// Three dummies in a shallow arc, `distance` straight up from `home`.
        public void ShowcaseDummies(V2 home, float distance)
        {
            dummySpots.Clear();
            dummyIds.Clear();
            dummyRespawn.Clear();
            for (int k = -1; k <= 1; k++)
            {
                dummySpots.Add(home + V2.FromAngle(-MathX.Pi / 2f + k * 0.42f) * distance);
                dummyIds.Add(0);
                dummyRespawn.Add(0.2f + 0.15f * (k + 1));
            }
        }

        /// The demo cast (Space, or the ultimate's slot) comes off cooldown straight away.
        public void ShowcaseRefill(int slot = 0)
        {
            var me = Me;
            if (me == null)
                return;
            me.Slots[slot].Charges = me.Slots[slot].MaxCharges;
            me.Slots[slot].Timer = 0f;
        }

        /// Instead of waves: keep the dummies standing (a dead one comes back a moment later).
        void TickShowcase(float dt)
        {
            State = MatchState.Fighting;
            for (int i = 0; i < dummySpots.Count; i++)
            {
                Enemy alive;
                if (dummyIds[i] != 0 && Enemies.TryGetValue(dummyIds[i], out alive))
                {
                    // Knocked back (Lance, Bulldoze...): walk back to its spot.
                    V2 back = dummySpots[i] - alive.Pos;
                    float step = DummyReturn * dt;
                    alive.Pos = back.Length <= step ? dummySpots[i] : alive.Pos + back.Normalized() * step;
                    continue;
                }
                dummyIds[i] = 0;
                if ((dummyRespawn[i] -= dt) > 0f)
                    continue;
                var e = SpawnEnemyAt(EnemyType.Grunt, dummySpots[i], 0.5f, 0);
                e.MaxHp = e.Hp = DummyHp;
                dummyIds[i] = e.Id;
                dummyRespawn[i] = DummyRespawn;
            }
            Orbs.Clear();
            Remaining = Enemies.Count;
        }
    }
}
