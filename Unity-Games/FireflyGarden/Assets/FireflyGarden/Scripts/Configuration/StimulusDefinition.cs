using UnityEngine;

namespace FireflyGarden.Configuration
{
    [CreateAssetMenu(menuName = "Firefly Garden/Stimulus Definition", fileName = "Stimulus")]
    public sealed class StimulusDefinition : ScriptableObject
    {
        [SerializeField] private string stimulusId;
        [SerializeField] private Sprite flowerSprite;
        [SerializeField] private string colourId;
        [SerializeField] private string symbolId;
        [SerializeField, Min(0.01f)] private float visualScaleMultiplier = 1f;

        public string StimulusId => stimulusId;
        public Sprite FlowerSprite => flowerSprite;
        public string ColourId => colourId;
        public string SymbolId => symbolId;
        public float VisualScaleMultiplier => visualScaleMultiplier;
    }
}
