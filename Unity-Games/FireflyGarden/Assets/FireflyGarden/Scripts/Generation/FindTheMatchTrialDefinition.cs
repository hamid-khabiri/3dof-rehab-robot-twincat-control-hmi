using System;
using FireflyGarden.Configuration;

namespace FireflyGarden.Generation
{
    public enum TargetSlotId { A, B }

    [Serializable]
    public sealed class FindTheMatchTrialDefinition
    {
        public int TrialIndexZeroBased { get; }
        public string TrialId { get; }
        public StimulusDefinition CorrectStimulus { get; }
        public StimulusDefinition NonMatchingStimulus { get; }
        public TargetSlotId CorrectTargetSlot { get; }
        public TargetPositionDefinition TargetAPosition { get; }
        public TargetPositionDefinition TargetBPosition { get; }

        public FindTheMatchTrialDefinition(int index, string id, StimulusDefinition correct,
            StimulusDefinition nonMatching, TargetSlotId correctSlot,
            TargetPositionDefinition positionA, TargetPositionDefinition positionB)
        {
            TrialIndexZeroBased = index; TrialId = id; CorrectStimulus = correct;
            NonMatchingStimulus = nonMatching; CorrectTargetSlot = correctSlot;
            TargetAPosition = positionA; TargetBPosition = positionB;
        }
    }
}
