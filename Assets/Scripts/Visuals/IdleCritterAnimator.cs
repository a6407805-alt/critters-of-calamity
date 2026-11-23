using UnityEngine;

namespace CrittersOfCalamity.Visuals
{
    public class IdleCritterAnimator : MonoBehaviour
    {
        public float BaseY;
        public float PhaseOffset;
        public float BobAmplitude = 0.032f;
        public float BobFrequency = 2.0f;

        void Update()
        {
            var p = transform.localPosition;
            p.y = BaseY + Mathf.Sin(Time.time * BobFrequency + PhaseOffset) * BobAmplitude;
            transform.localPosition = p;
        }
    }
}
