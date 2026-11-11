using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TrialProgressDotsUI : MonoBehaviour
{
    [SerializeField, Min(0)] private int totalTrials;
    [SerializeField, Min(0)] private int completedTrials;
    [SerializeField] private Color correctColor = new Color(0.2f, 0.78f, 0.36f, 1f);
    [SerializeField] private Color missColor = new Color(1f, 0.55f, 0.08f, 1f);
    [SerializeField] private Color wrongBinColor = new Color(0.9f, 0.16f, 0.14f, 1f);
    [SerializeField] private Color futureColor = new Color(0.55f, 0.59f, 0.63f, 0.68f);
    [SerializeField] private Color currentColor = Color.white;
    [SerializeField, Min(1f)] private float dotSize = 52.5f;
    [SerializeField, Min(0f)] private float spacing = 4f;
    [SerializeField] private RectTransform dotsContainer;

    private readonly List<TextMeshProUGUI> dots = new List<TextMeshProUGUI>();
    private readonly List<TrialOutcome> outcomes = new List<TrialOutcome>();

    public int TotalTrials => totalTrials;
    public int CompletedTrials => completedTrials;

    private void Awake()
    {
        CollectExistingDots();
        if (dots.Count != totalTrials) Rebuild(); else ApplyLayout();
    }
    // Unity forbids adding/destroying components in OnValidate, so only restyle once the grid layout already exists.
    private void OnValidate()
    {
        if (dotsContainer != null && dotsContainer.GetComponent<GridLayoutGroup>() != null
            && dotsContainer.GetComponent<HorizontalLayoutGroup>() == null)
            ApplyLayout();
    }

    public void SetProgress(int completed, int total)
    {
        total = Mathf.Max(0, total);
        completed = Mathf.Clamp(completed, 0, total);
        bool needsRebuild = totalTrials != total || dots.Count != total;
        totalTrials = total;
        completedTrials = completed;
        if (completed == 0) outcomes.Clear();
        if (needsRebuild) Rebuild(); else RefreshColors();
    }

    public void RecordOutcome(TrialOutcome outcome)
    {
        if (completedTrials >= totalTrials) return;
        if (outcomes.Count == completedTrials) outcomes.Add(outcome);
        else if (completedTrials < outcomes.Count) outcomes[completedTrials] = outcome;
        RefreshColors();
    }

    private void Rebuild()
    {
        if (dotsContainer == null) return;
        for (int index = dotsContainer.childCount - 1; index >= 0; index--)
            DestroySafe(dotsContainer.GetChild(index).gameObject);
        dots.Clear();

        for (int index = 0; index < totalTrials; index++)
        {
            var dotObject = new GameObject($"TrialDot{index + 1}", typeof(RectTransform), typeof(TextMeshProUGUI));
            dotObject.transform.SetParent(dotsContainer, false);
            TextMeshProUGUI dot = dotObject.GetComponent<TextMeshProUGUI>();
            dot.text = "\u25CF";
            dot.alignment = TextAlignmentOptions.Center;
            dot.raycastTarget = false;
            dots.Add(dot);
        }
        ApplyLayout();
    }

    private void CollectExistingDots()
    {
        dots.Clear();
        if (dotsContainer == null) return;
        for (int index = 0; index < dotsContainer.childCount; index++)
        {
            TextMeshProUGUI dot = dotsContainer.GetChild(index).GetComponent<TextMeshProUGUI>();
            if (dot != null) dots.Add(dot);
        }
    }

    private void ApplyLayout()
    {
        if (dotsContainer == null) return;
        HorizontalLayoutGroup oldLayout = dotsContainer.GetComponent<HorizontalLayoutGroup>();
        if (oldLayout != null)
        {
            oldLayout.enabled = false;
            // Destroy() is deferred in play mode, which would block adding the GridLayoutGroup below.
            if (Application.isPlaying) DestroyImmediate(oldLayout); else DestroySafe(oldLayout);
        }
        GridLayoutGroup layout = dotsContainer.GetComponent<GridLayoutGroup>();
        if (layout == null) layout = dotsContainer.gameObject.AddComponent<GridLayoutGroup>();
        if (layout == null) return;
        int fittingColumns = Mathf.Max(1, Mathf.FloorToInt((dotsContainer.rect.width + spacing) / (dotSize + spacing)));
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = Mathf.Min(Mathf.Max(1, totalTrials), fittingColumns);
        layout.cellSize = new Vector2(dotSize, dotSize);
        layout.spacing = new Vector2(spacing, spacing);
        layout.childAlignment = TextAnchor.MiddleLeft;
        foreach (TextMeshProUGUI dot in dots)
        {
            if (dot == null) continue;
            dot.fontSize = dotSize;
            dot.rectTransform.sizeDelta = new Vector2(dotSize, dotSize);
        }
        RefreshColors();
    }

    private void RefreshColors()
    {
        for (int index = 0; index < dots.Count; index++)
        {
            TextMeshProUGUI dot = dots[index];
            if (dot == null) continue;
            bool hasOutcome = index < outcomes.Count;
            bool isCurrent = completedTrials < totalTrials && index == completedTrials;
            dot.text = hasOutcome ? "\u25CF" : "\u25CB";
            dot.color = hasOutcome ? OutcomeColor(outcomes[index]) : isCurrent ? currentColor : futureColor;
            dot.rectTransform.localScale = Vector3.one * (isCurrent ? 1.05f : 1f);
        }
    }

    private Color OutcomeColor(TrialOutcome outcome)
    {
        switch (outcome)
        {
            case TrialOutcome.Correct: return correctColor;
            case TrialOutcome.Miss: return missColor;
            case TrialOutcome.WrongBin: return wrongBinColor;
            default: return futureColor;
        }
    }

    private static void DestroySafe(Object target)
    {
        if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        RectTransform container,
        float configuredDotSize = 20f,
        float configuredSpacing = 2f,
        Color? configuredIncompleteColor = null,
        Color? configuredCurrentColor = null)
    {
        dotsContainer = container;
        dotSize = configuredDotSize;
        spacing = configuredSpacing;
        futureColor = configuredIncompleteColor ?? new Color(0.55f, 0.59f, 0.63f, 0.68f);
        currentColor = configuredCurrentColor ?? Color.white;
        Rebuild();
    }
#endif
}
