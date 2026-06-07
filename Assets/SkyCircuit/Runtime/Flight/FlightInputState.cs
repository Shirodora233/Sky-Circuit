using UnityEngine;

namespace SkyCircuit.Flight
{
    public readonly struct FlightInputState
    {
        public readonly float Throttle;
        public readonly float Turn;
        public readonly float Vertical;
        public readonly Vector2 LookDelta;
        public readonly bool Boost;

        public FlightInputState(float throttle, float turn, float vertical, Vector2 lookDelta, bool boost)
        {
            Throttle = Mathf.Clamp(throttle, -1f, 1f);
            Turn = Mathf.Clamp(turn, -1f, 1f);
            Vertical = Mathf.Clamp(vertical, -1f, 1f);
            LookDelta = lookDelta;
            Boost = boost;
        }
    }
}
