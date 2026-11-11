#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CargoSorterDay3Setup
{
    private const string ScenePath = "Assets/Scenes/Gameplay.unity";
    // Popups stay fairly opaque so white text is readable over the busy kitchen photo.
    private static readonly Color PanelColor = new Color(0.06f, 0.09f, 0.13f, 0.80f);
    private static readonly Color ButtonColor = new Color(0.14f, 0.48f, 0.63f, 1f);
    private static readonly Color CompactButtonColor = new Color(0.06f, 0.09f, 0.13f, 0.65f);
    // In-game badges: warm white at 50% so the kitchen shows through.
    private static readonly Color HudBadgeColor = new Color(1f, 0.98f, 0.95f, 0.5f);
    private static readonly Color HudTextColor = new Color(0.13f, 0.11f, 0.10f, 1f);
    private static readonly Color HudInactiveColor = new Color(0.20f, 0.18f, 0.16f, 0.40f);
    private static readonly Color SecondaryButtonColor = new Color(0.30f, 0.36f, 0.42f, 1f);
    private static readonly Color ExitButtonColor = new Color(0.72f, 0.25f, 0.22f, 1f);
    private static readonly Color MutedTextColor = new Color(0.74f, 0.80f, 0.85f, 1f);
    private static readonly Color AccentTextColor = new Color(0.45f, 0.85f, 1f, 1f);
    private static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.14f);
    private static readonly Color SliderTrackColor = new Color(1f, 1f, 1f, 0.20f);
    private static readonly Color SliderFillColor = new Color(0.25f, 0.70f, 0.90f, 1f);
    private static readonly Color SliderTickColor = new Color(1f, 1f, 1f, 0.55f);
    private const float SliderHandleSize = 44f;
    private const float SliderTrackHeight = 12f;
    private const float SliderTickSize = 8f;
    private const string IconFolder = "Assets/Art/UI/Icons/";
    private const string FontFolder = "Assets/Art/Fonts/Poppins/";

    private static TMP_FontAsset uiFont;

    [MenuItem("Tools/Cargo Sorter/Setup Day 3 UI")]
    public static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        List<string> initiallyBrokenPaths = FindBrokenImageReferencePaths(scene, true);

        TrialManager trials = Object.FindAnyObjectByType<TrialManager>(FindObjectsInactive.Include);
        if (trials == null) { Debug.LogError("Day 3 setup requires the existing TrialManager."); return; }
        uiFont = LoadOrCreateUIFont();
        GameObject uiRoot = FindOrCreateRoot("UI");
        GameObject canvasObject = FindOrCreateUniqueDirect(uiRoot.transform, "GameplayCanvas");
        Canvas canvas = GetOrAdd<Canvas>(canvasObject); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        GetOrAdd<CanvasScaler>(canvasObject).uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        GetOrAdd<GraphicRaycaster>(canvasObject);
        EnsureEventSystem();
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(trials.gameObject);
        SessionFlowController flow = GetOrAdd<SessionFlowController>(trials.gameObject);
        flow.EditorConfigure(trials);
        GetOrAdd<GameAudio>(trials.gameObject).EditorConfigure(
            flow,
            trials,
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Background_Music.ogg"),
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Correct_Bin.wav"),
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Wrong_Bin2.wav"),
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Victory.wav"),
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Lost.mp3"));
        BinHighlightController highlights = GetOrAdd<BinHighlightController>(trials.gameObject);
        List<BinTarget> binTargets = new List<BinTarget>(Object.FindObjectsByType<BinTarget>(FindObjectsInactive.Include));
        highlights.EditorConfigure(binTargets);
        foreach (BinTarget binTarget in binTargets)
            if (binTarget != null) EditorUtility.SetDirty(binTarget.gameObject);

        RemoveDirect(canvasObject.transform, "CalibrationPanel");

        // Light icons sit on the dark popups, dark icons on the light in-game badges.
        Sprite turtleLight = LoadIcon("Turtle_Light"), hareLight = LoadIcon("Hare_Light");
        Sprite featherLight = LoadIcon("Feather_Light"), weightLight = LoadIcon("Weight_Light");
        Sprite turtleDark = LoadIcon("Turtle_Dark"), hareDark = LoadIcon("Hare_Dark");
        Sprite featherDark = LoadIcon("Feather_Dark"), weightDark = LoadIcon("Weight_Dark");

        GameObject menu = Panel(canvasObject.transform, "MainModeMenuPanel", new Vector2(0.30f, 0.08f), new Vector2(0.70f, 0.92f));
        Title(menu.transform, "Title", "Cargo Sorter", 64, new Vector2(0.06f, 0.80f), new Vector2(0.94f, 0.94f));
        Text(menu.transform, "Subtitle", "Choose the number of bins", 30, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.80f)).color = MutedTextColor;
        Divider(menu.transform, "Divider", 0.705f);
        Button threeBins = Button(menu.transform, "3BinsButton", "3 Bins", new Vector2(0.12f, 0.52f), new Vector2(0.88f, 0.65f));
        Button fiveBins = Button(menu.transform, "5BinsButton", "5 Bins", new Vector2(0.12f, 0.36f), new Vector2(0.88f, 0.49f));
        Button futureMode = Button(menu.transform, "FutureModeButton", "Mode 3 — Coming Soon", new Vector2(0.12f, 0.20f), new Vector2(0.88f, 0.33f));
        futureMode.interactable = false;
        Button exitGame = Button(menu.transform, "ExitButton", "Exit Game", new Vector2(0.28f, 0.05f), new Vector2(0.72f, 0.14f), ExitButtonColor);

        // Session settings: chosen here, before Guided/Unguided starts the block.
        GameObject submodeMenu = Panel(canvasObject.transform, "SubmodeMenuPanel", new Vector2(0.27f, 0.05f), new Vector2(0.73f, 0.95f));
        TextMeshProUGUI selectedMainModeTitle = Title(submodeMenu.transform, "SelectedMainModeTitle", "3 Bins", 58, new Vector2(0.08f, 0.86f), new Vector2(0.92f, 0.96f));
        Divider(submodeMenu.transform, "Divider", 0.845f);

        SettingLabel(submodeMenu.transform, "SpeedLabel", "Speed", 0.73f, 0.81f);
        TextMeshProUGUI speedValue = SettingValue(submodeMenu.transform, "SpeedValueText", 0.73f, 0.81f);
        Slider speedSlider = SettingSlider(submodeMenu.transform, "SpeedSlider", 0.64f, 0.72f, SettingLevels.MinSpeed, SettingLevels.MaxSpeed, trials.CurrentSpeedLevel);
        SliderEndIcon(submodeMenu.transform, "SpeedSlowIcon", turtleLight, 0.05f, 0.15f, 0.645f, 0.715f);
        SliderEndIcon(submodeMenu.transform, "SpeedFastIcon", hareLight, 0.85f, 0.95f, 0.645f, 0.715f);

        SettingLabel(submodeMenu.transform, "WeightLabel", "Bin Weight", 0.51f, 0.59f);
        TextMeshProUGUI weightValue = SettingValue(submodeMenu.transform, "WeightValueText", 0.51f, 0.59f);
        Slider weightSlider = SettingSlider(submodeMenu.transform, "WeightSlider", 0.42f, 0.50f, SettingLevels.MinWeight, SettingLevels.MaxWeight, SettingLevels.MinWeight);
        RemoveDirect(submodeMenu.transform, "WeightLightText");
        RemoveDirect(submodeMenu.transform, "WeightHeavyText");
        SliderEndIcon(submodeMenu.transform, "WeightLightIcon", featherLight, 0.06f, 0.14f, 0.425f, 0.495f);
        SliderEndIcon(submodeMenu.transform, "WeightHeavyIcon", weightLight, 0.86f, 0.94f, 0.425f, 0.495f);

        Text(submodeMenu.transform, "StartHintText", "Choose a mode to start", 26, new Vector2(0.08f, 0.31f), new Vector2(0.92f, 0.37f)).color = MutedTextColor;
        Button guided = Button(submodeMenu.transform, "GuidedButton", "Guided", new Vector2(0.08f, 0.18f), new Vector2(0.48f, 0.30f));
        Button category = Button(submodeMenu.transform, "UnguidedButton", "Unguided", new Vector2(0.52f, 0.18f), new Vector2(0.92f, 0.30f));
        Button submodeBack = Button(submodeMenu.transform, "BackButton", "Back", new Vector2(0.30f, 0.05f), new Vector2(0.70f, 0.14f), SecondaryButtonColor);
        RemoveDirect(canvasObject.transform, "MainMenuPanel");

        GameObject gameplay = Container(canvasObject.transform, "GameplayPanel", Vector2.zero, Vector2.one);
        RemoveComponent<Image>(gameplay);
        RemoveDirect(gameplay.transform, "InstructionText");
        RemoveDirect(gameplay.transform, "TrialProgressText");
        RemoveDirect(gameplay.transform, "ModeLabel");
        RemoveDirect(gameplay.transform, "ModeBadge");
        RemoveDirect(canvasObject.transform, "OutcomeFeedback");

        // Trial badge: title on top, two rows of dots with clear space above the bottom edge.
        GameObject trialBadge = Badge(gameplay.transform, "TrialBadge", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(360f, 190f));
        TextMeshProUGUI progress = Text(trialBadge.transform, "TrialProgressText", $"<b>Trial</b> 1 / {trials.TotalTrials}", 30, new Vector2(0.08f, 0.66f), new Vector2(0.92f, 0.94f));
        progress.alignment = TextAlignmentOptions.MidlineLeft;
        progress.color = HudTextColor;
        GameObject trialProgressDotsObject = Container(trialBadge.transform, "TrialProgressDotsUI", new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.64f));
        GameObject trialDotsContainer = Container(trialProgressDotsObject.transform, "TrialDotsContainer", Vector2.zero, Vector2.one);
        TrialProgressDotsUI trialProgressDots = GetOrAdd<TrialProgressDotsUI>(trialProgressDotsObject);
        trialProgressDots.EditorConfigure(
            trialDotsContainer.GetComponent<RectTransform>(),
            46f,
            6f,
            HudInactiveColor,
            HudTextColor);
        trialProgressDots.SetProgress(0, trials.TotalTrials);

        GameObject speedIndicatorObject = Badge(gameplay.transform, "SpeedIndicator", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -30f), new Vector2(420f, 120f));
        HudBadgeTitle(speedIndicatorObject.transform, "SpeedLabel", "Speed");
        Image turtleIcon = HudIcon(speedIndicatorObject.transform, "TurtleIcon", turtleDark, 0.05f, 0.20f);
        GameObject speedDotsObject = Container(speedIndicatorObject.transform, "SpeedDotsContainer", new Vector2(0.20f, 0.06f), new Vector2(0.80f, 0.465f));
        Image hareIcon = HudIcon(speedIndicatorObject.transform, "HareIcon", hareDark, 0.80f, 0.95f);
        SpeedIndicatorUI speedIndicator = GetOrAdd<SpeedIndicatorUI>(speedIndicatorObject);
        speedIndicator.EditorConfigure(
            speedDotsObject.GetComponent<RectTransform>(),
            turtleIcon,
            hareIcon,
            turtleDark,
            hareDark,
            46.5f,
            5f,
            HudInactiveColor);
        speedIndicator.SetLevel(trials.CurrentSpeedLevel);

        // Bin weight badge under the speed badge, laid out the same way: feather ... three rising bars ... weight.
        GameObject weightIndicatorObject = Badge(gameplay.transform, "WeightIndicator", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -162f), new Vector2(420f, 120f));
        HudBadgeTitle(weightIndicatorObject.transform, "WeightLabel", "Bin Weight");
        RemoveDirect(weightIndicatorObject.transform, "WeightLevelText");
        HudIcon(weightIndicatorObject.transform, "FeatherIcon", featherDark, 0.06f, 0.19f);
        HudIcon(weightIndicatorObject.transform, "WeightIcon", weightDark, 0.81f, 0.94f);
        var weightBars = new Image[SettingLevels.MaxWeight];
        for (int index = 0; index < weightBars.Length; index++)
        {
            GameObject bar = Container(weightIndicatorObject.transform, $"WeightBar{index + 1}",
                new Vector2(0.37f + index * 0.10f, 0.12f), new Vector2(0.44f + index * 0.10f, 0.28f + index * 0.16f));
            weightBars[index] = GetOrAdd<Image>(bar);
            ConfigureRoundedImage(weightBars[index], HudInactiveColor, 3f);
            weightBars[index].raycastTarget = false;
        }
        WeightIndicatorUI weightIndicator = GetOrAdd<WeightIndicatorUI>(weightIndicatorObject);
        weightIndicator.EditorConfigure(weightBars, null, HudInactiveColor);

        Sprite correctSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Feedback/Feedback_Correct_Green.png");
        Sprite wrongSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Feedback/Feedback_Wrong_Red.png");
        Sprite missSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Feedback/Miss_Exclamation_Orange.png");
        GameObject feedbackDisplay = FixedUIObject(gameplay.transform, "FeedbackIconDisplay", new Vector2(0.5f, 1f), new Vector2(0f, -205f), new Vector2(112f, 112f));
        Image feedbackImage = GetOrAdd<Image>(feedbackDisplay); feedbackImage.color = Color.white; feedbackImage.preserveAspect = true; feedbackImage.raycastTarget = false;
        CanvasGroup feedbackGroup = GetOrAdd<CanvasGroup>(feedbackDisplay); feedbackGroup.alpha = 0f; feedbackGroup.blocksRaycasts = false; feedbackGroup.interactable = false;
        OutcomeFeedbackUI feedback = GetOrAdd<OutcomeFeedbackUI>(feedbackDisplay);
        SerializedObject feedbackSerialized = new SerializedObject(feedback);
        Set(feedbackSerialized, "feedbackImage", feedbackImage); Set(feedbackSerialized, "canvasGroup", feedbackGroup);
        Set(feedbackSerialized, "correctSprite", correctSprite); Set(feedbackSerialized, "wrongSprite", wrongSprite); Set(feedbackSerialized, "missSprite", missSprite);
        feedbackSerialized.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject trialSerialized = new SerializedObject(trials); Set(trialSerialized, "outcomeFeedback", feedback); trialSerialized.ApplyModifiedPropertiesWithoutUndo();

        Sprite pauseSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Buttons/White_Pause.png");
        Sprite stopSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Buttons/White_Arow_Right.png");
        Button pause = IconButton(gameplay.transform, "PauseButton", pauseSprite, new Vector2(-38f, -30f));
        Button stop = IconButton(gameplay.transform, "StopButton", stopSprite, new Vector2(38f, -30f));

        GameObject pauseOverlay = Panel(canvasObject.transform, "PauseOverlay", new Vector2(0.34f, 0.25f), new Vector2(0.66f, 0.75f));
        Title(pauseOverlay.transform, "Title", "Paused", 52, new Vector2(0.08f, 0.70f), new Vector2(0.92f, 0.90f));
        Button resume = Button(pauseOverlay.transform, "ResumeButton", "Resume", new Vector2(0.16f, 0.40f), new Vector2(0.84f, 0.58f));
        Button pauseReturn = Button(pauseOverlay.transform, "PauseReturnToMenuButton", "Return to Menu", new Vector2(0.16f, 0.16f), new Vector2(0.84f, 0.34f));
        pauseOverlay.transform.SetAsLastSibling();

        GameObject summary = Panel(canvasObject.transform, "SummaryPanel", new Vector2(0.23f, 0.10f), new Vector2(0.77f, 0.90f));
        Title(summary.transform, "Title", "Activity Complete", 52, new Vector2(0.08f, 0.79f), new Vector2(0.92f, 0.94f));
        TextMeshProUGUI results = Text(summary.transform, "ResultsText", "Correct Deliveries: 0\nWrong Bin: 0\nMisses: 0\nAccuracy: 0%", 34, new Vector2(0.13f, 0.34f), new Vector2(0.87f, 0.77f));
        results.alignment = TextAlignmentOptions.MidlineLeft;
        Button again = Button(summary.transform, "PlayAgainButton", "Play Again", new Vector2(0.08f, 0.10f), new Vector2(0.47f, 0.27f));
        Button returnButton = Button(summary.transform, "ReturnToMenuButton", "Return to Menu", new Vector2(0.53f, 0.10f), new Vector2(0.92f, 0.27f));

        Day3UIController controller = GetOrAdd<Day3UIController>(canvasObject);
        SerializedObject serialized = new SerializedObject(controller);
        Set(serialized, "flow", flow); Set(serialized, "trials", trials); Set(serialized, "highlights", highlights);
        Set(serialized, "mainMenuPanel", menu); Set(serialized, "submodeMenuPanel", submodeMenu); Set(serialized, "gameplayPanel", gameplay); Set(serialized, "summaryPanel", summary);
        Set(serialized, "trialProgress", progress); Set(serialized, "summaryResults", results);
        Set(serialized, "threeBinsButton", threeBins); Set(serialized, "guidedButton", guided); Set(serialized, "categoryButton", category); Set(serialized, "submodeBackButton", submodeBack); Set(serialized, "playAgainButton", again); Set(serialized, "returnButton", returnButton);
        Set(serialized, "pauseButton", pause); Set(serialized, "stopButton", stop); Set(serialized, "pauseOverlay", pauseOverlay);
        Set(serialized, "trialProgressDots", trialProgressDots); Set(serialized, "speedIndicator", speedIndicator);
        Set(serialized, "resumeButton", resume); Set(serialized, "pauseReturnToMenuButton", pauseReturn);
        Set(serialized, "fiveBinsButton", fiveBins); Set(serialized, "selectedMainModeTitle", selectedMainModeTitle); Set(serialized, "exitButton", exitGame);
        Set(serialized, "speedSlider", speedSlider); Set(serialized, "speedValueText", speedValue);
        Set(serialized, "weightSlider", weightSlider); Set(serialized, "weightValueText", weightValue); Set(serialized, "weightIndicator", weightIndicator);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        menu.SetActive(true); submodeMenu.SetActive(false); gameplay.SetActive(false); summary.SetActive(false); pauseOverlay.SetActive(false);
        RepairBrokenImageReferences(scene);
        if (initiallyBrokenPaths.Count > 0)
            Debug.LogWarning("Repaired broken UI Image references on: " + string.Join(", ", initiallyBrokenPaths));
        EditorUtility.SetDirty(trials.gameObject); EditorUtility.SetDirty(canvasObject); EditorSceneManager.MarkSceneDirty(scene);
        List<string> brokenPaths = FindBrokenImageReferencePaths(scene, false);
        if (brokenPaths.Count == 0)
            Debug.Log("Cargo Sorter Day 3 UI setup complete — no broken UI Image references found.");
        else
            Debug.LogError("Cargo Sorter Day 3 UI setup found broken UI Image references on: " + string.Join(", ", brokenPaths));
    }

    private static GameObject FindOrCreateRoot(string name) { GameObject found = GameObject.Find(name); return found != null ? found : new GameObject(name); }
    private static GameObject FindDirect(Transform parent, string name) { Transform child = parent.Find(name); return child != null ? child.gameObject : null; }
    private static GameObject CreateUIObject(string name, Transform parent) { var value = new GameObject(name, typeof(RectTransform)); value.transform.SetParent(parent, false); return value; }
    private static T GetOrAdd<T>(GameObject target) where T : Component { T value = target.GetComponent<T>(); return value != null ? value : target.AddComponent<T>(); }
    private static GameObject Panel(Transform parent, string name, Vector2 min, Vector2 max)
    {
        GameObject value = FindOrCreateUniqueDirect(parent, name); Rect(value, min, max);
        Image image = GetOrAdd<Image>(value); ConfigureRoundedImage(image, PanelColor, 0.35f);
        Shadow shadow = GetOrAdd<Shadow>(value); shadow.effectColor = new Color(0f, 0f, 0f, 0.45f); shadow.effectDistance = new Vector2(0f, -8f);
        return value;
    }
    private static TextMeshProUGUI Title(Transform parent, string name, string content, float size, Vector2 min, Vector2 max)
    {
        TextMeshProUGUI text = Text(parent, name, content, size, min, max); text.fontStyle = FontStyles.Bold; return text;
    }
    private static void Divider(Transform parent, string name, float y)
    {
        GameObject value = FindOrCreateUniqueDirect(parent, name); Rect(value, new Vector2(0.08f, y - 0.002f), new Vector2(0.92f, y + 0.002f));
        Image image = GetOrAdd<Image>(value); ConfigurePlainImage(image, DividerColor); image.raycastTarget = false;
    }
    private static void SettingLabel(Transform parent, string name, string content, float minY, float maxY)
    {
        TextMeshProUGUI text = Text(parent, name, content, 34, new Vector2(0.08f, minY), new Vector2(0.50f, maxY));
        text.alignment = TextAlignmentOptions.MidlineLeft; text.fontStyle = FontStyles.Bold;
    }
    private static TextMeshProUGUI SettingValue(Transform parent, string name, float minY, float maxY)
    {
        TextMeshProUGUI text = Text(parent, name, string.Empty, 30, new Vector2(0.50f, minY), new Vector2(0.92f, maxY));
        text.alignment = TextAlignmentOptions.MidlineRight; text.color = AccentTextColor; return text;
    }
    private static void SliderEndIcon(Transform parent, string name, Sprite sprite, float minX, float maxX, float minY, float maxY)
    {
        GameObject value = FindOrCreateUniqueDirect(parent, name); Rect(value, new Vector2(minX, minY), new Vector2(maxX, maxY));
        Image icon = GetOrAdd<Image>(value); icon.sprite = sprite; icon.color = Color.white; icon.preserveAspect = true; icon.raycastTarget = false;
    }
    private static Sprite LoadIcon(string name)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + name + ".png");
        if (sprite == null) Debug.LogWarning($"Icon '{IconFolder}{name}.png' not found or not imported as a Sprite.");
        return sprite;
    }
    private static void HudBadgeTitle(Transform parent, string name, string content)
    {
        TextMeshProUGUI text = Text(parent, name, content, 28, new Vector2(0.1f, 0.67f), new Vector2(0.9f, 0.95f));
        text.fontStyle = FontStyles.Bold; text.color = HudTextColor;
    }
    private static Image HudIcon(Transform parent, string name, Sprite sprite, float minX, float maxX)
    {
        GameObject value = Container(parent, name, new Vector2(minX, 0.14f), new Vector2(maxX, 0.66f));
        Image icon = GetOrAdd<Image>(value); icon.sprite = sprite; icon.color = HudTextColor; icon.preserveAspect = true; icon.raycastTarget = false;
        return icon;
    }
    // Whole-number slider the therapist or patient can drag with the mouse: thin rounded track,
    // a dot for every level, and a round knob.
    private static Slider SettingSlider(Transform parent, string name, float minY, float maxY, int minValue, int maxValue, int value)
    {
        GameObject root = FindOrCreateUniqueDirect(parent, name); Rect(root, new Vector2(0.17f, minY), new Vector2(0.83f, maxY));
        float inset = SliderHandleSize * 0.5f;

        GameObject background = FindOrCreateUniqueDirect(root.transform, "Background"); CenterStrip(background, inset, SliderTrackHeight);
        Image backgroundImage = GetOrAdd<Image>(background); ConfigureRoundedImage(backgroundImage, SliderTrackColor, 0.5f); backgroundImage.raycastTarget = true;

        GameObject fillArea = FindOrCreateUniqueDirect(root.transform, "Fill Area"); CenterStrip(fillArea, inset, SliderTrackHeight);
        GameObject fill = FindOrCreateUniqueDirect(fillArea.transform, "Fill"); Rect(fill, Vector2.zero, Vector2.one);
        Image fillImage = GetOrAdd<Image>(fill); ConfigureRoundedImage(fillImage, SliderFillColor, 0.5f); fillImage.raycastTarget = false;

        GameObject ticks = FindOrCreateUniqueDirect(root.transform, "Ticks"); CenterStrip(ticks, inset, SliderTickSize);
        ticks.transform.SetSiblingIndex(fillArea.transform.GetSiblingIndex() + 1);
        int tickCount = maxValue - minValue + 1;
        for (int index = 0; index < tickCount; index++)
        {
            GameObject tick = FindOrCreateUniqueDirect(ticks.transform, $"Tick{index + 1}");
            RectTransform tickRect = tick.GetComponent<RectTransform>();
            float x = tickCount > 1 ? (float)index / (tickCount - 1) : 0.5f;
            tickRect.anchorMin = tickRect.anchorMax = new Vector2(x, 0.5f); tickRect.anchoredPosition = Vector2.zero; tickRect.sizeDelta = new Vector2(SliderTickSize, SliderTickSize);
            Image tickImage = GetOrAdd<Image>(tick);
            tickImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); tickImage.type = Image.Type.Simple; tickImage.color = SliderTickColor; tickImage.raycastTarget = false;
        }

        GameObject handleArea = FindOrCreateUniqueDirect(root.transform, "Handle Slide Area"); CenterStrip(handleArea, inset, SliderHandleSize);
        GameObject handle = FindOrCreateUniqueDirect(handleArea.transform, "Handle");
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        // The Slider stretches the handle to the slide area's height, so width = height = SliderHandleSize keeps it round.
        handleRect.anchorMin = new Vector2(0f, 0f); handleRect.anchorMax = new Vector2(0f, 1f); handleRect.sizeDelta = new Vector2(SliderHandleSize, 0f);
        Image handleImage = GetOrAdd<Image>(handle);
        handleImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); handleImage.type = Image.Type.Simple; handleImage.color = Color.white; handleImage.preserveAspect = true;
        Shadow handleShadow = GetOrAdd<Shadow>(handle); handleShadow.effectColor = new Color(0f, 0f, 0f, 0.4f); handleShadow.effectDistance = new Vector2(0f, -3f);

        Slider slider = GetOrAdd<Slider>(root);
        ColorBlock colors = slider.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(0.85f, 0.95f, 1f, 1f); colors.pressedColor = new Color(0.7f, 0.88f, 1f, 1f); colors.selectedColor = Color.white;
        slider.colors = colors;
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.wholeNumbers = true;
        slider.minValue = minValue; slider.maxValue = maxValue; slider.value = value;
        // Arrow keys / A-D also steer the trolley, so the slider must not react to keyboard navigation.
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        return slider;
    }
    // Full width minus a horizontal inset, fixed pixel height, vertically centred.
    private static void CenterStrip(GameObject target, float horizontalInset, float height)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f); rect.anchorMax = new Vector2(1f, 0.5f);
        rect.offsetMin = new Vector2(horizontalInset, -height * 0.5f); rect.offsetMax = new Vector2(-horizontalInset, height * 0.5f);
    }
    // Poppins (SIL Open Font License, see Assets/Art/Fonts/Poppins/OFL.txt): Medium for text, SemiBold used for bold.
    private static TMP_FontAsset LoadOrCreateUIFont()
    {
        TMP_FontAsset regular = LoadOrCreateFontAsset("Poppins-Medium");
        TMP_FontAsset bold = LoadOrCreateFontAsset("Poppins-SemiBold");
        if (regular == null) return TMP_Settings.defaultFontAsset;
        if (bold != null)
        {
            regular.fontWeightTable[7].regularTypeface = bold;   // index 7 = weight 700 (Bold)
            EditorUtility.SetDirty(regular);
        }
        return regular;
    }
    private static TMP_FontAsset LoadOrCreateFontAsset(string fontName)
    {
        string assetPath = FontFolder + fontName + " SDF.asset";
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + fontName + ".ttf");
        if (sourceFont == null)
        {
            Debug.LogWarning($"Font '{FontFolder}{fontName}.ttf' not found; using the default TextMesh Pro font.");
            return null;
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
        fontAsset.name = fontName + " SDF";
        AssetDatabase.CreateAsset(fontAsset, assetPath);
        fontAsset.atlasTextures[0].name = fontName + " SDF Atlas";
        AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
        fontAsset.material.name = fontName + " SDF Material";
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        // Characters Poppins lacks (e.g. the ● trial dots) fall back to the default font.
        if (TMP_Settings.defaultFontAsset != null)
            fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        return fontAsset;
    }
    private static GameObject Container(Transform parent, string name, Vector2 min, Vector2 max)
    {
        GameObject value = FindOrCreateUniqueDirect(parent, name); Rect(value, min, max); return value;
    }
    private static GameObject Badge(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        GameObject value = FindOrCreateUniqueDirect(parent, name);
        RectTransform rect = value.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = GetOrAdd<Image>(value); ConfigureRoundedBadgeImage(image);
        image.raycastTarget = false;
        return value;
    }
    private static void ConfigureRoundedBadgeImage(Image image) { ConfigureRoundedImage(image, HudBadgeColor, 0.4f); }
    // Lower cornerScale = rounder corners (it is the sliced sprite's pixels-per-unit multiplier).
    private static void ConfigureRoundedImage(Image image, Color color, float cornerScale)
    {
        Sprite roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (roundedSprite == null)
        {
            ConfigurePlainImage(image, color);
            return;
        }

        image.sprite = roundedSprite;
        image.overrideSprite = null;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = cornerScale;
        image.preserveAspect = false;
        image.color = color;
    }
    private static TextMeshProUGUI Text(Transform parent, string name, string content, float size, Vector2 min, Vector2 max)
    {
        GameObject value = FindOrCreateUniqueDirect(parent, name); Rect(value, min, max);
        TextMeshProUGUI text = GetOrAdd<TextMeshProUGUI>(value); text.font = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset; text.text = content; text.fontSize = size;
        text.color = Color.white; text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis; text.raycastTarget = false; return text;
    }
    private static Button Button(Transform parent, string name, string label, Vector2 min, Vector2 max, Color? color = null)
    {
        GameObject value = FindOrCreateUniqueDirect(parent, name); Rect(value, min, max);
        Image image = GetOrAdd<Image>(value); ConfigureRoundedImage(image, color ?? ButtonColor, 0.6f);
        Button button = GetOrAdd<Button>(value); button.targetGraphic = image; button.transition = Selectable.Transition.ColorTint; button.spriteState = new SpriteState();
        // Slightly dimmed at rest so hover can brighten it; selected = normal so a clicked button doesn't stay lit.
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.68f, 0.68f, 0.68f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.5f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        Text(value.transform, "Label", label, 32, Vector2.zero, Vector2.one).fontStyle = FontStyles.Bold; return button;
    }
    private static GameObject FixedUIObject(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject value = FindOrCreateUniqueDirect(parent, name);
        RectTransform rect = value.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, 1f); rect.anchoredPosition = position; rect.sizeDelta = size;
        return value;
    }
    private static Button IconButton(Transform parent, string name, Sprite sprite, Vector2 position)
    {
        GameObject value = FixedUIObject(parent, name, new Vector2(0.5f, 1f), position, new Vector2(64f, 64f));
        Image background = GetOrAdd<Image>(value); ConfigureRoundedImage(background, CompactButtonColor, 0.5f);
        Button button = GetOrAdd<Button>(value); button.targetGraphic = background; button.transition = Selectable.Transition.ColorTint; button.spriteState = new SpriteState();
        GameObject iconObject = FindOrCreateUniqueDirect(value.transform, "Icon"); Rect(iconObject, new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f));
        Image icon = GetOrAdd<Image>(iconObject); icon.sprite = sprite; icon.color = Color.white; icon.preserveAspect = true; icon.raycastTarget = false;
        return button;
    }
    private static void Rect(GameObject target, Vector2 min, Vector2 max) { RectTransform rect = target.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static void RemoveDirect(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) Undo.DestroyObjectImmediate(child.gameObject);
    }
    private static void RemoveComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null) Undo.DestroyObjectImmediate(component);
    }
    private static GameObject FindOrCreateUniqueDirect(Transform parent, string name)
    {
        GameObject found = null;
        for (int index = parent.childCount - 1; index >= 0; index--)
        {
            GameObject child = parent.GetChild(index).gameObject;
            if (child.name != name) continue;
            if (found == null) found = child;
            else Undo.DestroyObjectImmediate(child);
        }
        return found ?? CreateUIObject(name, parent);
    }
    private static void Set(SerializedObject value, string property, Object reference) { value.FindProperty(property).objectReferenceValue = reference; }
    private static void ConfigurePlainImage(Image image, Color color)
    {
        SerializedObject serialized = new SerializedObject(image);
        ClearObjectReference(serialized.FindProperty("m_Sprite"));
        ClearObjectReference(serialized.FindProperty("m_OverrideSprite"));
        serialized.FindProperty("m_Type").enumValueIndex = (int)Image.Type.Simple;
        serialized.FindProperty("m_PreserveAspect").boolValue = false;
        serialized.FindProperty("m_Color").colorValue = color;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    [MenuItem("Tools/Cargo Sorter/Diagnose UI Image References")]
    public static void DiagnoseImageReferences()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        List<string> brokenPaths = FindBrokenImageReferencePaths(scene, true);
        Debug.Log($"Cargo Sorter UI Image diagnosis complete: {brokenPaths.Count} Image(s) with broken sprite references found.");
    }

    private static readonly string[] ButtonSpriteProperties =
    {
        "m_SpriteState.m_HighlightedSprite",
        "m_SpriteState.m_PressedSprite",
        "m_SpriteState.m_SelectedSprite",
        "m_SpriteState.m_DisabledSprite"
    };

    private static void RepairBrokenImageReferences(Scene scene)
    {
        foreach (Image image in GetSceneImages(scene))
        {
            SerializedObject serialized = new SerializedObject(image);
            bool changed = ClearIfMissing(serialized.FindProperty("m_Sprite"));
            changed |= ClearIfMissing(serialized.FindProperty("m_OverrideSprite"));
            if (changed)
            {
                serialized.FindProperty("m_Type").enumValueIndex = (int)Image.Type.Simple;
                serialized.FindProperty("m_PreserveAspect").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(image);
            }

            Button button = image.GetComponent<Button>();
            if (button == null) continue;
            SerializedObject buttonSerialized = new SerializedObject(button);
            bool buttonChanged = false;
            foreach (string propertyPath in ButtonSpriteProperties)
                buttonChanged |= ClearIfMissing(buttonSerialized.FindProperty(propertyPath));
            if (buttonChanged) { buttonSerialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(button); }
        }
    }

    private static List<string> FindBrokenImageReferencePaths(Scene scene, bool logImages)
    {
        var paths = new List<string>();
        foreach (Image image in GetSceneImages(scene))
        {
            SerializedObject serialized = new SerializedObject(image);
            SerializedProperty sprite = serialized.FindProperty("m_Sprite");
            SerializedProperty overrideSprite = serialized.FindProperty("m_OverrideSprite");
            bool broken = IsMissing(sprite) || IsMissing(overrideSprite);
            Button button = image.GetComponent<Button>();
            var report = new StringBuilder()
                .Append("Image: ").Append(GetHierarchyPath(image.transform))
                .Append(" | sprite: ").Append(DescribeReference(sprite))
                .Append(" | overrideSprite: ").Append(DescribeReference(overrideSprite))
                .Append(" | type: ").Append(image.type)
                .Append(" | Button: ").Append(button == null ? "none" : "present");
            if (button != null)
            {
                SerializedObject buttonSerialized = new SerializedObject(button);
                report.Append(" | transition: ").Append(button.transition);
                foreach (string propertyPath in ButtonSpriteProperties)
                {
                    SerializedProperty stateSprite = buttonSerialized.FindProperty(propertyPath);
                    broken |= IsMissing(stateSprite);
                    report.Append(" | ").Append(propertyPath).Append(": ").Append(DescribeReference(stateSprite));
                }
            }
            if (broken) paths.Add(GetHierarchyPath(image.transform));
            if (logImages || broken) Debug.Log(report.ToString(), image);
        }
        return paths;
    }

    private static IEnumerable<Image> GetSceneImages(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
                yield return image;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null) { transform = transform.parent; path = transform.name + "/" + path; }
        return path;
    }

    private static string DescribeReference(SerializedProperty property)
    {
        if (property == null) return "property not found";
        if (IsMissing(property)) return $"MISSING (entity ID {property.objectReferenceEntityIdValue})";
        Object value = property.objectReferenceValue;
        return value == null ? "null" : $"valid ({value.name}, {AssetDatabase.GetAssetPath(value)})";
    }

    private static bool IsMissing(SerializedProperty property)
    {
        return property != null
            && property.objectReferenceValue == null
            && !property.objectReferenceEntityIdValue.Equals(default(EntityId));
    }

    private static bool ClearIfMissing(SerializedProperty property)
    {
        if (!IsMissing(property)) return false;
        ClearObjectReference(property);
        return true;
    }

    private static void ClearObjectReference(SerializedProperty property)
    {
        if (property != null) property.objectReferenceValue = null;
    }

    private static void EnsureEventSystem()
    {
        EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
        EventSystem eventSystem;
        if (eventSystems.Length == 0)
        {
            GameObject system = new GameObject("EventSystem", typeof(EventSystem));
            Undo.RegisterCreatedObjectUndo(system, "Create EventSystem");
            eventSystem = system.GetComponent<EventSystem>();
        }
        else
        {
            eventSystem = eventSystems[0];
            foreach (EventSystem candidate in eventSystems)
                if (candidate.GetComponent<InputSystemUIInputModule>() != null)
                {
                    eventSystem = candidate;
                    break;
                }

            foreach (EventSystem duplicate in eventSystems)
                if (duplicate != eventSystem) Undo.DestroyObjectImmediate(duplicate.gameObject);
        }

        foreach (StandaloneInputModule legacyModule in eventSystem.GetComponents<StandaloneInputModule>())
            Undo.DestroyObjectImmediate(legacyModule);

        InputSystemUIInputModule[] inputModules = eventSystem.GetComponents<InputSystemUIInputModule>();
        if (inputModules.Length == 0)
            Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject);
        else
            for (int index = 1; index < inputModules.Length; index++)
                Undo.DestroyObjectImmediate(inputModules[index]);
    }
}
#endif
