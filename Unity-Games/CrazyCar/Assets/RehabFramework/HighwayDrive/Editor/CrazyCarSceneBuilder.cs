using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using RehabFramework.HighwayDrive;

namespace RehabFramework.HighwayDrive.EditorTools
{
    // ─────────────────────────────────────────────────────────────────────────
    //  CRAZY CAR SCENE BUILDER (editor-only)
    //  Tools > Crazy Car > Build Crazy Car Scene writes Assets/Scenes/Crazy
    //  Car.unity from scratch: the Highway Drive road, traffic, hearts and
    //  logger (HighwayDriveSceneBuilder), switched to lane steering, plus the
    //  TwinCAT link, the session flow and Cargo Sorter-style menus.
    //
    //  The UI look is copied from Cargo Sorter's CargoSorterDay3Setup: Poppins,
    //  dark navy popups at 80 %, rounded corners
    //  from the built-in UISprite, the thin slider with a round 44 px knob.
    //
    //  Safe to re-run -- it always starts from a new empty scene and
    //  overwrites the file, so hand edits to the scene are lost; change the
    //  values here (or in HighwayDriveConfig defaults) instead.
    // ─────────────────────────────────────────────────────────────────────────
    public static class CrazyCarSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Crazy Car.unity";
        private const string IconFolder = "Assets/Art/UI/Icons/";
        private const string FontFolder = "Assets/Art/Fonts/Poppins/";
        private const string PauseIconPath = "Assets/Art/UI/Buttons/White_Pause.png";

        private static readonly Color PanelColor = new Color(0.06f, 0.09f, 0.13f, 0.80f);
        private static readonly Color ButtonColor = new Color(0.14f, 0.48f, 0.63f, 1f);
        private static readonly Color CompactButtonColor = new Color(0.06f, 0.09f, 0.13f, 0.65f);
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

        private static TMP_FontAsset uiFont;

