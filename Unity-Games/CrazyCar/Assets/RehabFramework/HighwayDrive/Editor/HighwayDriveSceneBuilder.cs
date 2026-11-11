using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using RehabFramework.HighwayDrive;

namespace RehabFramework.HighwayDrive.EditorTools
{
    // ─────────────────────────────────────────────────────────────────────────
    //  HIGHWAY DRIVE SCENE BUILDER (editor-only)
    //  Programmatically assembles Assets/Scenes/HighwayDrive.unity instead of
    //  hand-authoring scene YAML: every GUID/fileID reference comes from the
    //  live AssetDatabase, and the camera/EventSystem/robot setup matches the
    //  other scenes exactly because it is read from the project rather than
    //  re-typed. Same approach as RallyPaddleSceneBuilder.
    //
    //  Re-run via Tools > Highway Drive > Rebuild Scene. Safe to re-run: it
    //  always starts from a brand-new empty scene and overwrites the file.
    //
    //  ART COMES FROM Art/, WHICH IS GENERATED. The ChatGPT asset pack ships
    //  as ~50 unnamed sheets -- single sprites floating in transparent
    //  padding, paired vertical strips, and multi-prop sheets. Those are
    //  sliced into the individually-named sprites this builder wires up; see
    //  HighwayDriveSheetSlicer for the three slicing modes and why each sheet
    //  kind needs its own. If Art/ is missing, every LoadSprite below returns
    //  null and each component silently falls back to its procedural
    //  placeholder, so the scene still builds and runs.
    // ─────────────────────────────────────────────────────────────────────────
    public static class HighwayDriveSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/HighwayDrive.unity";
        // Was "Crazy Car.unity" until Crazy Car became its own game (2026-10-24,
        // CrazyCarSceneBuilder); renamed so this menu can never overwrite it.
        private const string LegacyScenePath = "Assets/Scenes/HighwayDrive_PreviousTuning.unity";
        private const string FontGuid = "0161d805a3764c089bef00bfe00793f5"; // shared TMP font used by ClearTheView and RallyPaddle
        internal const string ArtDir = "Assets/RehabFramework/HighwayDrive/Art";

        /// <summary>Everything BuildHub creates, handed to a variant's
        /// customise step (CrazyCarSceneBuilder) before the scene is saved.</summary>
        internal sealed class HubParts
        {
            public GameObject Hub;
            public Camera Camera;
            public RobotInterface Robot;
            public Canvas PatientCanvas;
            public HighwayDriveConfig Config;
            public HighwayDriveRoad Road;
            public HighwayDriveTraffic Traffic;
            public HighwayDriveHearts Hearts;
            public HighwayDriveInput Input;
            public HighwayDriveDataLogger Logger;
            public HighwayDriveInstructionUI InstructionUI;
            public HighwayDriveController Controller;
            public QuickPlayBootstrapHD QuickPlay;
        }

        [MenuItem("Tools/Highway Drive/Rebuild Scene")]
        public static void Build() => BuildScene(ScenePath, legacy: false);

        /// <summary>Writes Assets/Scenes/HighwayDrive_PreviousTuning.unity --
        /// the 2026-08-24 tuning, kept alive beside the current one so the two
        /// can be judged side by side rather than from memory.
        ///
        /// A RECONSTRUCTION, NOT A RESTORED COPY. HighwayDrive has never been
        /// committed to git, so the previous files no longer exist anywhere to
        /// be recovered; this rebuilds that behaviour from the same code with
        /// the old values and the three HighwayDriveConfig.Legacy* switches.
        /// One codebase, two scenes -- a forked copy would drift the moment
        /// either was touched, and a comparison between two things that have
        /// silently diverged is worth nothing.</summary>
        [MenuItem("Tools/Highway Drive/Rebuild Previous-Tuning Scene")]
        public static void BuildLegacy() => BuildScene(LegacyScenePath, legacy: true);

        [MenuItem("Tools/Highway Drive/Rebuild Both Scenes")]
        public static void BuildBoth()
        {
            Build();
            BuildLegacy();
        }

