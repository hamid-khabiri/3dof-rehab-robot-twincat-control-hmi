using System.Collections.Generic;
using UnityEngine;

namespace FireflyGarden.Configuration
{
    [CreateAssetMenu(menuName = "Firefly Garden/Session Definition", fileName = "SessionDefinition")]
    public sealed class SessionDefinition : ScriptableObject
    {
        [SerializeField] private string sessionDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private List<SingleFlowerBlockDefinition> orderedBlocks = new List<SingleFlowerBlockDefinition>();
        [SerializeField] private bool showBlockTransitions = true;
        [SerializeField] private bool requireManualContinue = true;
        [SerializeField, Min(0f)] private float minimumTransitionDisplayDuration = 0.5f;

        public string SessionDefinitionId => sessionDefinitionId;
        public string DisplayName => displayName;
        public IReadOnlyList<SingleFlowerBlockDefinition> OrderedBlocks => orderedBlocks;
        public bool ShowBlockTransitions => showBlockTransitions;
        public bool RequireManualContinue => requireManualContinue;
        public float MinimumTransitionDisplayDuration => minimumTransitionDisplayDuration;

        public bool TryValidate(out string error)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(sessionDefinitionId)) errors.Add("sessionDefinitionId must be non-empty.");
            if (orderedBlocks == null || orderedBlocks.Count == 0) errors.Add("At least one ordered block is required.");
            else
            {
                for (int index = 0; index < orderedBlocks.Count; index++)
                {
                    SingleFlowerBlockDefinition block = orderedBlocks[index];
                    if (block == null) errors.Add($"Block {index + 1} is null.");
                    else if (!block.TryValidate(out string blockError)) errors.Add($"Block {index + 1} is invalid: {blockError}");
                }
            }
            if (float.IsNaN(minimumTransitionDisplayDuration) || float.IsInfinity(minimumTransitionDisplayDuration)
                || minimumTransitionDisplayDuration < 0f) errors.Add("minimumTransitionDisplayDuration must be finite and non-negative.");
            error = string.Join(" ", errors);
            return errors.Count == 0;
        }
    }
}
