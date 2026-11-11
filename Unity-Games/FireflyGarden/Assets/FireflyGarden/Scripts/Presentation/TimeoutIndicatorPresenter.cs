using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FireflyGarden.Presentation
{
    public sealed class TimeoutIndicatorPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject rootVisual;
        [SerializeField] private Image clockIcon;
        [SerializeField] private TMP_Text remainingTimeText;
        [SerializeField] private Text fallbackRemainingTimeText;

        public bool IsConfigured => rootVisual != null && (remainingTimeText != null || fallbackRemainingTimeText != null);

        private void Awake()
        {
            ResetPresentation();
        }

        public void SetVisible(bool visible)
        {
            if (rootVisual != null) rootVisual.SetActive(visible);
        }

        public void SetRemainingSeconds(float remainingSeconds)
        {
            int wholeSeconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
            string value = $"{wholeSeconds} s";

            if (remainingTimeText != null) remainingTimeText.text = value;
            if (fallbackRemainingTimeText != null) fallbackRemainingTimeText.text = value;
        }

        public void ResetPresentation()
        {
            SetRemainingSeconds(0f);
            SetVisible(false);
        }
    }
}
