using FireflyGarden.Configuration;
using FireflyGarden.Presentation;
using UnityEngine;

namespace FireflyGarden.Gameplay
{
    public sealed class TargetSlot : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer flowerRenderer;
        [SerializeField] private Transform targetVisual;
        [SerializeField] private LogicalFlowerRegion logicalRegion;
        [SerializeField] private DwellRingPresenter dwellRingPresenter;
        [SerializeField] private StimulusDefinition currentStimulus;
        [SerializeField] private string slotIdentifier;
        [SerializeField] private bool isCorrect;
        [SerializeField] private bool isSelectable;

        private Vector3 authoredVisualScale;

        public StimulusDefinition CurrentStimulus => currentStimulus;
        public bool IsSelectable => isSelectable;
        public bool IsCorrect => isCorrect;
        public string SlotIdentifier => slotIdentifier;
        public LogicalFlowerRegion LogicalRegion => logicalRegion;

        private void Awake()
        {
            authoredVisualScale = targetVisual.localScale;
        }

        public bool IsConfigured => targetVisual != null && flowerRenderer != null && logicalRegion != null
            && dwellRingPresenter != null && dwellRingPresenter.IsConfigured && !string.IsNullOrWhiteSpace(slotIdentifier);

        public void Configure(StimulusDefinition stimulus, bool correct, Vector3 position)
        {
            transform.position = position;
            isCorrect = correct;
            AssignStimulus(stimulus);
            RestoreNormalVisual();
        }

        public void AssignStimulus(StimulusDefinition stimulus)
        {
            currentStimulus = stimulus;
            flowerRenderer.sprite = stimulus != null ? stimulus.FlowerSprite : null;
            float multiplier = stimulus != null ? stimulus.VisualScaleMultiplier : 1f;
            targetVisual.localScale = authoredVisualScale * multiplier;
            SynchronizeDwellRing();
        }

        public void Show(bool selectable)
        {
            isSelectable = selectable;
            flowerRenderer.enabled = true;
            SynchronizeDwellRing();
        }

        public void SetVisible(bool visible) { if (visible) Show(isSelectable); else Hide(); }
        public void SetSelectable(bool selectable) => isSelectable = selectable;
        public bool ContainsFireflyCentre(Vector2 fireflyPosition) => logicalRegion.Contains(fireflyPosition);
        public void ShowDwell(float normalizedProgress)
        {
            SynchronizeDwellRing();
            dwellRingPresenter.Show();
            dwellRingPresenter.SetProgress(normalizedProgress);
        }
        public void ResetDwellVisual() => dwellRingPresenter.Hide();
        public void ShowCompleted() => dwellRingPresenter.ShowCompleted();
        public void ShowIncorrectFeedback() => flowerRenderer.color = new Color(1f, 1f, 1f, 0.45f);
        public void RestoreNormalVisual() => flowerRenderer.color = Color.white;

        public void Hide()
        {
            isSelectable = false;
            flowerRenderer.enabled = false;
            dwellRingPresenter.Hide();
        }

        public void ResetDwellRing() => dwellRingPresenter.Hide();

        private void SynchronizeDwellRing() => dwellRingPresenter.AlignCentreToRenderedFlower(flowerRenderer);
    }
}