        /// <param name="customize">Optional variant step, run on the finished
        /// hub before the scene is saved (Crazy Car adds its menus here).</param>
        internal static void BuildScene(string path, bool legacy, System.Action<HubParts> customize = null)
        {
            ConfigureArtImportSettings();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera camera = BuildCamera();
            RobotInterface robot = BuildRobot();
            BuildEventSystem();
            Canvas patientCanvas = BuildPatientCanvas();
            HubParts parts = BuildHub(robot, camera, patientCanvas, legacy);
            customize?.Invoke(parts);

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Assets/Scenes");
            EditorSceneManager.SaveScene(scene, path);

            // Reopen + resave once: a scene saved immediately after its objects
            // are created in the same pass can leave the internal SceneRoots
            // cache stale. Reloading from disk and saving again forces Unity to
            // rebuild it from the actual Transform hierarchy. Same fix, same
            // reason, as RallyPaddleSceneBuilder.
            Scene reloaded = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            EditorSceneManager.MarkSceneDirty(reloaded);
            EditorSceneManager.SaveScene(reloaded, path);

            RegisterInBuildSettings(path);
            Debug.Log($"[HighwayDriveSceneBuilder] Wrote {path}" + (legacy ? "  (previous tuning)" : ""));
        }

        // ── Art import ──────────────────────────────────────────────────

