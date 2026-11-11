using System;
using System.Collections.Generic;
using FireflyGarden.Configuration;
using UnityEngine;

namespace FireflyGarden.Generation
{
    public sealed class FindTheMatchTrialGenerator
    {
        public const string Version = "FindTheMatchTrialGenerator/1.1";

        private sealed class PositionPair
        {
            public TargetPositionDefinition A;
            public TargetPositionDefinition B;
            public string Key => A.PositionId + "/" + B.PositionId;
        }

        public FindTheMatchTrialGenerationResult Generate(SingleFlowerBlockDefinition block, int effectiveSeed,
            DesktopGameplaySafeArea safeArea, float homeRadius, float targetARadius,
            float targetBRadius, float homeSafetyMargin, float targetTargetSafetyMargin)
        {
            var errors = new List<string>(); var warnings = new List<string>();
            int seed = effectiveSeed;
            var stimuli = ValidStimuli(block, errors);
            var pairs = ValidPositionPairs(block, safeArea, homeRadius, targetARadius,
                targetBRadius, homeSafetyMargin, targetTargetSafetyMargin, errors);
            if (block == null || (block.GameMode != GameMode.FindTheMatch && block.GameMode != GameMode.RememberTheMatch))
                errors.Add("A cognitive match block definition is required.");
            else
            {
                if (string.IsNullOrWhiteSpace(block.BlockId)) errors.Add("blockId must be non-empty.");
                if (block.TrialCount <= 0) errors.Add("trialCount must be positive.");
                if (effectiveSeed == 0) errors.Add("The effective random seed must be non-zero.");
            }
            if (block != null && block.MaxSameCorrectSlotConsecutively < 1) errors.Add("maxSameCorrectSlotConsecutively must be at least 1.");
            if (errors.Count > 0) return Result(new List<FindTheMatchTrialDefinition>(), errors, warnings, seed);

            var sameColourPairs = BuildStimulusPairs(stimuli, true);
            var differentColourPairs = BuildStimulusPairs(stimuli, false);
            if (sameColourPairs.Count == 0) errors.Add("No same-colour, different-symbol stimulus pair is available.");
            if (differentColourPairs.Count == 0) errors.Add("No different-colour, different-symbol stimulus pair is available.");
            if (errors.Count > 0) return Result(new List<FindTheMatchTrialDefinition>(), errors, warnings, seed);

            var random = new System.Random(seed);
            Shuffle(sameColourPairs, random); Shuffle(differentColourPairs, random); Shuffle(pairs, random);
            var correctOrder = new List<StimulusDefinition>(stimuli); Shuffle(correctOrder, random);
            var sameColourSchedule = BalancedPairCategories(block.TrialCount, random);
            var slots = BalancedSlots(block.TrialCount, random);
            var trials = new List<FindTheMatchTrialDefinition>(block.TrialCount);
            string previousStimulusKey = null, previousPositionKey = null;
            for (int i = 0; i < block.TrialCount; i++)
            {
                StimulusDefinition desiredCorrect = correctOrder[i % correctOrder.Count];
                List<Tuple<StimulusDefinition, StimulusDefinition>> scheduledPairs = sameColourSchedule[i]
                    ? sameColourPairs : differentColourPairs;
                int stimulusIndex = ChooseStimulusPair(scheduledPairs, desiredCorrect, previousStimulusKey,
                    block.AvoidImmediateStimulusPairRepeat, i);
                int positionIndex = ChoosePositionPair(pairs, previousPositionKey,
                    block.AvoidImmediatePositionPairRepeat, i);
                var stimulusPair = scheduledPairs[stimulusIndex]; var positionPair = pairs[positionIndex];
                string id = $"{block.BlockId}_{i + 1:D3}_{stimulusPair.Item1.StimulusId}_{positionPair.Key}_{slots[i]}";
                trials.Add(new FindTheMatchTrialDefinition(i, id, stimulusPair.Item1, stimulusPair.Item2,
                    slots[i], positionPair.A, positionPair.B));
                previousStimulusKey = StimulusKey(stimulusPair); previousPositionKey = positionPair.Key;
            }
            return Result(trials, errors, warnings, seed);
        }

        private static List<StimulusDefinition> ValidStimuli(SingleFlowerBlockDefinition block, List<string> errors)
        {
            var result = new List<StimulusDefinition>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            if (block == null) return result;
            foreach (StimulusDefinition value in block.EnabledStimuli)
            {
                if (value == null || string.IsNullOrWhiteSpace(value.StimulusId) || value.FlowerSprite == null
                    || string.IsNullOrWhiteSpace(value.ColourId) || string.IsNullOrWhiteSpace(value.SymbolId)) continue;
                if (!ids.Add(value.StimulusId)) { errors.Add("Duplicate enabled stimulusId: " + value.StimulusId); continue; }
                result.Add(value);
            }
            if (result.Count < 2) errors.Add("Find the Match requires at least two valid enabled stimuli.");
            return result;
        }

