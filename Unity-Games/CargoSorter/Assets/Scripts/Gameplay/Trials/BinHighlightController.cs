using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class BinHighlightController : MonoBehaviour
{
    private const string OutlineObjectName = "HighlightOutline";
    private const string GlowObjectName = "HighlightGlow";

    [SerializeField] private List<BinTarget> binTargets = new List<BinTarget>();
    [Header("Guided outline")]
    [SerializeField] private Color outlineColor = new Color(1f, 0.95f, 0.35f, 1f);
    [SerializeField, Min(1f)] private float outlineScaleMultiplier = 1.07f;
    [SerializeField, Range(0f, 1f)] private float minimumAlpha = 0.88f;
    [SerializeField, Range(0f, 1f)] private float maximumAlpha = 1f;
    [SerializeField, Min(0f)] private float pulseSpeed = 0.55f;
    [Header("Guided soft glow")]
    [SerializeField] private Color glowColor = new Color(1f, 0.92f, 0.35f, 0.28f);
    [SerializeField, Min(1f)] private float glowScaleMultiplier = 1.16f;
    [SerializeField, Range(0f, 1f)] private float glowMinimumAlpha = 0.12f;
    [SerializeField, Range(0f, 1f)] private float glowMaximumAlpha = 0.3f;
    [SerializeField, Min(0f)] private float glowScalePulse = 0.025f;
    [Header("Landing feedback")]
    [SerializeField] private Color correctTint = new Color(0.55f, 1f, 0.65f, 1f);
    [SerializeField] private Color wrongBinTint = new Color(0.85f, 0.75f, 0.6f, 1f);
    [SerializeField] private float feedbackDuration = 0.45f;
    [SerializeField] private float wrongBinShakeDuration = 0.25f;
    [SerializeField] private float wrongBinShakeAmplitude = 0.08f;

    private readonly Dictionary<BinTarget, SpriteRenderer> outlineRenderers = new Dictionary<BinTarget, SpriteRenderer>();
    private readonly Dictionary<BinTarget, SpriteRenderer> glowRenderers = new Dictionary<BinTarget, SpriteRenderer>();
    private readonly Dictionary<SpriteRenderer, Color> originalColors = new Dictionary<SpriteRenderer, Color>();
    private BinTarget guidedTarget;
    private Coroutine feedbackRoutine;
    private Transform shakenTransform;
    private Vector3 shakenOriginalLocalPosition;

    private void Awake()
    {
        ConfigureTargets();
        DisableAllOutlines();
    }

    private void Update()
    {
        if (guidedTarget == null || !outlineRenderers.TryGetValue(guidedTarget, out SpriteRenderer outline) || !outline.enabled)
            return;

        float pulse = pulseSpeed > 0f ? (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f : 1f;
        Color color = outlineColor;
        color.a = Mathf.Lerp(minimumAlpha, maximumAlpha, pulse);
        outline.color = color;

        if (!glowRenderers.TryGetValue(guidedTarget, out SpriteRenderer glow) || glow == null) return;
        Color softGlow = glowColor;
        softGlow.a = Mathf.Lerp(glowMinimumAlpha, glowMaximumAlpha, pulse);
        glow.color = softGlow;
        glow.transform.localScale = Vector3.one * (glowScaleMultiplier + pulse * glowScalePulse);
    }

    public void SetGuidedTarget(BinTarget target)
    {
        DisableAllOutlines();
        guidedTarget = target;
        if (target == null) return;

        if (!outlineRenderers.TryGetValue(target, out SpriteRenderer outline))
        {
            ConfigureTarget(target);
            outlineRenderers.TryGetValue(target, out outline);
        }

        if (outline == null) return;
        SetOutlineAlpha(outline, maximumAlpha);
        outline.enabled = true;
        if (glowRenderers.TryGetValue(target, out SpriteRenderer glow) && glow != null) glow.enabled = true;
    }

    public void ClearGuidedTarget() => SetGuidedTarget(null);

    public void SetActiveTargets(IReadOnlyList<BinTarget> targets)
    {
        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
        feedbackRoutine = null;
        RestoreShakePosition();
        DisableAllOutlines();
        RestoreBinColors();
        binTargets = targets != null ? new List<BinTarget>(targets) : new List<BinTarget>();
        ConfigureTargets();
        DisableAllOutlines();
    }

    public void ShowLandingFeedback(BinTarget selectedTarget, TrialOutcome outcome)
    {
        if (selectedTarget == null || outcome == TrialOutcome.Miss) return;
        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
        RestoreShakePosition();
        RestoreBinColors();
        feedbackRoutine = StartCoroutine(ShowFeedback(
            selectedTarget,
            outcome == TrialOutcome.Correct ? correctTint : wrongBinTint,
            outcome == TrialOutcome.WrongBin));
    }

    private IEnumerator ShowFeedback(BinTarget target, Color tint, bool shake)
    {
        TintBin(target, tint);
        float elapsed = 0f;
        if (shake)
        {
            shakenTransform = target.transform;
            shakenOriginalLocalPosition = shakenTransform.localPosition;
        }

        while (elapsed < feedbackDuration)
        {
            elapsed += Time.deltaTime;
            if (shake && shakenTransform != null && elapsed < wrongBinShakeDuration)
            {
                float progress = Mathf.Clamp01(elapsed / wrongBinShakeDuration);
                float horizontalOffset = Mathf.Sin(progress * Mathf.PI * 6f) *
                    wrongBinShakeAmplitude * (1f - progress);
                shakenTransform.localPosition = shakenOriginalLocalPosition + Vector3.right * horizontalOffset;
            }
            else if (shake)
            {
                RestoreShakePosition();
            }
            yield return null;
        }
        RestoreShakePosition();
        feedbackRoutine = null;
        RestoreBinColors();
    }

    private void RestoreShakePosition()
    {
        if (shakenTransform != null) shakenTransform.localPosition = shakenOriginalLocalPosition;
        shakenTransform = null;
    }

    private void ConfigureTargets()
    {
        outlineRenderers.Clear();
        glowRenderers.Clear();
        originalColors.Clear();
        foreach (BinTarget target in binTargets)
            ConfigureTarget(target);
    }

    private void ConfigureTarget(BinTarget target)
    {
        if (target == null) return;
        SpriteRenderer binRenderer = target.GetComponent<SpriteRenderer>();
        if (binRenderer == null) return;

        originalColors[binRenderer] = binRenderer.color;
        Transform outlineTransform = target.transform.Find(OutlineObjectName);
        if (outlineTransform == null)
        {
            GameObject outlineObject = new GameObject(OutlineObjectName, typeof(SpriteRenderer));
            outlineTransform = outlineObject.transform;
            outlineTransform.SetParent(target.transform, false);
        }

        outlineTransform.localPosition = Vector3.zero;
        outlineTransform.localRotation = Quaternion.identity;
        outlineTransform.localScale = Vector3.one * outlineScaleMultiplier;

        SpriteRenderer outline = outlineTransform.GetComponent<SpriteRenderer>();
        if (outline == null) outline = outlineTransform.gameObject.AddComponent<SpriteRenderer>();
        outline.sprite = binRenderer.sprite;
        outline.sortingLayerID = binRenderer.sortingLayerID;
        outline.sortingOrder = binRenderer.sortingOrder - 1;
        outline.flipX = binRenderer.flipX;
        outline.flipY = binRenderer.flipY;
        outline.drawMode = binRenderer.drawMode;
        outline.size = binRenderer.size;
        outline.maskInteraction = binRenderer.maskInteraction;
        SetOutlineAlpha(outline, maximumAlpha);
        outline.enabled = false;
        outlineRenderers[target] = outline;

        Transform glowTransform = target.transform.Find(GlowObjectName);
        if (glowTransform == null)
        {
            GameObject glowObject = new GameObject(GlowObjectName, typeof(SpriteRenderer));
            glowTransform = glowObject.transform;
            glowTransform.SetParent(target.transform, false);
        }
        glowTransform.localPosition = Vector3.zero;
        glowTransform.localRotation = Quaternion.identity;
        glowTransform.localScale = Vector3.one * glowScaleMultiplier;
        SpriteRenderer glow = glowTransform.GetComponent<SpriteRenderer>();
        CopyRendererShape(binRenderer, glow);
        glow.sortingOrder = binRenderer.sortingOrder - 2;
        glow.color = glowColor;
        glow.enabled = false;
        glowRenderers[target] = glow;
    }

    private static void CopyRendererShape(SpriteRenderer source, SpriteRenderer destination)
    {
        destination.sprite = source.sprite;
        destination.sortingLayerID = source.sortingLayerID;
        destination.flipX = source.flipX;
        destination.flipY = source.flipY;
        destination.drawMode = source.drawMode;
        destination.size = source.size;
        destination.maskInteraction = source.maskInteraction;
    }

    private void SetOutlineAlpha(SpriteRenderer outline, float alpha)
    {
        Color color = outlineColor;
        color.a = alpha;
        outline.color = color;
    }

    private void DisableAllOutlines()
    {
        guidedTarget = null;
        foreach (SpriteRenderer outline in outlineRenderers.Values)
            if (outline != null) outline.enabled = false;
        foreach (SpriteRenderer glow in glowRenderers.Values)
            if (glow != null) glow.enabled = false;
    }

    private void TintBin(BinTarget target, Color tint)
    {
        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.color = tint;
    }

    private void RestoreBinColors()
    {
        foreach (var pair in originalColors)
            if (pair.Key != null) pair.Key.color = pair.Value;
    }

    private void OnDisable()
    {
        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
        feedbackRoutine = null;
        RestoreShakePosition();
        DisableAllOutlines();
        RestoreBinColors();
    }

#if UNITY_EDITOR
    public void EditorConfigure(List<BinTarget> targets)
    {
        binTargets = targets;
        ConfigureTargets();
        DisableAllOutlines();
    }
#endif
}
