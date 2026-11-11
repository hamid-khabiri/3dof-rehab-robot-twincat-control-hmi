using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FireflyGarden.Presentation
{
    public sealed class TrialProgressLanternPresenter : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private RectTransform container;
        [SerializeField] private Image lanternTemplate;
        [SerializeField] private Sprite incompleteLanternSprite;
        [SerializeField] private Sprite completedLanternSprite;

        [Header("Runtime Debugging")]
        [SerializeField] private int displayedTotalCount;
        [SerializeField] private int displayedCompletedCount;

        private readonly List<Image> lanternImages = new List<Image>();

        public bool IsConfigured => container != null
            && lanternTemplate != null
            && incompleteLanternSprite != null
            && completedLanternSprite != null;
        public int DisplayedTotalCount => displayedTotalCount;
        public int DisplayedCompletedCount => displayedCompletedCount;

        private void Awake()
        {
            if (!IsConfigured)
            {
                Debug.LogError("TrialProgressLanternPresenter requires a container, template, and both lantern sprites.", this);
                enabled = false;
                return;
            }

            lanternTemplate.raycastTarget = false;
            lanternTemplate.gameObject.SetActive(false);
            displayedTotalCount = 0;
            displayedCompletedCount = 0;
        }

        public void Initialize(int totalTrialCount)
        {
            if (!IsConfigured || totalTrialCount <= 0)
            {
                return;
            }

            ClearRuntimeIcons();
            for (int index = 0; index < totalTrialCount; index++)
            {
                Image lantern = Instantiate(lanternTemplate, container);
                lantern.name = $"Lantern {index + 1}";
                lantern.sprite = incompleteLanternSprite;
                lantern.preserveAspect = true;
                lantern.raycastTarget = false;
                lantern.gameObject.SetActive(true);
                lanternImages.Add(lantern);
            }

            displayedTotalCount = totalTrialCount;
            displayedCompletedCount = 0;
        }

        public void SetCompletedCount(int completedTrialCount)
        {
            displayedCompletedCount = Mathf.Clamp(completedTrialCount, 0, displayedTotalCount);
            for (int index = 0; index < lanternImages.Count; index++)
            {
                lanternImages[index].sprite = index < displayedCompletedCount
                    ? completedLanternSprite
                    : incompleteLanternSprite;
            }
        }

        public void Clear() => ClearRuntimeIcons();

        private void ClearRuntimeIcons()
        {
            for (int index = lanternImages.Count - 1; index >= 0; index--)
            {
                if (lanternImages[index] != null)
                {
                    lanternImages[index].gameObject.SetActive(false);
                    Destroy(lanternImages[index].gameObject);
                }
            }

            lanternImages.Clear();
            displayedTotalCount = 0;
            displayedCompletedCount = 0;
        }
    }
}
