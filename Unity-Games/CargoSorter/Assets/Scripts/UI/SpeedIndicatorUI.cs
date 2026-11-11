using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SpeedIndicatorUI : MonoBehaviour
{
    [SerializeField, Range(1, 5)] private int currentSpeedLevel = 3;
    [SerializeField] private Color activeColor = new Color(0.12f, 0.9f, 0.35f, 1f);
    [SerializeField] private Color inactiveColor = new Color(0.38f, 0.42f, 0.46f, 0.48f);
    [SerializeField, Min(1f)] private float dotSize = 46.5f;
    [SerializeField, Min(0f)] private float spacing = 5f;
    [SerializeField] private TextMeshProUGUI speedLabel;
    [SerializeField] private Sprite turtleSprite;
    [SerializeField] private Sprite hareSprite;
    [SerializeField] private Image turtleIcon;
    [SerializeField] private Image hareIcon;
    [SerializeField] private RectTransform speedDotsContainer;

    private readonly List<TextMeshProUGUI> dots = new List<TextMeshProUGUI>();

    private void Awake()
    {
        EnsureSpeedLabel();
        CollectExistingDots();
        if (dots.Count != 5) Rebuild(); else ApplyVisuals();
    }
    private void OnValidate() { currentSpeedLevel = Mathf.Clamp(currentSpeedLevel, 1, 5); ApplyVisuals(); }

    public void SetLevel(int level)
    {
        currentSpeedLevel = Mathf.Clamp(level, 1, 5);
        if (dots.Count != 5) Rebuild(); else ApplyVisuals();
    }

    private void Rebuild()
    {
        dots.Clear();
        if (speedDotsContainer == null) return;
        for (int index = speedDotsContainer.childCount - 1; index >= 0; index--)
            DestroySafe(speedDotsContainer.GetChild(index).gameObject);
        for (int index = 0; index < 5; index++)
        {
            var dotObject = new GameObject($"SpeedDot{index + 1}", typeof(RectTransform), typeof(TextMeshProUGUI));
            dotObject.transform.SetParent(speedDotsContainer, false);
            TextMeshProUGUI dot = dotObject.GetComponent<TextMeshProUGUI>();
            dot.text = "\u25CF";
            dot.alignment = TextAlignmentOptions.Center;
            dot.raycastTarget = false;
            dots.Add(dot);
        }
        ApplyVisuals();
    }

    private void CollectExistingDots()
    {
        dots.Clear();
        if (speedDotsContainer == null) return;
        for (int index = 0; index < speedDotsContainer.childCount; index++)
        {
            TextMeshProUGUI dot = speedDotsContainer.GetChild(index).GetComponent<TextMeshProUGUI>();
            if (dot != null) dots.Add(dot);
        }
    }

    private void ApplyVisuals()
    {
        if (turtleIcon != null) { turtleIcon.sprite = turtleSprite; turtleIcon.preserveAspect = true; }
        if (hareIcon != null) { hareIcon.sprite = hareSprite; hareIcon.preserveAspect = true; }
        if (speedDotsContainer != null)
        {
            HorizontalLayoutGroup layout = speedDotsContainer.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = speedDotsContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }
        for (int index = 0; index < dots.Count; index++)
        {
            TextMeshProUGUI dot = dots[index];
            if (dot == null) continue;
            dot.fontSize = dotSize;
            dot.color = index == currentSpeedLevel - 1 ? activeColor : inactiveColor;
            dot.rectTransform.sizeDelta = new Vector2(dotSize, dotSize + 4f);
            dot.rectTransform.localScale = Vector3.one * (index == currentSpeedLevel - 1 ? 1.08f : 0.82f);
        }
    }

    private void EnsureSpeedLabel()
    {
        if (speedLabel == null)
        {
            Transform existing = transform.Find("SpeedLabel");
            if (existing != null) speedLabel = existing.GetComponent<TextMeshProUGUI>();
        }
        if (speedLabel == null)
        {
            var labelObject = new GameObject("SpeedLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(transform, false);
            speedLabel = labelObject.GetComponent<TextMeshProUGUI>();
        }
        speedLabel.text = "Speed";
        speedLabel.fontSize = 30f;
        speedLabel.fontStyle = FontStyles.Bold;
        speedLabel.alignment = TextAlignmentOptions.Center;
        speedLabel.enableWordWrapping = false;
        speedLabel.color = new Color(0.12f, 0.18f, 0.22f, 0.9f);
        speedLabel.raycastTarget = false;
        RectTransform rect = speedLabel.rectTransform;
        rect.anchorMin = new Vector2(0.1f, 0.67f);
        rect.anchorMax = new Vector2(0.9f, 0.94f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void DestroySafe(GameObject target)
    {
        if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        RectTransform dotsContainer,
        Image slowIcon,
        Image fastIcon,
        Sprite slowSprite,
        Sprite fastSprite,
        float configuredDotSize = 19f,
        float configuredSpacing = 5f,
        Color? configuredInactiveColor = null)
    {
        speedDotsContainer = dotsContainer;
        turtleIcon = slowIcon;
        hareIcon = fastIcon;
        turtleSprite = slowSprite;
        hareSprite = fastSprite;
        dotSize = configuredDotSize;
        spacing = configuredSpacing;
        inactiveColor = configuredInactiveColor ?? new Color(0.55f, 0.59f, 0.63f, 1f);
        Rebuild();
    }
#endif
}