        [MenuItem("Tools/Crazy Car/Build Crazy Car Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            HighwayDriveSceneBuilder.BuildScene(ScenePath, legacy: false, Customize);
            MakeFirstBuildScene(ScenePath);
            Debug.Log("[CrazyCarSceneBuilder] Crazy Car scene built and saved: " + ScenePath);
        }

        private static void Customize(HighwayDriveSceneBuilder.HubParts p)
        {
            uiFont = LoadOrCreateUIFont();
            p.Hub.name = "CrazyCar";

            ConfigureGame(p.Config);

            // Traffic: every car, truck and motorcycle from Art/, except the
            // three used as the player's Taxi / Van / Truck, so the player's
            // own vehicle never also appears as traffic.
            Set(p.Traffic, "_carSprites", WithoutPlayerSprites(HighwayDriveSceneBuilder.LoadSpritesByPrefix("veh_car_")));
            Set(p.Traffic, "_truckSprites", WithoutPlayerSprites(HighwayDriveSceneBuilder.LoadSpritesByPrefix("veh_truck_")));
            Set(p.Traffic, "_motorcycleSprites", HighwayDriveSceneBuilder.LoadSpritesByPrefix("veh_moto_"));

            // In-game HUD from Highway Drive: Poppins, no planar-control
            // tutorial cross (it shows the wrong movements), and no pause
            // picture -- the real pause button is on the menu canvas.
            Set(p.InstructionUI, "_fontAsset", uiFont);
            Set(p.InstructionUI, "_tutorialCrossSprite", null);
            Set(p.InstructionUI, "_pauseButtonSprite", null);
            Set(p.InstructionUI, "_oneSidedSpeedGauge", true);   // no braking: only the top half of the speed bar

            Set(p.Logger, "_fileNamePrefix", "CrazyCar");

            // The R / F dev keys would restart a drive behind the menus.
            Object.DestroyImmediate(p.QuickPlay);

            var twincatObject = new GameObject("TwinCAT");
            var twincat = twincatObject.AddComponent<TwincatConnectorCrazyCar>();
            Set(p.Input, "_twincat", twincat);

            var flow = p.Hub.AddComponent<CrazyCarSessionFlow>();
            Set(flow, "_controller", p.Controller);
            Set(flow, "_config", p.Config);
            Set(flow, "_instructionUI", p.InstructionUI);
            Set(flow, "_logger", p.Logger);
            Set(flow, "_twincat", twincat);
            Set(flow, "_hudCanvas", p.PatientCanvas);
            Set(flow, "_road", p.Road);
            Set(flow, "_vehicles", BuildVehicles());

            Set(p.Controller, "_playerCarSprite", HighwayDriveSceneBuilder.LoadSprite(TaxiSprite));

            BuildAudio(p.Hub, flow, p.Controller, p.Traffic, p.Input);
            BuildMenus(flow);
        }

        private const string TaxiSprite = "veh_car_yellow";
        private const string VanSprite = "veh_car_whitevan";
        private const string TruckSprite = "veh_truck_semi";
        private const string AudioFolder = "Assets/Audio/CrazyCar/";

        private static Sprite[] WithoutPlayerSprites(Sprite[] sprites)
            => sprites.Where(s => s.name != TaxiSprite && s.name != VanSprite && s.name != TruckSprite).ToArray();

        // ── Audio ────────────────────────────────────────────────────────

        private static void BuildAudio(GameObject hub, CrazyCarSessionFlow flow, HighwayDriveController controller,
            HighwayDriveTraffic traffic, HighwayDriveInput input)
        {
            ConfigureAudioImport("Music_Background.mp3", streaming: true);
            string[] shortClips =
            {
                "Horn_1.wav", "Horn_2.wav", "Horn_3.wav", "Horn_3b.mp3", "Horn_4.wav", "Horn_5.wav", "Horn_6.wav",
                "Crash_1.wav", "Crash_2.wav", "Gas_Up_2.wav", "Gas_Up_3.wav", "Gas_Up_4.wav", "Heart_Coin.wav", "Lost.mp3",
            };
            foreach (string clip in shortClips) ConfigureAudioImport(clip, streaming: false);

            var audio = hub.AddComponent<CrazyCarAudio>();
            Set(audio, "_flow", flow);
            Set(audio, "_controller", controller);
            Set(audio, "_traffic", traffic);
            Set(audio, "_input", input);
            Set(audio, "_music", LoadClip("Music_Background.mp3"));
            Set(audio, "_horns", LoadClips("Horn_1.wav", "Horn_2.wav", "Horn_3.wav", "Horn_3b.mp3", "Horn_4.wav", "Horn_5.wav", "Horn_6.wav"));
            Set(audio, "_crashes", LoadClips("Crash_1.wav", "Crash_2.wav"));
            Set(audio, "_gasUps", LoadClips("Gas_Up_2.wav", "Gas_Up_3.wav", "Gas_Up_4.wav"));
            Set(audio, "_heart", LoadClip("Heart_Coin.wav"));
            Set(audio, "_lost", LoadClip("Lost.mp3"));
        }

        /// <summary>The long music track streams from disk; short effects are
        /// decompressed on load so they start instantly.</summary>
        private static void ConfigureAudioImport(string file, bool streaming)
        {
            var importer = AssetImporter.GetAtPath(AudioFolder + file) as AudioImporter;
            if (importer == null) return;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            AudioClipLoadType loadType = streaming ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            if (settings.loadType == loadType) return;
            settings.loadType = loadType;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        private static AudioClip LoadClip(string file)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + file);
            if (clip == null) Debug.LogWarning($"[CrazyCarSceneBuilder] Audio '{AudioFolder}{file}' not found.");
            return clip;
        }

        private static AudioClip[] LoadClips(params string[] files)
            => files.Select(LoadClip).Where(c => c != null).ToArray();

