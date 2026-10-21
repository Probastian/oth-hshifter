using System;
using System.Collections.Generic;
using UnityEngine;

namespace HShifter
{
    internal enum GearRange { None = 0, High = 1, Low = 2 }

    /// <summary>A position in the H-gate: a gear (0 = neutral, +n forward, -n reverse) plus the transfer-case range it implies.</summary>
    internal readonly struct Slot
    {
        public readonly int Gear;
        public readonly GearRange Range;
        public Slot(int gear, GearRange range) { Gear = gear; Range = range; }

        public static readonly Slot Neutral = new Slot(0, GearRange.None);
        public bool IsNeutral => Gear == 0;
        public bool Exists => Gear != 0;

        public string Label
        {
            get
            {
                if (Gear == 0) return "N";
                if (Gear < 0) return Gear == -1 ? "R" : "R" + (-Gear);
                return Gear + (Range == GearRange.High ? "H" : Range == GearRange.Low ? "L" : "");
            }
        }
    }

    /// <summary>
    /// Pure H-pattern gate with a physical lever (no game dependencies).
    ///
    /// Geometry: column i sits at x = i (column units), the neutral channel is the line y = 0 from the first to the last column,
    /// each column is a rail along y in [-1, 1] (gate units) and the gear slots are the rail ends (y = +1 up, y = -1 down;
    /// a column without a down gear only has the upper half of its rail). On screen one column spacing is
    /// <see cref="ColAspect"/> times as long as one gate unit vertically, and the lever speed is applied in *screen* distance,
    /// so it moves equally fast sideways and up/down.
    ///
    /// The lever is moved relatively: it travels in the direction the stick is pushed and can only travel along the rails.
    /// Crossings are forgiving: a column's rail can be entered from a fair distance away from its centre (the lever snaps onto it),
    /// sliding sideways slows down around column centres so it is easy to stop on one, and from near the channel on a rail a
    /// sideways push switches straight back into the channel. The lever stays where it is when the stick is released and is in N
    /// whenever it is not at a slot.
    /// </summary>
    internal sealed class HGate
    {
        internal struct Column
        {
            public Slot Up;
            public Slot Down;
            public float RailMin => Down.Exists ? -1f : 0f;
            public float RailMax => Up.Exists ? 1f : 0f;
        }

        /// <summary>On-screen column spacing divided by the on-screen height of one gate unit (HUD: 96 px / 54 px).</summary>
        internal const float ColAspect = 96f / 54f;

        private const float OnRailEps = 0.001f;
        private const float EnterTol = 0.38f;      // column units: how far from a column centre its rail can still be entered
        private const float LeaveTol = 0.32f;      // gate units: on a rail this close to the channel, a sideways push leaves the rail
        private const float Detent = 0.30f;        // column units: slow-down zone around column centres when sliding sideways
        private const float DetentSlow = 0.45f;    // speed factor at the very centre of a column
        private const float SnapSpeed = 8f;        // column units per second when the lever snaps onto a rail
        private const float Magnet = 0.38f;        // pull-in range towards a column centre when the stick is not pushed sideways
        private const float MagnetSpeed = 4f;

        internal readonly List<Column> Columns = new List<Column>();
        internal int Forward { get; private set; }
        internal int Reverse { get; private set; }
        internal bool HiLo { get; private set; }
        internal int ColumnCount => Columns.Count;

        /// <summary>Lever position: x in column units, y in gate units (-1 .. +1).</summary>
        internal float LeverX { get; private set; }
        internal float LeverY { get; private set; }
        private float _lastChannelX = -1f;

        /// <summary>Slot the lever is in right now (what will be engaged on clutch release); Neutral anywhere else.</summary>
        internal Slot Desired
        {
            get
            {
                if (Columns.Count == 0) return Slot.Neutral;
                int c = Mathf.Clamp((int)Math.Round(LeverX), 0, Columns.Count - 1);
                if (Mathf.Abs(LeverX - c) > 0.02f) return Slot.Neutral;
                if (LeverY >= 0.999f && Columns[c].Up.Exists) return Columns[c].Up;
                if (LeverY <= -0.999f && Columns[c].Down.Exists) return Columns[c].Down;
                return Slot.Neutral;
            }
        }

        internal void Configure(int forward, int reverse, bool hiLo, bool reverseOnRight)
        {
            Forward = Math.Max(0, forward);
            Reverse = Math.Max(0, reverse);
            HiLo = hiLo;
            Columns.Clear();

            // Low on the left, High in the middle (reverse at the end): [1L 2L] [1H 2H] [R]
            var ranges = hiLo ? new[] { GearRange.Low, GearRange.High } : new[] { GearRange.None };
            var fwd = new List<Column>();
            foreach (var r in ranges)
                for (int g = 1; g <= Forward; g += 2)
                    fwd.Add(new Column
                    {
                        Up = new Slot(g, r),
                        Down = g + 1 <= Forward ? new Slot(g + 1, r) : Slot.Neutral,
                    });

            bool hasRev = Reverse >= 1;
            var rev = new Column
            {
                Up = hasRev ? new Slot(-1, GearRange.None) : Slot.Neutral,
                Down = Reverse >= 2 ? new Slot(-2, GearRange.None) : Slot.Neutral,
            };

            if (hasRev && !reverseOnRight) Columns.Add(rev);
            Columns.AddRange(fwd);
            if (hasRev && reverseOnRight) Columns.Add(rev);
            if (Columns.Count == 0) Columns.Add(default);

            _lastChannelX = (float)Math.Round(Columns.Count > 1 ? (Columns.Count - 1) / 2f : 0f);
            LeverX = _lastChannelX;
            LeverY = 0f;
        }

