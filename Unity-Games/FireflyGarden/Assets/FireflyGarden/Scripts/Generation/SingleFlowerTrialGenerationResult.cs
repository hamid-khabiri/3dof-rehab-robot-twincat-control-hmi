using System.Collections.Generic;

namespace FireflyGarden.Generation
{
    public sealed class SingleFlowerTrialGenerationResult
    {
        public bool Succeeded => Errors.Count == 0;
        public IReadOnlyList<SingleFlowerTrialDefinition> Trials { get; }
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public int RandomSeedUsed { get; }
        public string GeneratorVersion { get; }

        internal SingleFlowerTrialGenerationResult(
            IList<SingleFlowerTrialDefinition> trials, IList<string> errors,
            IList<string> warnings, int seed, string version)
        {
            Trials = new List<SingleFlowerTrialDefinition>(trials).AsReadOnly();
            Errors = new List<string>(errors).AsReadOnly();
            Warnings = new List<string>(warnings).AsReadOnly();
            RandomSeedUsed = seed;
            GeneratorVersion = version;
        }
    }
}
