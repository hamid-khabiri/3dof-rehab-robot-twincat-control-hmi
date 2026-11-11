namespace FireflyGarden.Core
{
    /// <summary>
    /// A one-shot boolean signal: each Trigger produces exactly one FALSE -> TRUE -> FALSE pulse.
    /// The value stays TRUE for <see cref="HighDuration"/> seconds, then FALSE for at least
    /// <see cref="LowDuration"/> seconds, so a receiver polling at a slower rate still sees every
    /// rising edge. Triggers that arrive during a pulse are queued and play out afterwards.
    /// </summary>
    public sealed class BooleanPulse
    {
        private const int MaximumQueuedTriggers = 8;

        private int queuedTriggers;
        private float phaseTimeRemaining;
        private bool inLowGap;

        public BooleanPulse(float highDuration, float lowDuration)
        {
            HighDuration = highDuration > 0f ? highDuration : 0.01f;
            LowDuration = lowDuration > 0f ? lowDuration : 0.01f;
        }

        public float HighDuration { get; }
        public float LowDuration { get; }
        public bool Value { get; private set; }
        public bool IsIdle => !Value && !inLowGap && queuedTriggers == 0;

        public void Trigger()
        {
            if (queuedTriggers < MaximumQueuedTriggers) queuedTriggers++;
            if (!Value && !inLowGap) StartNextPulse();
        }

        /// <summary>Advances the pulse by real elapsed time and returns the value to output now.</summary>
        public bool Tick(float deltaTime)
        {
            if (deltaTime < 0f) deltaTime = 0f;
            if (Value)
            {
                phaseTimeRemaining -= deltaTime;
                if (phaseTimeRemaining <= 0f)
                {
                    Value = false;
                    inLowGap = true;
                    phaseTimeRemaining = LowDuration;
                }
            }
            else if (inLowGap)
            {
                phaseTimeRemaining -= deltaTime;
                if (phaseTimeRemaining <= 0f)
                {
                    inLowGap = false;
                    StartNextPulse();
                }
            }
            return Value;
        }

        public void Reset()
        {
            queuedTriggers = 0;
            phaseTimeRemaining = 0f;
            inLowGap = false;
            Value = false;
        }

        private void StartNextPulse()
        {
            if (queuedTriggers == 0) return;
            queuedTriggers--;
            Value = true;
            phaseTimeRemaining = HighDuration;
        }
    }
}
