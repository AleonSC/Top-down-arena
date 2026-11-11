using System;
using System.Collections.Generic;

namespace Arena
{
    /// How the lobby's loadout preview (Showcase.cs) presses a weapon's Alt (PlayerInput.WeaponAction): one entry per weapon with a
    /// secondary, so the preview shows it too. An entry gets the preview's clock and says whether Alt is held this tick; it may also
    /// stage what the Alt needs (loaded grenades, a mark...). To preview a new weapon's Alt, add a line to Table and nothing else.
    public static class ShowcaseAlt
    {
        public delegate bool Demo(Game game, Player me, float t, float dt);

        /// True on the single tick when the clock passes `at` seconds into each `period`: a tap of the key.
        public static bool Tap(float t, float dt, float period, float at)
        {
            float p = MathX.Fmod(t, period);
            return p >= at && p - dt < at;
        }

        /// True while the clock is within `len` seconds after `at` into each `period`: the key held down.
        public static bool Hold(float t, float period, float at, float len)
        {
            float p = MathX.Fmod(t, period);
            return p >= at && p < at + len;
        }

        public const float BoStaffEvery = 5.2f, BoStaffShove = 3.3f;  // two full chains and a third cut short by the shove (BoStaff.cs)
        public const float TwinNadeEvery = 1.6f, TwinNadeAt = 0.8f;   // a grenade every 1.6 s, the tubes taking turns (TwinGrenades.cs)

        static readonly Dictionary<WeaponId, Demo> Table = new Dictionary<WeaponId, Demo>
        {
            // The Bo Staff: the shove, partway into a chain so the reset shows.
            { WeaponId.BoStaff, (g, me, t, dt) => Tap(t, dt, BoStaffEvery, BoStaffShove) },
            // The Twin Rifle: hits would load the tubes (60 of them each), too slow for a preview, so an empty pair is refilled just before the press.
            { WeaponId.TwinRifle, (g, me, t, dt) =>
                {
                    bool tap = Tap(t, dt, TwinNadeEvery, TwinNadeAt);
                    if (tap && me.TwinNades == 0)
                        me.TwinNades = (byte)Game.TwinNadeMax;
                    return tap;
                } },
            // The Twin Rifle Auto: lock on, and let go again a few seconds later (a dummy under the cursor is locked if one is near; TwinAuto.cs).
            { WeaponId.TwinRifleAuto, (g, me, t, dt) => Tap(t, dt, 4f, 1f) || Tap(t, dt, 4f, 3f) },
        };

        public static bool Has(WeaponId id) => Table.ContainsKey(id);

        /// Is Alt held this tick for this weapon?
        public static bool Held(Game game, Player me, float t, float dt) =>
            Table.TryGetValue(me.Weapon.Id, out var demo) && demo(game, me, t, dt);
    }
}
