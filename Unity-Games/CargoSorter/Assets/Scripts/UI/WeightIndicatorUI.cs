using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD badge under the speed indicator: three rising bars, filled up to the selected bin weight.
public sealed class WeightIndicatorUI : MonoBehaviour
{
    [SerializeField, Range(SettingLevels.MinWeight, SettingLevels.MaxWeight)] private int currentWeightLevel = SettingLevels.MinWeight;
    [SerializeField] private Image[] bars = new Image[0];
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Color lightColor = new Color(0.12f, 0.9f, 0.35f, 1f);
    [SerializeField] private Color mediumColor = new Color(1f, 0.67f, 0.1f, 1f);
    [SerializeField] private Color heavyColor = new Color(0.92f, 0.22f, 0.18f, 1f);
    [SerializeField] private Color inactiveColor = new Color(0.55f, 0.59f, 0.63f, 0.7f);

    private void Awake() { ApplyVisuals(); }
    private void OnValidate() { ApplyVisuals(); }

    public void SetLevel(int level)
    {
        currentWeightLevel = Mathf.Clamp(level, SettingLevels.MinWeight, SettingLevels.MaxWeight);
        ApplyVisuals();
    }

    private void ApplyVisuals()
    {
        int level = Mathf.Clamp(currentWeightLevel, SettingLevels.MinWeight, SettingLevels.MaxWeight);
        Color active = level == 1 ? lightColor : level == 2 ? mediumColor : heavyColor;
        for (int index = 0; index < bars.Length; index++)
            if (bars[index] != null) bars[index].color = index < level ? active : inactiveColor;
        if (levelText != null) levelText.text = SettingLevels.WeightName(level);
    }

#if UNITY_EDITOR
    public void EditorConfigure(Image[] weightBars, TextMeshProUGUI weightLevelText, Color barInactiveColor)
    {
        bars = weightBars;
        levelText = weightLevelText;
        inactiveColor = barInactiveColor;
        ApplyVisuals();
    }
#endif
}
