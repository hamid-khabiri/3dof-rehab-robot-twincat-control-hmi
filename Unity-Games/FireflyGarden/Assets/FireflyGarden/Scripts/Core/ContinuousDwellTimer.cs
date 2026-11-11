using UnityEngine;

namespace FireflyGarden.Core
{
    public sealed class ContinuousDwellTimer
    {
        public float ElapsedTime { get; private set; }

        public float GetNormalizedProgress(float duration)
        {
            return duration > 0f ? Mathf.Clamp01(ElapsedTime / duration) : 0f;
        }

        public bool Advance(float deltaTime, float duration)
        {
            if (deltaTime > 0f)
            {
                ElapsedTime += deltaTime;
            }

            return duration > 0f && ElapsedTime >= duration;
        }

        public void Reset()
        {
            ElapsedTime = 0f;
        }
    }
}