        /// <summary>Forces Single sprite mode on every generated sprite rather
        /// than trusting whatever Unity auto-picked on first import (these land
        /// as Multiple with an auto-detected sub-sprite, which
        /// LoadAssetAtPath&lt;Sprite&gt; cannot resolve reliably). The exact
        /// pixelsPerUnit does not matter downstream: every consumer sizes
        /// sprites through HighwayDriveSpriteFactory.FitToWorldSize, which
        /// reads the sprite's own rect and PPU at runtime.</summary>
        private static void ConfigureArtImportSettings()
        {
            if (!Directory.Exists(ArtDir))
            {
                Debug.LogWarning($"[HighwayDriveSceneBuilder] No {ArtDir} -- every component will fall back " +
                                  "to its procedural placeholder art.");
                return;
            }

            foreach (string path in Directory.GetFiles(ArtDir, "*.png"))
            {
                string assetPath = path.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;

                bool dirty = importer.textureType != TextureImporterType.Sprite
                          || importer.spriteImportMode != SpriteImportMode.Single
                          || !importer.alphaIsTransparency
                          || importer.mipmapEnabled;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);

                if (dirty) importer.SaveAndReimport();
            }
        }

        // ── Scaffold GameObjects ────────────────────────────────────────

        private static Camera BuildCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);

            var cam = go.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.4f;
            cam.backgroundColor = new Color(0.10f, 0.14f, 0.10f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            return cam;
        }

        private static RobotInterface BuildRobot()
        {
            var go = new GameObject("Robot");
            return go.AddComponent<MKBRobotInterface>();
        }

        private static void BuildEventSystem()
        {
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        private static Canvas BuildPatientCanvas()
        {
            var go = new GameObject("PatientCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private static HubParts BuildHub(RobotInterface robot, Camera camera, Canvas patientCanvas, bool legacy)
        {
            var go = new GameObject(legacy ? "CrazyCar" : "HighwayDrive");

            var config = go.AddComponent<HighwayDriveConfig>();
            if (legacy) ApplyPreviousTuning(config);

            var road = go.AddComponent<HighwayDriveRoad>();
            SetPrivateField(road, "_config", config);
            SetPrivateField(road, "_camera", camera);
            WireEnvironment(road, "green");
            WireEnvironment(road, "desert");
            WireEnvironment(road, "snow");

            var traffic = go.AddComponent<HighwayDriveTraffic>();
            SetPrivateField(traffic, "_config", config);
            SetPrivateField(traffic, "_road", road);
            if (legacy)
            {
                // The 2026-08-24 pool: six cars, no trucks, no motorcycles.
                SetPrivateField(traffic, "_carSprites", LoadSprites(
                    "car_red", "car_yellow", "car_green", "car_orange", "car_purple", "car_van_white"));
            }
            else
            {
                // Prefix-loaded rather than listed by name: the slicer decides
                // how many vehicles each pool ends up with, and a hard-coded
                // list would silently drop any added later.
                SetPrivateField(traffic, "_carSprites", LoadSpritesByPrefix("veh_car_"));
                SetPrivateField(traffic, "_truckSprites", LoadSpritesByPrefix("veh_truck_"));
                SetPrivateField(traffic, "_motorcycleSprites", LoadSpritesByPrefix("veh_moto_"));
            }

            var hearts = go.AddComponent<HighwayDriveHearts>();
            SetPrivateField(hearts, "_config", config);
            SetPrivateField(hearts, "_road", road);
            // Left null for the previous tuning, which had no glowing pickup
            // art and fell back to the procedural heart.
            if (!legacy) SetPrivateField(hearts, "_pickupSprite", LoadSprite("heart_glow"));

            var landmarks = go.AddComponent<HighwayDriveLandmarks>();
            SetPrivateField(landmarks, "_config", config);
            SetPrivateField(landmarks, "_road", road);
            SetPrivateField(landmarks, "_landmarkSprites", LoadSpritesByPrefix("landmark_a_", "landmark_b_"));

            var input = go.AddComponent<HighwayDriveInput>();
            SetPrivateField(input, "_robot", robot);
            SetPrivateField(input, "_config", config);

            var logger = go.AddComponent<HighwayDriveDataLogger>();

            var instructionUI = go.AddComponent<HighwayDriveInstructionUI>();
            SetPrivateField(instructionUI, "_patientCanvas", patientCanvas);
            SetPrivateField(instructionUI, "_config", config);
            SetPrivateField(instructionUI, "_fontAsset", LoadFont());
            SetPrivateField(instructionUI, "_bannerSprite", LoadSprite("hud_banner_keepclear"));
            SetPrivateField(instructionUI, "_timerIconSprite", LoadSprite("hud_timer_icon"));
            SetPrivateField(instructionUI, "_pauseButtonSprite", LoadSprite("hud_pause_button"));
            SetPrivateField(instructionUI, "_gaugeTrackSprite", LoadSprite("hud_gauge_track"));
            SetPrivateField(instructionUI, "_gaugeKnobSprite", LoadSprite("hud_gauge_knob"));
            SetPrivateField(instructionUI, "_speedTextSprite", LoadSprite("hud_speed_text"));
            SetPrivateField(instructionUI, "_rabbitSprite", LoadSprite("hud_rabbit"));
            SetPrivateField(instructionUI, "_turtleSprite", LoadSprite("hud_turtle"));
            SetPrivateField(instructionUI, "_tutorialCrossSprite", LoadSprite("hud_tutorial_cross"));
            SetPrivateField(instructionUI, "_centerTargetSprite", LoadSprite("hud_center_target"));
            if (!legacy)
            {
                SetPrivateField(instructionUI, "_heartFullSprite", LoadSprite("heart_full"));
                SetPrivateField(instructionUI, "_heartEmptySprite", LoadSprite("heart_empty"));
                SetPrivateField(instructionUI, "_heartBrokenSprite", LoadSprite("heart_broken"));
            }
            else
            {
                // No gauge easing in the previous tuning -- the indicator was
                // driven straight from the command.
                SetPrivateField(instructionUI, "_gaugeSmoothingS", 0f);
            }

            var controller = go.AddComponent<HighwayDriveController>();
            SetPrivateField(controller, "_config", config);
            SetPrivateField(controller, "_road", road);
            SetPrivateField(controller, "_traffic", traffic);
            SetPrivateField(controller, "_input", input);
            SetPrivateField(controller, "_instructionUI", instructionUI);
            SetPrivateField(controller, "_hearts", hearts);
            SetPrivateField(controller, "_landmarks", landmarks);
            SetPrivateField(controller, "_logger", logger);
            SetPrivateField(controller, "_playerCarSprite", LoadSprite("car_player_blue"));
            SetPrivateField(controller, "_haloSprite", LoadSprite("fx_halo_cyan"));
            SetPrivateField(controller, "_impactBurstSprite", LoadSprite("fx_impact_burst"));

            var quickPlay = go.AddComponent<QuickPlayBootstrapHD>();
            SetPrivateField(quickPlay, "_controller", controller);

            return new HubParts
            {
                Hub = go, Camera = camera, Robot = robot, PatientCanvas = patientCanvas,
                Config = config, Road = road, Traffic = traffic, Hearts = hearts, Input = input,
                Logger = logger, InstructionUI = instructionUI, Controller = controller, QuickPlay = quickPlay,
            };
        }

        /// <summary>Wires one environment's modular road kit. The verge
        /// scatter deliberately uses only the FLORA and ROCK sprites, not the
        /// signs: the supplied milestone signs have distances baked into the
        /// artwork as numerals ("12 km", "KM 24", "A7 128"), and CLAUDE.md's
        /// no-digits rule covers anything patient-visible, not just text
        /// components. Trees are left out of the dense scatter too -- at
        /// roadside-prop size they read as shrubs; they belong in the sparse
        /// landmark set where they get room.</summary>
        private static void WireEnvironment(HighwayDriveRoad road, string prefix)
        {
            SetPrivateField(road, $"_{prefix}Asphalt", LoadSprite($"{prefix}_asphalt"));
            SetPrivateField(road, $"_{prefix}LaneDash", LoadSprite($"{prefix}_lane_dash"));
            SetPrivateField(road, $"_{prefix}EdgeLeft", LoadSprite($"{prefix}_edge_left"));
            SetPrivateField(road, $"_{prefix}EdgeRight", LoadSprite($"{prefix}_edge_right"));
            SetPrivateField(road, $"_{prefix}Props", LoadSpritesByPrefix($"{prefix}_flora_", $"{prefix}_rock_"));
        }

        /// <summary>Every plain-value difference between the two tunings, in
        /// one place. Only things that are genuinely BEHAVIOURAL live behind
        /// the three Legacy* switches; everything here is an ordinary field
        /// that happened to be tuned differently on 2026-08-24.</summary>
        private static void ApplyPreviousTuning(HighwayDriveConfig config)
        {
            // Behavioural switches -- see HighwayDriveConfig for each.
            config.LegacySceneryCoupling = true;   // scenery rides the road speed
            config.LegacyHeartPacing = true;       // road-speed approach, random lane
            config.LegacyTrafficPool = true;       // one pool, one shared size

            // The permanent cyan ring around the player car.
            config.ShowPlayerHighlight = true;
            config.PlayerHighlightDiameterFactor = 1.18f;

            // Smaller, unpulsed pickup.
            config.HeartPickupSize = new Vector2(0.55f, 0.55f);
            config.HeartPulseAmount = 0f;
        }

        // ── Loading helpers ──────────────────────────────────────────────

        internal static Sprite LoadSprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");
            if (sprite == null)
                Debug.LogWarning($"[HighwayDriveSceneBuilder] Sprite {name} not found -- that component " +
                                  "falls back to its procedural placeholder.");
            return sprite;
        }

        internal static Sprite[] LoadSprites(params string[] names)
            => names.Select(LoadSprite).Where(s => s != null).ToArray();

        /// <summary>Every generated sprite whose file name starts with any of
        /// the given prefixes, in sorted order. Prefix-based rather than an
        /// explicit list because the slicer decides how many props a sheet
        /// yields -- hard-coding counts here would silently drop sprites the
        /// moment a sheet sliced differently.</summary>
        internal static Sprite[] LoadSpritesByPrefix(params string[] prefixes)
        {
            if (!Directory.Exists(ArtDir)) return new Sprite[0];

            var found = new List<Sprite>();
            foreach (string path in Directory.GetFiles(ArtDir, "*.png").OrderBy(p => p))
            {
                string file = Path.GetFileNameWithoutExtension(path);
                if (!prefixes.Any(p => file.StartsWith(p))) continue;

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path.Replace('\\', '/'));
                if (sprite != null) found.Add(sprite);
            }
            return found.ToArray();
        }

        private static TMP_FontAsset LoadFont()
        {
            string path = AssetDatabase.GUIDToAssetPath(FontGuid);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null)
                Debug.LogError($"[HighwayDriveSceneBuilder] TMP font not found for GUID {FontGuid} " +
                               "-- patient text will fall back to the TMP default.");
            return font;
        }

        internal static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                Debug.LogError($"[HighwayDriveSceneBuilder] Field {fieldName} not found on {target.GetType()}");
                return;
            }
            field.SetValue(target, value);
        }

        private static void RegisterInBuildSettings(string path)
        {
            if (EditorBuildSettings.scenes.Any(s => s.path == path)) return;

            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.Add(new EditorBuildSettingsScene(path, false));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
