using System;
using FireflyGarden.Configuration;
using UnityEngine;

namespace FireflyGarden.Generation
{
    [Serializable]
    public sealed class SingleFlowerTrialDefinition
    {
        public int TrialIndexZeroBased { get; }
        public string TrialId { get; }
        public string PositionId { get; }
        public SingleFlowerDirection Direction { get; }
        public TargetDistanceBand DistanceBand { get; }
        public float AngleDegrees { get; }
        public Vector2 LocalOffsetFromHome { get; }

        public SingleFlowerTrialDefinition(int index, string trialId, TargetPositionDefinition position)
        {
            TrialIndexZeroBased = index;
            TrialId = trialId;
            PositionId = position.PositionId;
            Direction = position.Direction;
            DistanceBand = position.DistanceBand;
            AngleDegrees = position.AngleDegrees;
            LocalOffsetFromHome = position.LocalOffsetFromHome;
        }
    }
}