        private static void ConfigureGame(HighwayDriveConfig config)
        {
            config.LaneSteering = true;
            config.InputSource = HighwayDriveInputSource.TwinCAT;   // keyboard takes over while the PLC is off
            config.LaneCount = 3;
            config.LaneMode = LaneSteeringMode.WristPosition;       // small wrist turn = next lane, always lane-centred
            config.WristFullScaleDeg = 12f;
            config.LaneHysteresis = 0.15f;
            config.LaneChangeThresholdDeg = 15f;
            config.LaneRearmDeg = 5f;

            // Scenery fixed to the ground: bushes, rocks and landmarks move
            // exactly with the road instead of sliding at their own speed.
            config.RoadsideParallaxFactor = 1f;
            config.RoadsideMaxSpeedWorldPerS = 1000f;
            config.LandmarkParallaxFactor = 1f;
            config.LandmarkMaxSpeedWorldPerS = 1000f;

            // Green -> Desert -> Snow, one step per minute of driving.
            config.ThemeMode = HighwayDriveEnvironmentThemeMode.Fixed;
            config.FixedTheme = HighwayDriveEnvironmentTheme.Green;
            config.ThemeChangeSeconds = 60f;

            config.EnableResistance = false;        // force comes from the PLC, chosen by nGameStatus
            config.EndWhenHeartsRunOut = true;
            config.SkipRecentering = true;          // a crash costs a heart, then driving continues
            config.MotivationalPraise = true;       // praise grows with the drive (three groups, one per minute)
            config.PraiseGroupSeconds = 60f;
            config.NiceControlVisibleSeconds = 1.8f; // longer sentences need a little more reading time
            config.TrialDurationSeconds = 0f;       // open-ended: the game lasts until the hearts run out
            config.MaxHearts = 5;

            config.SpeedRampIntervalSeconds = 30f;
            config.SpeedRampStepWorldPerS = 1f;
            config.SpeedRampMaxSteps = 8;

            config.PlayerCarSize = SizeFromWidth(HighwayDriveSceneBuilder.LoadSprite(TaxiSprite), 0.92f);
        }

        // Widths grow with the vehicle; lengths follow each sprite's own aspect
        // so nothing is stretched (and the collision box matches the picture).
        private static CrazyCarVehicleSetup[] BuildVehicles()
        {
            Sprite taxi = HighwayDriveSceneBuilder.LoadSprite(TaxiSprite);
            Sprite van = HighwayDriveSceneBuilder.LoadSprite(VanSprite);
            Sprite truck = HighwayDriveSceneBuilder.LoadSprite(TruckSprite);
            return new[]
            {
                new CrazyCarVehicleSetup
                {
                    Vehicle = CrazyCarVehicle.Taxi, DisplayName = "Taxi", Description = "Light steering",
                    Sprite = taxi, Size = SizeFromWidth(taxi, 0.92f), FallbackColor = new Color(0.98f, 0.80f, 0.15f, 1f),
                },
                new CrazyCarVehicleSetup
                {
                    Vehicle = CrazyCarVehicle.Van, DisplayName = "Van", Description = "Medium steering",
                    Sprite = van, Size = SizeFromWidth(van, 1.0f), FallbackColor = new Color(0.92f, 0.93f, 0.95f, 1f),
                },
                new CrazyCarVehicleSetup
                {
                    Vehicle = CrazyCarVehicle.Truck, DisplayName = "Truck", Description = "Heavy steering",
                    Sprite = truck, Size = SizeFromWidth(truck, 1.12f), FallbackColor = new Color(0.22f, 0.40f, 0.78f, 1f),
                },
            };
        }

        private static Vector2 SizeFromWidth(Sprite sprite, float width)
        {
            if (sprite == null || sprite.rect.width <= 0f) return new Vector2(width, width * 1.65f);
            return new Vector2(width, width * sprite.rect.height / sprite.rect.width);
        }

        // ── Menus ────────────────────────────────────────────────────────

