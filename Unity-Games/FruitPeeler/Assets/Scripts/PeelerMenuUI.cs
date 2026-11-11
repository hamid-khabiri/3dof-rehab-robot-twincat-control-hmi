using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────────────────
//  PEELER MENUS + POPUPS + HUD (Crazy Car / Cargo Sorter look)
//  Shows the panel that matches PeelerSessionFlow's screen and forwards
//  button / slider input to it:
//
//      Main menu  : one button per fruit level + red Exit Game
//      Settings   : fruit name, Speed slider (Slow / Normal / Fast), Start, Back
//      HUD        : fruit + stage badge (top left), speed badge + pause (top right)
//      Pause      : Resume, Main Menu            (Esc or P also toggles it)
//      Result     : session summary, Play Again, Next Fruit, Main Menu
//
//  Everything is built from code at startup on a canvas that survives scene
//  loads, so no scene has to be edited. Font and icons are loaded from
//  Assets/Resources/PeelerUI (copied from Cargo Sorter).
// ─────────────────────────────────────────────────────────────────────────

public class PeelerMenuUI : MonoBehaviour
{
    private const string FontFolder = "PeelerUI/Fonts/";
    private const string IconFolder = "PeelerUI/Icons/";

    private static readonly Color PanelColor = new Color(0.06f, 0.09f, 0.13f, 0.80f);
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.30f);
    private static readonly Color ButtonColor = new Color(0.14f, 0.48f, 0.63f, 1f);
    private static readonly Color CompactButtonColor = new Color(0.06f, 0.09f, 0.13f, 0.65f);
    private static readonly Color SecondaryButtonColor = new Color(0.30f, 0.36f, 0.42f, 1f);
    private static readonly Color ExitButtonColor = new Color(0.72f, 0.25f, 0.22f, 1f);
    private static readonly Color TitleColor = Color.white;
    private static readonly Color MutedTextColor = new Color(0.74f, 0.80f, 0.85f, 1f);
    private static readonly Color AccentTextColor = new Color(0.45f, 0.85f, 1f, 1f);
    private static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.14f);
    private static readonly Color SliderTrackColor = new Color(1f, 1f, 1f, 0.20f);
    private static readonly Color SliderFillColor = new Color(0.25f, 0.70f, 0.90f, 1f);
    private static readonly Color SliderTickColor = new Color(1f, 1f, 1f, 0.55f);
    private static readonly Color BadgeColor = new Color(1f, 0.98f, 0.95f, 0.50f);
    private static readonly Color BadgeTextColor = new Color(0.13f, 0.11f, 0.10f, 1f);
    private static readonly Color DotActiveColor = new Color(0.12f, 0.75f, 0.30f, 1f);
    private static readonly Color DotInactiveColor = new Color(0.20f, 0.18f, 0.16f, 0.40f);

    private const float SliderHandleSize = 44f;
    private const float SliderTrackHeight = 12f;
    private const float SliderTickSize = 8f;
    private const float PopupWidth = 640f;
    private const float WideButtonWidth = 460f;
    private const float ButtonHeight = 66f;
    private const float ButtonSpacing = 80f;

    private PeelerSessionFlow flow;
    private TMP_FontAsset regularFont;
    private TMP_FontAsset boldFont;
    private Sprite roundedSprite;
    private Sprite circleSprite;

    private GameObject dimmer;
    private GameObject mainMenuPanel;
    private GameObject settingsPanel;
    private GameObject hudPanel;
    private GameObject pausePanel;
    private GameObject resultPanel;

    private TextMeshProUGUI settingsTitle;
    private Slider speedSlider;
    private TextMeshProUGUI speedValueText;

    private TextMeshProUGUI hudFruitText;
    private TextMeshProUGUI hudStageText;
    private Image[] hudSpeedDots;

    private TextMeshProUGUI resultSubtitle;
    private TextMeshProUGUI resultText;
    private GameObject nextFruitButton;

    #region Unity Lifecycle
    private void Start()
    {
        flow = GetComponent<PeelerSessionFlow>();
        if (flow == null)
        {
            Debug.LogWarning("[PeelerMenuUI] No PeelerSessionFlow on this object; menus disabled.", this);
            enabled = false;
            return;
        }

        LoadAssets();
        Transform canvas = BuildCanvas();
        BuildHud(canvas);
        dimmer = CreateImage("Dimmer", canvas, null, DimColor);
        Stretch(dimmer.GetComponent<RectTransform>());
        BuildMainMenu(canvas);
        BuildSettings(canvas);
        BuildPause(canvas);
        BuildResult(canvas);

        flow.OnScreenChanged += HandleScreenChanged;
        flow.OnPausedChanged += HandlePausedChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        EnsureEventSystem();
        Refresh();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (flow == null) return;
        flow.OnScreenChanged -= HandleScreenChanged;
        flow.OnPausedChanged -= HandlePausedChanged;
    }

    private void Update()
    {
        if (flow.CurrentScreen != PeelerScreen.Playing) return;

        // Escape or P toggles the pause popup while playing.
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
            flow.SetPaused(!flow.IsPaused);

        RefreshHud();
    }
    #endregion

    #region Screen Switching
    private void HandleScreenChanged(PeelerScreen screen) => Refresh();

    private void HandlePausedChanged(bool paused) => Refresh();

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureEventSystem();

    private void Refresh()
    {
        PeelerScreen screen = flow.CurrentScreen;
        bool playing = screen == PeelerScreen.Playing;

        SetActive(mainMenuPanel, screen == PeelerScreen.MainMenu);
        SetActive(settingsPanel, screen == PeelerScreen.Settings);
        SetActive(hudPanel, playing);
        SetActive(pausePanel, playing && flow.IsPaused);
        SetActive(resultPanel, screen == PeelerScreen.Result);
        SetActive(dimmer, !flow.IsPlaying);

        if (screen == PeelerScreen.Settings) RefreshSettings();
        if (screen == PeelerScreen.Result) RefreshResult();
        if (playing) RefreshHud();
    }

    private void RefreshSettings()
    {
        settingsTitle.text = flow.GetLevelDisplayName(flow.SelectedLevelIndex);
        speedSlider.SetValueWithoutNotify(flow.SpeedLevel);
        speedValueText.text = PeelerSessionFlow.SpeedName(flow.SpeedLevel);
    }

    private void RefreshHud()
    {
        int levelIndex = GlobalLevelManager.Instance != null
            ? GlobalLevelManager.Instance.GetCurrentLevelIndex()
            : flow.SelectedLevelIndex;
        hudFruitText.text = flow.GetLevelDisplayName(levelIndex);

        GameManager gameManager = GameManager.Instance;
        hudStageText.text = gameManager != null
            ? $"Stage {gameManager.CurrentStageNumber} / {gameManager.StageCount}"
            : string.Empty;

        for (int i = 0; i < hudSpeedDots.Length; i++)
        {
            bool active = i == flow.SpeedLevel - 1;
            hudSpeedDots[i].color = active ? DotActiveColor : DotInactiveColor;
            hudSpeedDots[i].rectTransform.localScale = Vector3.one * (active ? 1.15f : 0.85f);
        }
    }

    private void RefreshResult()
    {
        string fruitName = flow.GetLevelDisplayName(flow.SelectedLevelIndex);
        resultSubtitle.text = $"{fruitName}  ·  {PeelerSessionFlow.SpeedName(flow.SpeedLevel)} speed";

        SessionMetrics metrics = GlobalLevelManager.Instance != null ? GlobalLevelManager.Instance.SessionMetrics : null;
        resultText.text = metrics == null
            ? "All stages peeled!"
            : $"Successful peels   <b>{metrics.SuccessfulPeels}</b>\n" +
              $"Missed attempts   <b>{metrics.FailedOrMissedAttempts}</b>\n" +
              $"Success rate   <b>{Mathf.RoundToInt(metrics.PeelSuccessRate * 100f)} %</b>\n" +
              $"Time   <b>{FormatDuration(metrics.DurationSeconds)}</b>";

        SetActive(nextFruitButton, flow.HasNextLevel);
    }

    private void HandleSpeedChanged(float value)
    {
        flow.SetSpeedLevel(Mathf.RoundToInt(value));
        RefreshSettings();
    }
    #endregion

    #region Build: Canvas, HUD, Popups
    private Transform BuildCanvas()
    {
        GameObject canvasObject = new GameObject("PeelerMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // above the scene canvases

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvasObject.transform;
    }

    private void BuildHud(Transform canvas)
    {
        hudPanel = new GameObject("HUD", typeof(RectTransform));
        hudPanel.transform.SetParent(canvas, false);
        Stretch(hudPanel.GetComponent<RectTransform>());

        // Top left: fruit name + stage.
        GameObject stageBadge = CreateBadge("StageBadge", hudPanel.transform, new Vector2(0f, 1f), new Vector2(12f, -16f), new Vector2(300f, 100f));
        hudFruitText = CreateText(stageBadge.transform, "Fruit", 32f, true, BadgeTextColor, new Vector2(0f, -14f), new Vector2(260f, 40f));
        hudStageText = CreateText(stageBadge.transform, "Stage", 24f, false, BadgeTextColor, new Vector2(0f, -56f), new Vector2(260f, 32f));

        // Top right: pause button.
        Button pauseButton = CreateButton(hudPanel.transform, string.Empty, CompactButtonColor, Vector2.zero, new Vector2(80f, 80f), () => flow.SetPaused(true));
        Anchor(pauseButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(-32f, -32f));
        for (int i = 0; i < 2; i++)
        {
            GameObject bar = CreateImage("PauseBar", pauseButton.transform, null, Color.white);
            RectTransform barRect = bar.GetComponent<RectTransform>();
            barRect.sizeDelta = new Vector2(10f, 34f);
            barRect.anchoredPosition = new Vector2(i == 0 ? -10f : 10f, 0f);
            bar.GetComponent<Image>().raycastTarget = false;
        }

        // Top left, under the fruit badge: speed badge (turtle, dots, hare).
        GameObject speedBadge = CreateBadge("SpeedBadge", hudPanel.transform, new Vector2(0f, 1f), new Vector2(12f, -128f), new Vector2(260f, 80f));
        CreateIcon(speedBadge.transform, "Turtle_Dark", new Vector2(-90f, 0f), 46f);
        CreateIcon(speedBadge.transform, "Hare_Dark", new Vector2(90f, 0f), 46f);
        hudSpeedDots = new Image[PeelerSessionFlow.MaxSpeedLevel];
        for (int i = 0; i < hudSpeedDots.Length; i++)
        {
            GameObject dot = CreateImage("SpeedDot", speedBadge.transform, circleSprite, DotInactiveColor);
            RectTransform dotRect = dot.GetComponent<RectTransform>();
            dotRect.sizeDelta = new Vector2(22f, 22f);
            dotRect.anchoredPosition = new Vector2((i - 1) * 36f, 0f);
            hudSpeedDots[i] = dot.GetComponent<Image>();
        }
    }

    private void BuildMainMenu(Transform canvas)
    {
        int levelCount = flow.GetLevelCount();
        float height = 210f + levelCount * ButtonSpacing + 24f + ButtonSpacing + 20f;
        mainMenuPanel = CreatePopup("MainMenuPanel", canvas, height);

        float y = -44f;
        CreateText(mainMenuPanel.transform, "Fruit Peeler", 56f, true, TitleColor, new Vector2(0f, y), new Vector2(PopupWidth - 60f, 72f));
        y -= 76f;
        CreateText(mainMenuPanel.transform, "Choose a fruit for this session", 26f, false, MutedTextColor, new Vector2(0f, y), new Vector2(PopupWidth - 60f, 40f));
        y -= 60f;
        CreateDivider(mainMenuPanel.transform, y);
        y -= 30f;

        for (int i = 0; i < levelCount; i++)
        {
            int levelIndex = i;
            string label = $"Level {i + 1}  ·  {flow.GetLevelDisplayName(i)}";
            CreateButton(mainMenuPanel.transform, label, ButtonColor, new Vector2(0f, y), new Vector2(WideButtonWidth, ButtonHeight), () => flow.ChooseLevel(levelIndex));
            y -= ButtonSpacing;
        }

        y -= 24f;
        CreateButton(mainMenuPanel.transform, "Exit Game", ExitButtonColor, new Vector2(0f, y), new Vector2(WideButtonWidth, ButtonHeight), flow.QuitGame);
    }

    private void BuildSettings(Transform canvas)
    {
        settingsPanel = CreatePopup("SettingsPanel", canvas, 600f, 760f);

        float y = -44f;
        settingsTitle = CreateText(settingsPanel.transform, "Fruit", 56f, true, TitleColor, new Vector2(0f, y), new Vector2(680f, 72f));
        y -= 76f;
        CreateText(settingsPanel.transform, "Choose the peeler speed", 26f, false, MutedTextColor, new Vector2(0f, y), new Vector2(680f, 40f));
        y -= 60f;
        CreateDivider(settingsPanel.transform, y);
        y -= 34f;

        TextMeshProUGUI speedLabel = CreateText(settingsPanel.transform, "Speed", 32f, true, TitleColor, new Vector2(-150f, y), new Vector2(300f, 44f));
        speedLabel.alignment = TextAlignmentOptions.Left;
        speedValueText = CreateText(settingsPanel.transform, "Normal", 30f, true, AccentTextColor, new Vector2(150f, y), new Vector2(300f, 44f));
        speedValueText.alignment = TextAlignmentOptions.Right;
        y -= 72f;

        CreateIcon(settingsPanel.transform, "Turtle_Light", new Vector2(-270f, y - 4f), 52f, topAnchored: true);
        CreateIcon(settingsPanel.transform, "Hare_Light", new Vector2(270f, y - 4f), 52f, topAnchored: true);
        speedSlider = CreateSlider(settingsPanel.transform, new Vector2(0f, y), 440f,
            PeelerSessionFlow.MinSpeedLevel, PeelerSessionFlow.MaxSpeedLevel);
        speedSlider.onValueChanged.AddListener(HandleSpeedChanged);
        y -= 100f;

        CreateButton(settingsPanel.transform, "Start", ButtonColor, new Vector2(0f, y), new Vector2(WideButtonWidth, ButtonHeight), flow.StartGame);
        y -= ButtonSpacing;
        CreateButton(settingsPanel.transform, "Back", SecondaryButtonColor, new Vector2(0f, y), new Vector2(WideButtonWidth, ButtonHeight), flow.ShowMainMenu);
    }

    private void BuildPause(Transform canvas)
    {
        pausePanel = CreatePopup("PausePanel", canvas, 360f);

        float y = -44f;
        CreateText(pausePanel.transform, "Paused", 56f, true, TitleColor, new Vector2(0f, y), new Vector2(PopupWidth - 60f, 72f));
        y -= 100f;
        CreateButton(pausePanel.transform, "Resume", ButtonColor, new Vector2(0f, y), new Vector2(WideButtonWidth, ButtonHeight), () => flow.SetPaused(false));
        y -= ButtonSpacing;
        CreateButton(pausePanel.transform, "Main Menu", SecondaryButtonColor, new Vector2(0f, y), new Vector2(WideButtonWidth, ButtonHeight), flow.ShowMainMenu);
    }

    private void BuildResult(Transform canvas)
    {
        resultPanel = CreatePopup("ResultPanel", canvas, 690f);

        float y = -44f;
        CreateText(resultPanel.transform, "Well done!", 56f, true, TitleColor, new Vector2(0f, y), new Vector2(PopupWidth - 60f, 72f));
        y -= 76f;
        resultSubtitle = CreateText(resultPanel.transform, "Fruit", 26f, false, MutedTextColor, new Vector2(0f, y), new Vector2(PopupWidth - 60f, 40f));
        y -= 60f;
        CreateDivider(resultPanel.transform, y);
        y -= 24f;
        resultText = CreateText(resultPanel.transform, string.Empty, 28f, false, TitleColor, new Vector2(0f, y), new Vector2(PopupWidth - 100f, 180f));
        resultText.lineSpacing = 12f;
        y -= 210f;

        CreateButton(resultPanel.transform, "Play Again", ButtonColor, new Vector2(0f, y), new Vector2(WideButtonWidth, ButtonHeight), flow.PlayAgain);
        y -= ButtonSpacing;
        nextFruitButton = CreateButton(resultPanel.transform, "Next Fruit", ButtonColor, new Vector2(0f, y), new Vector2(WideButtonWidth, ButtonHeight), flow.ChooseNextLevel).gameObject;
        y -= ButtonSpacing;
        CreateButton(resultPanel.transform, "Main Menu", SecondaryButtonColor, new Vector2(0f, y), new Vector2(WideButtonWidth, ButtonHeight), flow.ShowMainMenu);
    }
    #endregion

    #region UI Element Helpers
    private GameObject CreatePopup(string objectName, Transform canvas, float height, float width = PopupWidth)
    {
        GameObject panel = CreateImage(objectName, canvas, roundedSprite, PanelColor);
        Image image = panel.GetComponent<Image>();
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 0.6f; // rounder corners

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);

        Shadow shadow = panel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        shadow.effectDistance = new Vector2(0f, -8f);
        return panel;
    }

    private GameObject CreateBadge(string objectName, Transform parent, Vector2 corner, Vector2 offset, Vector2 size)
    {
        GameObject badge = CreateImage(objectName, parent, roundedSprite, BadgeColor);
        Image image = badge.GetComponent<Image>();
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 0.45f; // very rounded
        image.raycastTarget = false;

        RectTransform rect = badge.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        Anchor(rect, corner, offset);
        return badge;
    }

    /// <summary>Text centred horizontally, positioned from the top of its parent.</summary>
    private TextMeshProUGUI CreateText(Transform parent, string text, float fontSize, bool bold, Color color, Vector2 topOffset, Vector2 size)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TopAnchor(textObject.GetComponent<RectTransform>(), topOffset, size);

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        if (bold ? boldFont != null : regularFont != null) label.font = bold ? boldFont : regularFont;
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = bold && boldFont == null ? FontStyles.Bold : FontStyles.Normal;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    private Button CreateButton(Transform parent, string labelText, Color color, Vector2 topOffset, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = CreateImage(labelText + "Button", parent, roundedSprite, color);
        buttonObject.AddComponent<Button>();
        Image image = buttonObject.GetComponent<Image>();
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 0.9f;
        TopAnchor(buttonObject.GetComponent<RectTransform>(), topOffset, size);

        Shadow shadow = buttonObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
        shadow.effectDistance = new Vector2(0f, -3f);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.70f, 0.70f, 0.70f, 1f);
        colors.selectedColor = colors.normalColor; // a clicked button does not stay lit
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(onClick);

        if (!string.IsNullOrEmpty(labelText))
        {
            TextMeshProUGUI label = CreateText(buttonObject.transform, labelText, 30f, true, Color.white, Vector2.zero, size);
            Stretch(label.rectTransform);
        }

        return button;
    }

    private Slider CreateSlider(Transform parent, Vector2 topOffset, float width, int minValue, int maxValue)
    {
        GameObject sliderObject = new GameObject("SpeedSlider", typeof(RectTransform));
        sliderObject.transform.SetParent(parent, false);
        TopAnchor(sliderObject.GetComponent<RectTransform>(), topOffset, new Vector2(width, SliderHandleSize));
        float halfKnob = SliderHandleSize * 0.5f;

        GameObject track = CreateImage("Track", sliderObject.transform, roundedSprite, SliderTrackColor);
        Image trackImage = track.GetComponent<Image>();
        trackImage.type = Image.Type.Sliced;
        trackImage.pixelsPerUnitMultiplier = 2.5f;
        RectTransform trackRect = HorizontalBand(track.GetComponent<RectTransform>(), SliderTrackHeight, 0f);

        GameObject fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObject.transform, false);
        HorizontalBand(fillArea.GetComponent<RectTransform>(), SliderTrackHeight, halfKnob);

        GameObject fill = CreateImage("Fill", fillArea.transform, roundedSprite, SliderFillColor);
        Image fillImage = fill.GetComponent<Image>();
        fillImage.type = Image.Type.Sliced;
        fillImage.pixelsPerUnitMultiplier = 2.5f;
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(-halfKnob, 0f); // start under the left end of the track
        fillRect.offsetMax = Vector2.zero;

        // One small dot per level, under the knob.
        int steps = maxValue - minValue;
        for (int i = 0; i <= steps; i++)
        {
            GameObject tick = CreateImage("Tick", sliderObject.transform, circleSprite, SliderTickColor);
            RectTransform tickRect = tick.GetComponent<RectTransform>();
            tickRect.anchorMin = tickRect.anchorMax = new Vector2(0f, 0.5f);
            tickRect.sizeDelta = new Vector2(SliderTickSize, SliderTickSize);
            tickRect.anchoredPosition = new Vector2(halfKnob + (width - SliderHandleSize) * i / Mathf.Max(1, steps), 0f);
            tick.GetComponent<Image>().raycastTarget = false;
        }

        // Unity's Slider stretches the handle to the slide area's height, so the area
        // is exactly one knob high and the handle is (knob, 0): the knob stays round.
        GameObject handleArea = new GameObject("HandleSlideArea", typeof(RectTransform));
        handleArea.transform.SetParent(sliderObject.transform, false);
        HorizontalBand(handleArea.GetComponent<RectTransform>(), SliderHandleSize, halfKnob);

        GameObject handle = CreateImage("Handle", handleArea.transform, circleSprite, Color.white);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(SliderHandleSize, 0f);
        Shadow handleShadow = handle.AddComponent<Shadow>();
        handleShadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        handleShadow.effectDistance = new Vector2(0f, -3f);

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.wholeNumbers = true;
        slider.navigation = new Navigation { mode = Navigation.Mode.None }; // arrow keys also move the peeler
        slider.SetValueWithoutNotify(PeelerSessionFlow.NormalSpeedLevel);

        // The whole track is clickable, not only the knob.
        trackImage.raycastTarget = true;
        return slider;
    }

    private void CreateDivider(Transform parent, float topY)
    {
        GameObject divider = CreateImage("Divider", parent, null, DividerColor);
        TopAnchor(divider.GetComponent<RectTransform>(), new Vector2(0f, topY), new Vector2(PopupWidth - 120f, 2f));
        divider.GetComponent<Image>().raycastTarget = false;
    }

    private void CreateIcon(Transform parent, string iconName, Vector2 position, float size, bool topAnchored = false)
    {
        Sprite sprite = Resources.Load<Sprite>(IconFolder + iconName);
        if (sprite == null)
        {
            Debug.LogWarning($"[PeelerMenuUI] Icon '{iconName}' not found in Resources/{IconFolder}.", this);
            return;
        }

        GameObject icon = CreateImage(iconName, parent, sprite, Color.white);
        Image image = icon.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;

        RectTransform rect = icon.GetComponent<RectTransform>();
        if (topAnchored) TopAnchor(rect, position, new Vector2(size, size));
        else { rect.sizeDelta = new Vector2(size, size); rect.anchoredPosition = position; }
    }

    private static GameObject CreateImage(string objectName, Transform parent, Sprite sprite, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return imageObject;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>Anchors to the top centre of the parent; offset.y is measured down from the top.</summary>
    private static void TopAnchor(RectTransform rect, Vector2 offset, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = offset;
    }

    /// <summary>Anchors to a screen corner; offset moves it inwards from that corner.</summary>
    private static void Anchor(RectTransform rect, Vector2 corner, Vector2 offset)
    {
        rect.anchorMin = rect.anchorMax = corner;
        rect.pivot = corner;
        rect.anchoredPosition = offset;
    }

    /// <summary>Full width minus padding on each side, fixed height, vertically centred.</summary>
    private static RectTransform HorizontalBand(RectTransform rect, float height, float sidePadding)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(-2f * sidePadding, height);
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active) target.SetActive(active);
    }

    private static string FormatDuration(float durationSeconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(durationSeconds));
        return (totalSeconds / 60).ToString("0") + ":" + (totalSeconds % 60).ToString("00");
    }
    #endregion

    #region Assets / Event System
    private void LoadAssets()
    {
        regularFont = Resources.Load<TMP_FontAsset>(FontFolder + "Poppins-Medium SDF");
        boldFont = Resources.Load<TMP_FontAsset>(FontFolder + "Poppins-SemiBold SDF");
        if (regularFont == null || boldFont == null)
            Debug.LogWarning("[PeelerMenuUI] Poppins font assets not found in Resources/" + FontFolder + "; using the TMP default font.", this);

        roundedSprite = CreateRoundedSprite(64, 24);
        circleSprite = CreateRoundedSprite(64, 32);
    }

    /// <summary>White rounded square with soft edges; radius = size/2 gives a circle.
    /// Sliced borders equal the radius so corners keep their shape at any size.</summary>
    private static Sprite CreateRoundedSprite(int size, int radius)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float r = radius;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                float nx = Mathf.Clamp(px, r, size - r);
                float ny = Mathf.Clamp(py, r, size - r);
                float distance = Mathf.Sqrt((px - nx) * (px - nx) + (py - ny) * (py - ny));
                float alpha = Mathf.Clamp01(r - distance + 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply();

        float border = Mathf.Min(radius, size / 2 - 1);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    /// <summary>Buttons need an EventSystem in the active scene. The fruit scenes have one;
    /// this only adds one if a scene does not.</summary>
    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
    #endregion
}