        /// <summary>Clutch pressed: the lever sits at the slot of the car's current gear (or in the channel for N).</summary>
        internal void Begin(int currentGear, GearRange currentRange)
        {
            if (currentGear != 0)
            {
                for (int i = 0; i < Columns.Count; i++)
                {
                    foreach (var (slot, y) in new[] { (Columns[i].Up, 1f), (Columns[i].Down, -1f) })
                    {
                        if (!slot.Exists || slot.Gear != currentGear) continue;
                        if (slot.Range != GearRange.None && currentRange != GearRange.None && slot.Range != currentRange) continue;
                        LeverX = i;
                        LeverY = y;
                        return;
                    }
                }
            }
            // N (or a gear that is not in the gate): lever in the channel where it was last left
            LeverX = Mathf.Clamp(_lastChannelX, 0f, Columns.Count - 1);
            LeverY = 0f;
        }

        /// <param name="stick">Raw right-stick vector (y up).</param>
        /// <param name="dt">Frame time in seconds.</param>
        /// <param name="speed">Lever speed at full deflection, in gate units per second (screen distance, same sideways and up/down).</param>
        /// <param name="deadzone">Radial stick deadzone.</param>
        internal void Move(Vector2 stick, float dt, float speed, float deadzone)
        {
            float mag = stick.magnitude;
            Vector2 v = Vector2.zero;
            if (mag >= deadzone)
            {
                float k = Mathf.Clamp((mag - deadzone) / Mathf.Max(0.001f, 1f - deadzone), 0f, 1f);
                v = new Vector2(stick.x / mag * k, stick.y / mag * k);
            }
            float stepY = speed * dt;                 // gate units
            float stepX = speed * dt / ColAspect;     // column units - the same distance on screen
            int cols = Columns.Count;

            if (Mathf.Abs(LeverY) > OnRailEps)
            {
                // On a column rail: locked to the column.
                int c = Mathf.Clamp((int)Math.Round(LeverX), 0, cols - 1);
                LeverX = c;

                // Close to the channel and pushed clearly sideways: leave the rail (easy column switching).
                if (Mathf.Abs(LeverY) <= LeaveTol && Mathf.Abs(v.x) > 0.5f && Mathf.Abs(v.x) > 1.2f * Mathf.Abs(v.y))
                {
                    LeverY = 0f;
                    // fall through to the channel logic below
                }
                else
                {
                    float before = LeverY;
                    float y = Mathf.Clamp(LeverY + v.y * stepY, Columns[c].RailMin, Columns[c].RailMax);
                    if (before * y < 0f || Mathf.Abs(y) < OnRailEps) y = 0f;          // reached / crossed the channel
                    LeverY = y;
                    if (LeverY == 0f) _lastChannelX = LeverX;
                    return;
                }
            }

            // In the neutral channel.
            LeverY = 0f;
            int near = Mathf.Clamp((int)Math.Round(LeverX), 0, cols - 1);
            float dist = Mathf.Abs(LeverX - near);

            // Pushed mostly up/down near a column: snap onto its rail and go.
            bool wantsVertical = Mathf.Abs(v.y) > 0f && Mathf.Abs(v.y) >= 0.6f * Mathf.Abs(v.x);
            if (dist <= EnterTol && wantsVertical)
            {
                var col = Columns[near];
                bool railThatWay = (v.y > 0f && col.RailMax > 0f) || (v.y < 0f && col.RailMin < 0f);
                if (railThatWay)
                {
                    LeverX = Mathf.MoveTowards(LeverX, near, SnapSpeed * dt);
                    LeverY = Mathf.Clamp(v.y * stepY, col.RailMin, col.RailMax);
                    _lastChannelX = LeverX;
                    return;
                }
            }

            if (Mathf.Abs(v.x) > 0f)
            {
                // slower around column centres, so it is easy to stop on one
                float factor = dist < Detent ? Mathf.Lerp(DetentSlow, 1f, dist / Detent) : 1f;
                LeverX = Mathf.Clamp(LeverX + v.x * stepX * factor, 0f, cols - 1);
            }
            else if (dist < Magnet && dist > 0f)
            {
                // not pushing sideways: ease towards the nearest column centre so its rail is easy to enter
                LeverX = Mathf.MoveTowards(LeverX, near, MagnetSpeed * dt);
            }
            _lastChannelX = LeverX;
        }
    }
}
