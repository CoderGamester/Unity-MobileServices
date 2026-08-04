using UnityEngine;

namespace GameLovers.MobileServices.Gestures
{
    /// <summary>
    /// A completed tap: where it started and ended, how long it lasted, and how far the finger
    /// drifted while down.
    /// </summary>
    public struct TapInput
    {
        public readonly Vector2 PressPosition;
        public readonly Vector2 ReleasePosition;
        public readonly double TapDuration;
        public readonly float TapDrift;
        public readonly double TimeStamp;

        internal TapInput(ActiveGesture gesture) : this()
        {
            PressPosition = gesture.StartPosition;
            ReleasePosition = gesture.EndPosition;
            TapDuration = gesture.EndTime - gesture.StartTime;
            TapDrift = gesture.TravelDistance;
            TimeStamp = gesture.EndTime;
        }
    }
}