        private static void BuildMenus(CrazyCarSessionFlow flow)
        {
            var canvasObject = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;   // above the Highway Drive PatientCanvas
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasObject.transform;

            // Light icons on the dark popups.
            Sprite turtleLight = LoadIcon("Turtle_Light"), hareLight = LoadIcon("Hare_Light");
            Sprite pauseIcon = AssetDatabase.LoadAssetAtPath<Sprite>(PauseIconPath);
            if (pauseIcon == null) Debug.LogWarning($"[CrazyCarSceneBuilder] {PauseIconPath} not found or not a Sprite.");

            // Main menu: choose the car.
            GameObject menu = Panel(root, "MainMenuPanel", new Vector2(0.30f, 0.08f), new Vector2(0.70f, 0.92f));
            Title(menu.transform, "Title", "Crazy Car", 64, new Vector2(0.06f, 0.80f), new Vector2(0.94f, 0.94f));
            Text(menu.transform, "Subtitle", "Choose your car", 30, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.80f)).color = MutedTextColor;
            Divider(menu.transform, "Divider", 0.705f);
            Button taxi = Button(menu.transform, "TaxiButton", "Taxi", new Vector2(0.12f, 0.52f), new Vector2(0.88f, 0.65f));
            Button van = Button(menu.transform, "VanButton", "Van", new Vector2(0.12f, 0.36f), new Vector2(0.88f, 0.49f));
            Button truck = Button(menu.transform, "TruckButton", "Truck", new Vector2(0.12f, 0.20f), new Vector2(0.88f, 0.33f));
            Button exit = Button(menu.transform, "ExitButton", "Exit Game", new Vector2(0.28f, 0.05f), new Vector2(0.72f, 0.14f), ExitButtonColor);