        private static List<PositionPair> ValidPositionPairs(SingleFlowerBlockDefinition block,
            DesktopGameplaySafeArea safeArea, float homeRadius, float radiusA, float radiusB,
            float homeMargin, float pairMargin, List<string> errors)
        {
            var result = new List<PositionPair>();
            if (block == null || safeArea == null) { errors.Add("A block and gameplay safe area are required for position-pair validation."); return result; }
            var positions = new List<TargetPositionDefinition>();
            foreach (TargetPositionDefinition p in block.EnabledPositions) if (p != null && p.EnabledByDefault) positions.Add(p);
            for (int a = 0; a < positions.Count; a++) for (int b = 0; b < positions.Count; b++)
            {
                if (a == b || positions[a].PositionId == positions[b].PositionId) continue;
                Vector2 pa = positions[a].LocalOffsetFromHome, pb = positions[b].LocalOffsetFromHome;
                if (!safeArea.ContainsCircle(pa, radiusA) || !safeArea.ContainsCircle(pb, radiusB)) continue;
                if (pa.magnitude <= homeRadius + radiusA + homeMargin || pb.magnitude <= homeRadius + radiusB + homeMargin) continue;
                if (Vector2.Distance(pa, pb) <= radiusA + radiusB + pairMargin) continue;
                if (Mathf.Abs(pa.magnitude - pb.magnitude) > 0.35f) continue;
                result.Add(new PositionPair { A = positions[a], B = positions[b] });
            }
            if (result.Count == 0) errors.Add("No enabled target-position pair satisfies safe-area, home, separation, and comparable-reach requirements.");
            return result;
        }

        private static List<Tuple<StimulusDefinition, StimulusDefinition>> BuildStimulusPairs(
            List<StimulusDefinition> values, bool sameColour)
        {
            var result = new List<Tuple<StimulusDefinition, StimulusDefinition>>();
            foreach (var correct in values) foreach (var other in values)
                if (correct != other && correct.StimulusId != other.StimulusId
                    && correct.SymbolId != other.SymbolId
                    && (sameColour ? correct.ColourId == other.ColourId : correct.ColourId != other.ColourId))
                    result.Add(Tuple.Create(correct, other));
            return result;
        }

        private static List<bool> BalancedPairCategories(int count, System.Random random)
        {
            bool sameColourFirst = random.Next(2) == 0;
            var result = new List<bool>(count);
            for (int i = 0; i < count; i++)
                result.Add(i % 2 == 0 ? sameColourFirst : !sameColourFirst);
            return result;
        }

        private static List<TargetSlotId> BalancedSlots(int count, System.Random random)
        {
            TargetSlotId first = random.Next(2) == 0 ? TargetSlotId.A : TargetSlotId.B;
            var values = new List<TargetSlotId>(count);
            int aRemaining = count / 2 + (count % 2 == 1 && first == TargetSlotId.A ? 1 : 0);
            int bRemaining = count - aRemaining;
            for (int i = 0; i < count; i++)
            {
                TargetSlotId preferred = i % 2 == 0 ? first : (first == TargetSlotId.A ? TargetSlotId.B : TargetSlotId.A);
                if ((preferred == TargetSlotId.A ? aRemaining : bRemaining) == 0) preferred = preferred == TargetSlotId.A ? TargetSlotId.B : TargetSlotId.A;
                values.Add(preferred); if (preferred == TargetSlotId.A) aRemaining--; else bRemaining--;
            }
            // Alternation satisfies every supported positive run limit while preserving exact balance.
            return values;
        }

        private static int ChooseStimulusPair(List<Tuple<StimulusDefinition, StimulusDefinition>> pairs,
            StimulusDefinition correct, string previous, bool avoidRepeat, int offset)
        {
            for (int step = 0; step < pairs.Count; step++) { int i = (offset + step) % pairs.Count;
                if (pairs[i].Item1 == correct && (!avoidRepeat || StimulusKey(pairs[i]) != previous)) return i; }
            for (int step = 0; step < pairs.Count; step++) { int i = (offset + step) % pairs.Count;
                if (!avoidRepeat || StimulusKey(pairs[i]) != previous) return i; }
            return offset % pairs.Count;
        }

        private static int ChoosePositionPair(List<PositionPair> pairs, string previous, bool avoidRepeat, int offset)
        {
            for (int step = 0; step < pairs.Count; step++) { int i = (offset + step) % pairs.Count;
                if (!avoidRepeat || pairs[i].Key != previous) return i; }
            return offset % pairs.Count;
        }

        private static string StimulusKey(Tuple<StimulusDefinition, StimulusDefinition> pair) => pair.Item1.StimulusId + "/" + pair.Item2.StimulusId;
        private static FindTheMatchTrialGenerationResult Result(IList<FindTheMatchTrialDefinition> trials,
            IList<string> errors, IList<string> warnings, int seed) => new FindTheMatchTrialGenerationResult(trials, errors, warnings, seed, Version);
        private static void Shuffle<T>(IList<T> values, System.Random random)
        { for (int i = values.Count - 1; i > 0; i--) { int j = random.Next(i + 1); T t = values[i]; values[i] = values[j]; values[j] = t; } }
    }
}
