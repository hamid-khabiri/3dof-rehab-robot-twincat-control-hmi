using UnityEngine;

namespace FireflyGarden.Configuration
{
    [CreateAssetMenu(menuName = "Firefly Garden/Desktop Gameplay Safe Area", fileName = "DesktopGameplaySafeArea")]
    public sealed class DesktopGameplaySafeArea : ScriptableObject
    {
        [SerializeField] private string safeAreaId;
        [SerializeField] private float minimumXOffsetFromHome;
        [SerializeField] private float maximumXOffsetFromHome;
        [SerializeField] private float minimumYOffsetFromHome;
        [SerializeField] private float maximumYOffsetFromHome;
        [SerializeField] private float targetVisualMargin;
        [SerializeField, TextArea] private string designerNotes;

        public string SafeAreaId => safeAreaId;
        public float MinimumXOffsetFromHome => minimumXOffsetFromHome;
        public float MaximumXOffsetFromHome => maximumXOffsetFromHome;
        public float MinimumYOffsetFromHome => minimumYOffsetFromHome;
        public float MaximumYOffsetFromHome => maximumYOffsetFromHome;
        public float TargetVisualMargin => targetVisualMargin;
        public string DesignerNotes => designerNotes;

        public bool ContainsCircle(Vector2 offset, float radius)
        {
            float protectedRadius = radius + targetVisualMargin;
            return offset.x - protectedRadius >= minimumXOffsetFromHome
                && offset.x + protectedRadius <= maximumXOffsetFromHome
                && offset.y - protectedRadius >= minimumYOffsetFromHome
                && offset.y + protectedRadius <= maximumYOffsetFromHome;
        }
    }
}
