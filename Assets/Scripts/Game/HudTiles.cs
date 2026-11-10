using UnityEngine;

namespace Arena
{
    /// The ability bar's tiles react ("Snap", option A of Docs/Mockups/ability-tiles.html), on this screen only:
    ///   ready      a cooldown finishes: the tile pops up 8% and settles, its rim flashes white, READY slides in
    ///   charge     a second charge comes back: the new pip pops with a halo, the tile nods
    ///   use        a charge is spent: the tile presses in to 93% and springs back
    ///   too early  you press while it's recharging: a short side-to-side shake, the rim and the timer flash red
    ///   ultimate   its ready pop is bigger (12%) and longer, in gold
    /// The rim ("Rim runner", look D of Docs/Mockups/hud-animations.html): recharging draws the border round clockwise from
    /// the top left as a loading ring; a ready tile has a spark with a tail circling it; using it snuffs the rim out with a
    /// small burst at the key cap. Reduced effects: the ring only.
    /// Ready, charge and use come from each slot's Charges compared with last frame's; too early from the key press
    /// (ArenaApp.Update), which otherwise does nothing. Reduced effects: half the pop, no shake, no white flash.
    /// Visual effects off: nothing, the bar draws exactly as before.
    public partial class ArenaApp
    {
        const float TileReadyTime = 0.3f, TileUltReadyTime = 0.4f, TileChargeTime = 0.35f, TileUseTime = 0.2f, TileDenyTime = 0.32f;
        const float TileNever = 99f;

        readonly int[] tileCharges = new int[Upgrades.SlotCount];
        readonly AbilityId[] tileIds = new AbilityId[Upgrades.SlotCount];
        readonly float[] tileReady = Filled(TileNever), tileCharge = Filled(TileNever), tileUse = Filled(TileNever), tileDeny = Filled(TileNever);
        readonly int[] tilePip = new int[Upgrades.SlotCount];  // which pip the last charge came back into
        int tilesFor = -1;

        static float[] Filled(float v)
        {
            var a = new float[Upgrades.SlotCount];
            for (int i = 0; i < a.Length; i++)
                a[i] = v;
            return a;
        }

        /// Each frame (Update): age the moments and look for new ones in your slots.
        void TickTiles(float dt)
        {
            var me = game != null ? game.Me : null;
            for (int i = 0; i < Upgrades.SlotCount; i++)
            {
                tileReady[i] += dt; tileCharge[i] += dt; tileUse[i] += dt; tileDeny[i] += dt;
            }
            if (me == null)
            {
                tilesFor = -1;
                return;
            }
            bool fresh = tilesFor != me.Id;
            tilesFor = me.Id;
            for (int i = 0; i < Upgrades.SlotCount; i++)
            {
                var s = me.Slots[i];
                // A new player, or the slot holds another ability (moved in the Tab loadout, a new pick): no moment.
                if (!fresh && s.Id == tileIds[i] && s.Id != AbilityId.None && fxOn)
                {
                    if (s.Charges > tileCharges[i])
                    {
                        if (tileCharges[i] == 0)
                            tileReady[i] = 0f;
                        else
                        {
                            tileCharge[i] = 0f;
                            tilePip[i] = s.Charges - 1;
                        }
                    }
                    else if (s.Charges < tileCharges[i])
                        tileUse[i] = 0f;
                }
                tileIds[i] = s.Id;
                tileCharges[i] = s.Charges;
            }
        }

        /// A key for slot i was pressed: if it's still recharging, the tile shakes it off.
        void TileKeyPressed(int i)
        {
            if (!fxOn || game == null || session.Screen != AppScreen.Game || game.State != MatchState.Fighting && game.State != MatchState.Intermission)
                return;
            var me = game.Me;
            if (me == null || me.Downed)
                return;
            var s = me.Slots[i];
            if (s.Id == AbilityId.None || s.Charges > 0 || s.Id == AbilityId.StunGun && me.StunShots > 0)
                return;  // (the Stun Gun fires its shots while its cooldown waits)
            tileDeny[i] = 0f;
        }

