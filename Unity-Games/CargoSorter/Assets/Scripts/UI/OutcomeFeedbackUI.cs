using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class OutcomeFeedbackUI : MonoBehaviour
{
    [SerializeField] private Image feedbackImage;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Sprite correctSprite;
    [SerializeField] private Sprite wrongSprite;
    [SerializeField] private Sprite missSprite;
    [SerializeField] private float visibleDuration = 0.7f;
    [SerializeField] private float fadeInDuration = 0.1f;
    [SerializeField] private float fadeOutDuration = 0.2f;
    [SerializeField] private Vector2 targetScreenOffset = new Vector2(0f, 72f);
    [SerializeField] private Vector2 missAnchoredPosition = new Vector2(0f, 290f);

    private Coroutine feedbackRoutine;
    private Transform trackedTarget;
    private RectTransform feedbackRect;
    private RectTransform feedbackParent;
    private Canvas parentCanvas;
    private Vector2 originalAnchorMin;
    private Vector2 originalAnchorMax;
    private Vector2 originalPivot;
    private bool usesFixedCanvasPosition;

    public float TotalDuration => Mathf.Max(0f, fadeInDuration) + Mathf.Max(0f, visibleDuration) + Mathf.Max(0f, fadeOutDuration);

    private void Awake()
    {
        if (feedbackImage == null || canvasGroup == null)
        {
            Debug.LogError($"{nameof(OutcomeFeedbackUI)} on '{name}' requires an Image and CanvasGroup.", this);
            enabled = false;
            return;
        }

        feedbackImage.preserveAspect = true;
        feedbackImage.raycastTarget = false;
        feedbackRect = feedbackImage.rectTransform;
        feedbackParent = feedbackRect.parent as RectTransform;
        parentCanvas = feedbackImage.GetComponentInParent<Canvas>();
        originalAnchorMin = feedbackRect.anchorMin;
        originalAnchorMax = feedbackRect.anchorMax;
        originalPivot = feedbackRect.pivot;
        Hide();
    }

    private void OnDisable()
    {
        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
        feedbackRoutine = null;
        Hide();
    }

    public void ShowOutcome(TrialOutcome outcome, Transform target)
    {
        if (!enabled || feedbackImage == null || canvasGroup == null) return;

        Sprite sprite = GetSprite(outcome);
        if (sprite == null) return;
        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);

        feedbackImage.sprite = sprite;
        // Feedback sprites carry their approved colors; a neutral tint preserves them exactly.
        feedbackImage.color = Color.white;
        trackedTarget = target;
        usesFixedCanvasPosition = outcome == TrialOutcome.Miss;
        if (usesFixedCanvasPosition)
        {
            feedbackRect.anchorMin = feedbackRect.anchorMax = new Vector2(0.5f, 0f);
            feedbackRect.pivot = new Vector2(0.5f, 0.5f);
            feedbackRect.anchoredPosition = missAnchoredPosition;
        }
        else
        {
            feedbackRect.anchorMin = originalAnchorMin;
            feedbackRect.anchorMax = originalAnchorMax;
            feedbackRect.pivot = originalPivot;
        }
        UpdateTrackedPosition();
        feedbackImage.enabled = true;
        canvasGroup.alpha = 0f;
        feedbackRoutine = StartCoroutine(ShowFeedback());
    }

    private IEnumerator ShowFeedback()
    {
        yield return Fade(0f, 1f, fadeInDuration);
        float visibleElapsed = 0f;
        while (visibleElapsed < visibleDuration)
        {
            visibleElapsed += Time.deltaTime;
            UpdateTrackedPosition();
            yield return null;
        }
        yield return Fade(1f, 0f, fadeOutDuration);
        Hide();
        feedbackRoutine = null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f) { canvasGroup.alpha = to; yield break; }
        float elapsed = 0f;
        while (elapsed < duration)
        {
            UpdateTrackedPosition();
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        canvasGroup.alpha = to;
    }

    private void UpdateTrackedPosition()
    {
        if (usesFixedCanvasPosition || trackedTarget == null || feedbackRect == null || feedbackParent == null) return;
        Camera worldCamera = Camera.main;
        if (worldCamera == null) return;
        Vector2 screenPoint = (Vector2)worldCamera.WorldToScreenPoint(trackedTarget.position) + targetScreenOffset;
        Camera canvasCamera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? parentCanvas.worldCamera
            : null;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(feedbackParent, screenPoint, canvasCamera, out Vector2 localPoint))
            feedbackRect.anchoredPosition = localPoint;
    }

    private Sprite GetSprite(TrialOutcome outcome)
    {
        switch (outcome)
        {
            case TrialOutcome.Correct: return correctSprite;
            case TrialOutcome.WrongBin: return wrongSprite;
            case TrialOutcome.Miss: return missSprite;
            default: return null;
        }
    }

    private void Hide()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (feedbackImage != null) feedbackImage.enabled = false;
        trackedTarget = null;
    }
}
