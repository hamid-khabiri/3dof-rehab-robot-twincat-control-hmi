using UnityEngine;
using UnityEngine.UI;

namespace FireflyGarden.Presentation
{
    public sealed class DwellRingPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject rootVisual;
        [SerializeField] private Image trackImage;
        [SerializeField] private Image progressImage;

        private RectTransform rootRectTransform;

        public bool IsConfigured => rootVisual != null
            && trackImage != null
            && progressImage != null;

        private void Awake()
        {
            if (!IsConfigured)
            {
                Debug.LogError(
                    "DwellRingPresenter requires a root visual, Track Image, and Progress Image.",
                    this);
                enabled = false;
                return;
            }

            rootRectTransform = rootVisual.transform as RectTransform;
            ResetProgress();
        }

        public void AlignCentreToRenderedFlower(SpriteRenderer flowerRenderer)
        {
            if (flowerRenderer == null || flowerRenderer.sprite == null) return;
            if (rootRectTransform == null)
                rootRectTransform = rootVisual.transform as RectTransform;
            if (rootRectTransform == null || rootRectTransform.parent == null) return;

            Bounds renderedBounds = flowerRenderer.bounds;
            Vector3 centreInRingParent = rootRectTransform.parent.InverseTransformPoint(renderedBounds.center);
            Vector3 localPosition = rootRectTransform.localPosition;
            rootRectTransform.localPosition = new Vector3(
                centreInRingParent.x, centreInRingParent.y, localPosition.z);
        }

        public void Show()
        {
            rootVisual.SetActive(true);
        }

        public void Hide()
        {
            ResetProgress();
            rootVisual.SetActive(false);
        }

        public void SetProgress(float normalizedProgress)
        {
            progressImage.fillAmount = Mathf.Clamp01(normalizedProgress);
        }

        public void ResetProgress()
        {
            SetProgress(0f);
        }

        public void ShowCompleted()
        {
            Show();
            SetProgress(1f);
        }
    }
}