            // Settings: speed, then Start / Back.
            GameObject settings = Panel(root, "SettingsPanel", new Vector2(0.27f, 0.10f), new Vector2(0.73f, 0.90f));
            TextMeshProUGUI settingsTitle = Title(settings.transform, "SettingsTitle", "Taxi", 58, new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.96f));
            TextMeshProUGUI settingsSubtitle = Text(settings.transform, "SettingsSubtitle", "Light steering", 28, new Vector2(0.08f, 0.77f), new Vector2(0.92f, 0.84f));
            settingsSubtitle.color = MutedTextColor;
            Divider(settings.transform, "Divider", 0.755f);

            SettingLabel(settings.transform, "SpeedLabel", "Speed", 0.60f, 0.69f);
            TextMeshProUGUI speedValue = SettingValue(settings.transform, "SpeedValueText", 0.60f, 0.69f);
            Slider speedSlider = SettingSlider(settings.transform, "SpeedSlider", 0.50f, 0.59f,
                CrazyCarSessionFlow.MinSpeedLevel, CrazyCarSessionFlow.MaxSpeedLevel, CrazyCarSessionFlow.NormalSpeedLevel);
            SliderEndIcon(settings.transform, "SpeedSlowIcon", turtleLight, 0.05f, 0.15f, 0.505f, 0.585f);
            SliderEndIcon(settings.transform, "SpeedFastIcon", hareLight, 0.85f, 0.95f, 0.505f, 0.585f);

            Text(settings.transform, "HintText", "Twist your wrist to change lanes", 26,
                new Vector2(0.06f, 0.36f), new Vector2(0.94f, 0.43f)).color = MutedTextColor;
            Button start = Button(settings.transform, "StartButton", "Start Driving", new Vector2(0.18f, 0.20f), new Vector2(0.82f, 0.33f));
            Button back = Button(settings.transform, "BackButton", "Back", new Vector2(0.30f, 0.05f), new Vector2(0.70f, 0.15f), SecondaryButtonColor);

            // In-game HUD: only the pause button (top-right). The speed and
            // vehicle badges were removed at the user's request (2026-10-25).
            GameObject hud = Container(root, "HudPanel", Vector2.zero, Vector2.one);

            Button pause = IconButton(hud.transform, "PauseButton", pauseIcon, new Vector2(-72f, -58f));

            // Pause popup.
            GameObject pausePanel = Panel(root, "PausePanel", new Vector2(0.34f, 0.25f), new Vector2(0.66f, 0.75f));
            Title(pausePanel.transform, "Title", "Paused", 52, new Vector2(0.08f, 0.70f), new Vector2(0.92f, 0.90f));
            Button resume = Button(pausePanel.transform, "ResumeButton", "Resume", new Vector2(0.16f, 0.40f), new Vector2(0.84f, 0.58f));
            Button pauseMenu = Button(pausePanel.transform, "ReturnToMenuButton", "Return to Menu", new Vector2(0.16f, 0.16f), new Vector2(0.84f, 0.34f));

            // Result popup.
            GameObject result = Panel(root, "ResultPanel", new Vector2(0.30f, 0.15f), new Vector2(0.70f, 0.85f));
            Title(result.transform, "Title", "Drive Complete", 52, new Vector2(0.08f, 0.80f), new Vector2(0.92f, 0.95f));
            Divider(result.transform, "Divider", 0.785f);
            TextMeshProUGUI resultText = Text(result.transform, "ResultText", string.Empty, 36, new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.76f));
            resultText.textWrappingMode = TextWrappingModes.Normal;
            resultText.overflowMode = TextOverflowModes.Overflow;
            Button again = Button(result.transform, "PlayAgainButton", "Play Again", new Vector2(0.08f, 0.08f), new Vector2(0.47f, 0.24f));
            Button resultMenu = Button(result.transform, "ReturnToMenuButton", "Return to Menu", new Vector2(0.53f, 0.08f), new Vector2(0.92f, 0.24f), SecondaryButtonColor);

            var ui = canvasObject.AddComponent<CrazyCarMenuUI>();
            Set(ui, "_flow", flow);
            Set(ui, "_mainMenuPanel", menu);
            Set(ui, "_settingsPanel", settings);
            Set(ui, "_hudPanel", hud);
            Set(ui, "_pausePanel", pausePanel);
            Set(ui, "_resultPanel", result);
            Set(ui, "_taxiButton", taxi);
            Set(ui, "_vanButton", van);
            Set(ui, "_truckButton", truck);
            Set(ui, "_exitButton", exit);
            Set(ui, "_settingsTitle", settingsTitle);
            Set(ui, "_settingsSubtitle", settingsSubtitle);
            Set(ui, "_speedSlider", speedSlider);
            Set(ui, "_speedValueText", speedValue);
            Set(ui, "_startButton", start);
            Set(ui, "_backButton", back);
            Set(ui, "_pauseButton", pause);
            Set(ui, "_resumeButton", resume);
            Set(ui, "_pauseMenuButton", pauseMenu);
            Set(ui, "_resultText", resultText);
            Set(ui, "_playAgainButton", again);
            Set(ui, "_resultMenuButton", resultMenu);

            menu.SetActive(true);
            settings.SetActive(false);
            hud.SetActive(false);
            pausePanel.SetActive(false);
            result.SetActive(false);
        }

        // ── Build settings ───────────────────────────────────────────────

        /// <summary>Crazy Car is the game a build starts in, so its scene goes
        /// first in the Build Profile list and is enabled.</summary>
        private static void MakeFirstBuildScene(string path)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ── UI helpers (from Cargo Sorter's CargoSorterDay3Setup) ─────────

        private static void Set(object target, string field, object value)
            => HighwayDriveSceneBuilder.SetPrivateField(target, field, value);

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var value = new GameObject(name, typeof(RectTransform));
            value.transform.SetParent(parent, false);
            return value;
        }

        private static void Rect(GameObject target, Vector2 min, Vector2 max)
        {
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static GameObject Container(Transform parent, string name, Vector2 min, Vector2 max)
        {
            GameObject value = CreateUIObject(name, parent);
            Rect(value, min, max);
            return value;
        }

        private static GameObject Panel(Transform parent, string name, Vector2 min, Vector2 max)
        {
            GameObject value = Container(parent, name, min, max);
            ConfigureRoundedImage(value.AddComponent<Image>(), PanelColor, 0.35f);
            Shadow shadow = value.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -8f);
            return value;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, string content, float size, Vector2 min, Vector2 max)
        {
            GameObject value = Container(parent, name, min, max);
            TextMeshProUGUI text = value.AddComponent<TextMeshProUGUI>();
            text.font = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset;
            text.text = content;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static TextMeshProUGUI Title(Transform parent, string name, string content, float size, Vector2 min, Vector2 max)
        {
            TextMeshProUGUI text = Text(parent, name, content, size, min, max);
            text.fontStyle = FontStyles.Bold;
            return text;
        }

        private static void Divider(Transform parent, string name, float y)
        {
            GameObject value = Container(parent, name, new Vector2(0.08f, y - 0.002f), new Vector2(0.92f, y + 0.002f));
            Image image = value.AddComponent<Image>();
            image.color = DividerColor;
            image.raycastTarget = false;
        }

        private static void SettingLabel(Transform parent, string name, string content, float minY, float maxY)
        {
            TextMeshProUGUI text = Text(parent, name, content, 34, new Vector2(0.08f, minY), new Vector2(0.50f, maxY));
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.fontStyle = FontStyles.Bold;
        }

        private static TextMeshProUGUI SettingValue(Transform parent, string name, float minY, float maxY)
        {
            TextMeshProUGUI text = Text(parent, name, string.Empty, 30, new Vector2(0.50f, minY), new Vector2(0.92f, maxY));
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.color = AccentTextColor;
            return text;
        }

        private static void SliderEndIcon(Transform parent, string name, Sprite sprite, float minX, float maxX, float minY, float maxY)
        {
            GameObject value = Container(parent, name, new Vector2(minX, minY), new Vector2(maxX, maxY));
            Image icon = value.AddComponent<Image>();
            icon.sprite = sprite;
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        private static Sprite LoadIcon(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + name + ".png");
            if (sprite == null) Debug.LogWarning($"[CrazyCarSceneBuilder] Icon '{IconFolder}{name}.png' not found or not imported as a Sprite.");
            return sprite;
        }

        // Whole-number slider: thin rounded track, a dot for every level, round knob.
        private static Slider SettingSlider(Transform parent, string name, float minY, float maxY, int minValue, int maxValue, int value)
        {
            GameObject root = Container(parent, name, new Vector2(0.17f, minY), new Vector2(0.83f, maxY));
            float inset = SliderHandleSize * 0.5f;

            GameObject background = CreateUIObject("Background", root.transform);
            CenterStrip(background, inset, SliderTrackHeight);
            Image backgroundImage = background.AddComponent<Image>();
            ConfigureRoundedImage(backgroundImage, SliderTrackColor, 0.5f);

            GameObject fillArea = CreateUIObject("Fill Area", root.transform);
            CenterStrip(fillArea, inset, SliderTrackHeight);
            GameObject fill = Container(fillArea.transform, "Fill", Vector2.zero, Vector2.one);
            Image fillImage = fill.AddComponent<Image>();
            ConfigureRoundedImage(fillImage, SliderFillColor, 0.5f);
            fillImage.raycastTarget = false;

            GameObject ticks = CreateUIObject("Ticks", root.transform);
            CenterStrip(ticks, inset, SliderTickSize);
            int tickCount = maxValue - minValue + 1;
            for (int index = 0; index < tickCount; index++)
            {
                GameObject tick = CreateUIObject($"Tick{index + 1}", ticks.transform);
                RectTransform tickRect = tick.GetComponent<RectTransform>();
                float x = tickCount > 1 ? (float)index / (tickCount - 1) : 0.5f;
                tickRect.anchorMin = tickRect.anchorMax = new Vector2(x, 0.5f);
                tickRect.anchoredPosition = Vector2.zero;
                tickRect.sizeDelta = new Vector2(SliderTickSize, SliderTickSize);
                Image tickImage = tick.AddComponent<Image>();
                tickImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                tickImage.color = SliderTickColor;
                tickImage.raycastTarget = false;
            }

            // The Slider stretches the handle to the slide area's height, so the
            // area is exactly one knob tall and the handle is (knob, 0): round, not oval.
            GameObject handleArea = CreateUIObject("Handle Slide Area", root.transform);
            CenterStrip(handleArea, inset, SliderHandleSize);
            GameObject handle = CreateUIObject("Handle", handleArea.transform);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(0f, 1f);
            handleRect.sizeDelta = new Vector2(SliderHandleSize, 0f);
            Image handleImage = handle.AddComponent<Image>();
            handleImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            handleImage.color = Color.white;
            handleImage.preserveAspect = true;
            Shadow handleShadow = handle.AddComponent<Shadow>();
            handleShadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
            handleShadow.effectDistance = new Vector2(0f, -3f);

            Slider slider = root.AddComponent<Slider>();
            ColorBlock colors = slider.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.85f, 0.95f, 1f, 1f);
            colors.pressedColor = new Color(0.7f, 0.88f, 1f, 1f);
            colors.selectedColor = Color.white;
            slider.colors = colors;
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.wholeNumbers = true;
            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.value = value;
            // The arrow keys also steer the car, so the slider must ignore keyboard navigation.
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            return slider;
        }

        private static void CenterStrip(GameObject target, float horizontalInset, float height)
        {
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(horizontalInset, -height * 0.5f);
            rect.offsetMax = new Vector2(-horizontalInset, height * 0.5f);
        }

        private static Button Button(Transform parent, string name, string label, Vector2 min, Vector2 max, Color? color = null)
        {
            GameObject value = Container(parent, name, min, max);
            Image image = value.AddComponent<Image>();
            ConfigureRoundedImage(image, color ?? ButtonColor, 0.6f);
            Button button = value.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            // Slightly dimmed at rest so hover can brighten it; selected = normal so a clicked button doesn't stay lit.
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.68f, 0.68f, 0.68f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.5f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            Text(value.transform, "Label", label, 32, Vector2.zero, Vector2.one).fontStyle = FontStyles.Bold;
            return button;
        }

        /// <summary>64 px rounded icon button, anchored top-right.</summary>
        private static Button IconButton(Transform parent, string name, Sprite sprite, Vector2 position)
        {
            GameObject value = CreateUIObject(name, parent);
            RectTransform rect = value.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(64f, 64f);
            Image background = value.AddComponent<Image>();
            ConfigureRoundedImage(background, CompactButtonColor, 0.5f);
            Button button = value.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            GameObject iconObject = Container(value.transform, "Icon", new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f));
            Image icon = iconObject.AddComponent<Image>();
            icon.sprite = sprite;
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return button;
        }

        // Lower cornerScale = rounder corners (it is the sliced sprite's pixels-per-unit multiplier).
        private static void ConfigureRoundedImage(Image image, Color color, float cornerScale)
        {
            Sprite roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.color = color;
            if (roundedSprite == null) return;
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = cornerScale;
            image.preserveAspect = false;
        }

        // Poppins (SIL Open Font License, see Assets/Art/Fonts/Poppins/OFL.txt): Medium for text, SemiBold for bold.
        private static TMP_FontAsset LoadOrCreateUIFont()
        {
            TMP_FontAsset regular = LoadOrCreateFontAsset("Poppins-Medium");
            TMP_FontAsset bold = LoadOrCreateFontAsset("Poppins-SemiBold");
            if (regular == null) return TMP_Settings.defaultFontAsset;
            if (bold != null && regular.fontWeightTable[7].regularTypeface != bold)
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
                Debug.LogWarning($"[CrazyCarSceneBuilder] Font '{FontFolder}{fontName}.ttf' not found; using the default TextMesh Pro font.");
                return null;
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
            fontAsset.name = fontName + " SDF";
            AssetDatabase.CreateAsset(fontAsset, assetPath);
            fontAsset.atlasTextures[0].name = fontName + " SDF Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            fontAsset.material.name = fontName + " SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            if (TMP_Settings.defaultFontAsset != null)
                fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            return fontAsset;
        }
    }
}
