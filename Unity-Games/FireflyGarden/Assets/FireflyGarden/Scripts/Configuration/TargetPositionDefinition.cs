using UnityEngine;

namespace FireflyGarden.Configuration
{
    [CreateAssetMenu(menuName = "Firefly Garden/Target Position", fileName = "TargetPosition")]
    public sealed class TargetPositionDefinition : ScriptableObject
    {
        [SerializeField] private string positionId;
        [SerializeField] private SingleFlowerDirection direction;
        [SerializeField] private float angleDegrees;
        [SerializeField] private TargetDistanceBand distanceBand;
        [SerializeField] private Vector2 localOffsetFromHome;
        [SerializeField] private bool enabledByDefault = true;
        [SerializeField, TextArea] private string designerNotes;

        public string PositionId => positionId;
        public SingleFlowerDirection Direction => direction;
        public float AngleDegrees => angleDegrees;
        public TargetDistanceBand DistanceBand => distanceBand;
        public Vector2 LocalOffsetFromHome => localOffsetFromHome;
        public bool EnabledByDefault => enabledByDefault;
        public string DesignerNotes => designerNotes;
    }
}