        /// How tile i moves and what flashes on it this frame.
        struct TileLook
        {
            public float Scale, Dx;        // around the tile's centre
            public float White, Red;       // rim flashes (0..1)
            public float ReadyIn;          // READY sliding in: 0 just started .. 1 in place
            public int Pip;                // the pip that popped (-1 none)
            public float PipPop, PipHalo;  // its extra size, and its halo's spread 0..1
        }

        TileLook TileLookFor(int i, bool ultimate)
        {
            var l = new TileLook { Scale = 1f, ReadyIn = 1f, Pip = -1 };
            if (!fxOn)
                return l;
            float half = fxReduced ? 0.5f : 1f;
            float readyLen = ultimate ? TileUltReadyTime : TileReadyTime;
            if (tileReady[i] < readyLen)
            {
                float k = tileReady[i] / readyLen;
                l.Scale = 1f + (ultimate ? 0.12f : 0.08f) * half * Mathf.Sin(Mathf.PI * EaseOut3(k));
            }
            if (tileReady[i] < TileReadyTime)
                l.ReadyIn = EaseOut3(tileReady[i] / TileReadyTime);
            if (tileReady[i] < 0.25f && !fxReduced)
                l.White = 1f - tileReady[i] / 0.25f;
            if (tileCharge[i] < TileChargeTime)
            {
                float k = tileCharge[i] / TileChargeTime;
                l.Pip = tilePip[i];
                l.PipPop = 1.2f * (1f - EaseOut3(k));
                l.PipHalo = k;
                l.Scale = Mathf.Max(l.Scale, 1f + 0.03f * half * Mathf.Sin(Mathf.PI * k));
            }
            if (tileUse[i] < TileUseTime)
            {
                // In to 93% over the first third, then springs back with a little overshoot.
                float k = tileUse[i] / TileUseTime;
                l.Scale = k < 0.35f ? 1f - 0.07f * half * (k / 0.35f) : 1f - 0.07f * half * (1f - EaseOutBack((k - 0.35f) / 0.65f));
            }
            if (tileDeny[i] < TileDenyTime)
            {
                float k = tileDeny[i] / TileDenyTime;
                if (!fxReduced)
                    l.Dx = Mathf.Sin(k * Mathf.PI * 6f) * 5f * (1f - k);
                l.Red = 1f - k;
            }
            return l;
        }

        static float EaseOut3(float k) => 1f - (1f - k) * (1f - k) * (1f - k);

        static float EaseOutBack(float k)
        {
            const float c = 1.70158f;
            float q = k - 1f;
            return 1f + (c + 1f) * q * q * q + c * q * q;
        }

        /// Draw what `draw` draws scaled by s around `centre` and shifted dx, on top of whatever GUI.matrix is in force
        /// (the HUD's own slide-in included).
        static void TileTransformed(Vector2 centre, float s, float dx, System.Action draw)
        {
            if (s == 1f && dx == 0f)
            {
                draw();
                return;
            }
            var old = GUI.matrix;
            float sc = old.m00, tx = old.m03, ty = old.m13;
            GUI.matrix = Matrix4x4.TRS(new Vector3(tx + (centre.x * (1f - s) + dx) * sc, ty + centre.y * (1f - s) * sc, 0f), Quaternion.identity, new Vector3(sc * s, sc * s, 1f));
            draw();
            GUI.matrix = old;
        }

        /// For the preview tool: start a moment on every slot, `ago` seconds back.
        void PreviewTileMoment(string moment, float ago)
        {
            for (int i = 0; i < Upgrades.SlotCount; i++)
            {
                if (moment == "ready") tileReady[i] = ago;
                else if (moment == "charge") { tileCharge[i] = ago; tilePip[i] = 1; }
                else if (moment == "use") tileUse[i] = ago;
                else if (moment == "deny") tileDeny[i] = ago;
            }
        }
    }
}
