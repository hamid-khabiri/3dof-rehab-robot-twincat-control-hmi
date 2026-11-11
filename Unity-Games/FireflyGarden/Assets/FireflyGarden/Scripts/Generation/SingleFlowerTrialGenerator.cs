using System;
using System.Collections.Generic;
using FireflyGarden.Configuration;
using UnityEngine;

namespace FireflyGarden.Generation
{
    public sealed class SingleFlowerTrialGenerator
    {
        public const string Version = "SingleFlowerTrialGenerator/1.0";

        public SingleFlowerTrialGenerationResult Generate(SingleFlowerBlockDefinition block)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            Validate(block, errors, warnings);
            int seed = block != null ? block.RandomSeed : 0;
            if (errors.Count > 0)
                return new SingleFlowerTrialGenerationResult(new List<SingleFlowerTrialDefinition>(), errors, warnings, seed, Version);

            var positions = new List<TargetPositionDefinition>();
            foreach (TargetPositionDefinition position in block.EnabledPositions)
                if (position.EnabledByDefault) positions.Add(position);

            var random = new System.Random(seed);
            Shuffle(positions, random);
            var remaining = new Dictionary<TargetPositionDefinition, int>();
            int baseCount = block.TrialCount / positions.Count;
            int remainder = block.TrialCount % positions.Count;
            for (int i = 0; i < positions.Count; i++) remaining[positions[i]] = baseCount + (i < remainder ? 1 : 0);

            var ordered = new List<TargetPositionDefinition>(block.TrialCount);
            // A bounded deterministic backtrack preserves balanced counts while satisfying adjacency rules.
            int visitedCandidates = 0;
            if (!BuildOrder(positions, remaining, ordered, block, random, ref visitedCandidates,
                Math.Max(10000, block.TrialCount * positions.Count * 1000)))
            {
                errors.Add("No valid ordered trial schedule satisfies the configured repeat constraints.");
                return new SingleFlowerTrialGenerationResult(new List<SingleFlowerTrialDefinition>(), errors, warnings, seed, Version);
            }

            var trials = new List<SingleFlowerTrialDefinition>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                string id = $"{block.BlockId}_{i + 1:D3}_{ordered[i].PositionId}";
                trials.Add(new SingleFlowerTrialDefinition(i, id, ordered[i]));
            }
            return new SingleFlowerTrialGenerationResult(trials, errors, warnings, seed, Version);
        }

        private static void Validate(SingleFlowerBlockDefinition block, List<string> errors, List<string> warnings)
        {
            if (block == null) { errors.Add("A block definition is required."); return; }
            if (string.IsNullOrWhiteSpace(block.BlockId)) errors.Add("blockId must be non-empty.");
            if (string.IsNullOrWhiteSpace(block.ConditionId)) errors.Add("conditionId must be non-empty.");
            if (block.ConfigurationVersion <= 0) errors.Add("configurationVersion must be positive.");
            if (block.TrialCount <= 0) errors.Add("trialCount must be positive.");
            if (block.RandomSeed == 0) errors.Add("randomSeed must be a stable non-zero value.");
            if (block.MaximumConsecutiveSameDirection < 1) errors.Add("maximumConsecutiveSameDirection must be at least 1.");
            if (block.TargetScaleMultiplier <= 0f) errors.Add("targetScaleMultiplier must be positive.");
            if (block.InitialHomeDwellDuration <= 0f || block.TargetDwellDuration <= 0f
                || block.SuccessFeedbackDuration <= 0f || block.ReturnHomeDwellDuration <= 0f)
                errors.Add("All block timing durations must be positive.");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var offsets = new HashSet<Vector2>();
            int usable = 0;
            if (block.EnabledPositions == null || block.EnabledPositions.Count == 0) errors.Add("At least one enabled position must be assigned.");
            else foreach (TargetPositionDefinition position in block.EnabledPositions)
            {
                if (position == null) { errors.Add("enabledPositions contains a null reference."); continue; }
                if (string.IsNullOrWhiteSpace(position.PositionId)) errors.Add("Every positionId must be non-empty.");
                else if (!ids.Add(position.PositionId)) errors.Add($"Duplicate positionId: {position.PositionId}.");
                if (position.LocalOffsetFromHome == Vector2.zero) errors.Add($"Position {position.PositionId} has a zero offset.");
                else if (!offsets.Add(position.LocalOffsetFromHome)) errors.Add($"Duplicate local offset on position {position.PositionId}.");
                if (position.EnabledByDefault) usable++;
            }
            if (usable == 0) errors.Add("At least one assigned position must be enabled by default.");
            if (usable == 1 && block.TrialCount > 1 && block.AvoidImmediatePositionRepeat)
                errors.Add("Immediate-repeat avoidance is impossible with one enabled position and more than one trial.");
            warnings.Add("Desktop offsets require manual camera-edge and logical-region review in Unity.");
        }

        private static bool BuildOrder(List<TargetPositionDefinition> positions,
            Dictionary<TargetPositionDefinition, int> remaining, List<TargetPositionDefinition> ordered,
            SingleFlowerBlockDefinition block, System.Random random, ref int visits, int visitLimit)
        {
            if (ordered.Count == block.TrialCount) return true;
            if (visits >= visitLimit) return false;
            var candidates = new List<TargetPositionDefinition>();
            foreach (TargetPositionDefinition p in positions) if (remaining[p] > 0) candidates.Add(p);
            Shuffle(candidates, random);
            candidates.Sort((a, b) => remaining[b].CompareTo(remaining[a]));
            foreach (TargetPositionDefinition candidate in candidates)
            {
                visits++;
                if (visits > visitLimit) return false;
                if (!CanAppend(candidate, ordered, block)) continue;
                ordered.Add(candidate); remaining[candidate]--;
                if (BuildOrder(positions, remaining, ordered, block, random, ref visits, visitLimit)) return true;
                remaining[candidate]++; ordered.RemoveAt(ordered.Count - 1);
            }
            return false;
        }

        private static bool CanAppend(TargetPositionDefinition candidate, List<TargetPositionDefinition> ordered, SingleFlowerBlockDefinition block)
        {
            if (ordered.Count == 0) return true;
            if (block.AvoidImmediatePositionRepeat && ordered[ordered.Count - 1].PositionId == candidate.PositionId) return false;
            int run = 0;
            for (int i = ordered.Count - 1; i >= 0 && ordered[i].Direction == candidate.Direction; i--) run++;
            return run < block.MaximumConsecutiveSameDirection;
        }

        private static void Shuffle<T>(IList<T> values, System.Random random)
        {
            for (int i = values.Count - 1; i > 0; i--) { int j = random.Next(i + 1); T t = values[i]; values[i] = values[j]; values[j] = t; }
        }
    }
}
