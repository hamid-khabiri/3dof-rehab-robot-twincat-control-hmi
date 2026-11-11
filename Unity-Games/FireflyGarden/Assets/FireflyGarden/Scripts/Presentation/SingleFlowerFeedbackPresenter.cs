using UnityEngine;

namespace FireflyGarden.Presentation
{
    public sealed class SingleFlowerFeedbackPresenter : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private bool isTargetActive;

        public bool IsTargetActive => isTargetActive;
        public bool IsConfigured => targetRenderer != null;

        private void Awake()
        {
            if (targetRenderer == null)
            {
                Debug.LogError("SingleFlowerFeedbackPresenter requires an assigned target SpriteRenderer.", this);
                enabled = false;
                return;
            }
        }

        public void SetTargetActive(bool active)
        {
            isTargetActive = active;
            targetRenderer.enabled = active;
        }
    }
}
