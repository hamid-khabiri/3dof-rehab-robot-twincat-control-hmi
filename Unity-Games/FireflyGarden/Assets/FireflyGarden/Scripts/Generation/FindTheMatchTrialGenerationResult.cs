using System.Collections.Generic;

namespace FireflyGarden.Generation
{
    public sealed class FindTheMatchTrialGenerationResult
    {
        public bool Succeeded => Errors.Count == 0;
        public IReadOnlyList<FindTheMatchTrialDefinition> Trials { get; }
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public int RandomSeedUsed { get; }
        public string GeneratorVersion { get; }

        internal FindTheMatchTrialGenerationResult(IList<FindTheMatchTrialDefinition> trials,
            IList<string> errors, IList<string> warnings, int seed, string version)
        {
            Trials = new List<FindTheMatchTrialDefinition>(trials).AsReadOnly();
            Errors = new List<string>(errors).AsReadOnly(); Warnings = new List<string>(warnings).AsReadOnly();
            RandomSeedUsed = seed; GeneratorVersion = version;
        }
    }
}
