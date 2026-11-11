using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FireflyGarden.UI
{
    /// <summary>
    /// Shared pop-up look for Firefly Garden menus (same visual language as the lab's other games):
    /// a dark rounded panel with a soft shadow, rounded coloured buttons, and the Poppins font.
    /// Presentation only; it holds no gameplay or session state.
    /// </summary>
    public static class FireflyUiStyle
    {
        public static readonly Color PanelColor = new Color(0.06f, 0.09f, 0.13f, 0.80f);
        public static readonly Color DimColor = new Color(0f, 0f, 0f, 0.30f);
        public static readonly Color PrimaryButtonColor = new Color(0.14f, 0.48f, 0.63f, 1f);
        public static readonly Color SecondaryButtonColor = new Color(0.30f, 0.36f, 0.42f, 1f);
        public static readonly Color ExitButtonColor = new Color(0.72f, 0.25f, 0.22f, 1f);
        public static readonly Color TitleColor = Color.white;
        public static readonly Color MutedTextColor = new Color(0.74f, 0.80f, 0.85f, 1f);
        public static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.14f);

        public const float PopupWidth = 640f;
        public const float WideButtonWidth = 520f;
        public const float ButtonHeight = 66f;
        public const float ButtonSpacing = 80f;

        private const string FontFolder = "FireflyUI/Fonts/";
        private static TMP_FontAsset regularFont;
        private static TMP_FontAsset boldFont;
        private static bool fontWarningLogged;
        // Runtime textures are unloaded on scene changes; Unity's null check rebuilds the sprite when needed.
        private static Sprite roundedSprite;

        public static Transform CreateCanvas(Transform parent, string objectName, int sortingOrder)
        {
            var canvasObject = new GameObject(objectName, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvasObject.transform;
        }

        public static GameObject CreateDimmer(Transform parent)
        {
            GameObject dimmer = CreateImage("Dimmer", parent, null, DimColor);
            Stretch(dimmer.GetComponent<RectTransform>());
            return dimmer;
        }

        /// <summary>Full-screen background image that keeps its aspect ratio and covers the screen.</summary>
        public static GameObject CreateBackground(Transform parent, Sprite sprite, Color fallbackColor)
        {
            GameObject background = CreateImage("Background", parent, sprite, sprite != null ? Color.white : fallbackColor);
            background.GetComponent<Image>().raycastTarget = false;
            RectTransform rect = background.GetComponent<RectTransform>();
            Stretch(rect);
            if (sprite != null && sprite.rect.height > 0f)
            {
                AspectRatioFitter fitter = background.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            }
            return background;
        }

        public static GameObject CreatePopup(Transform parent, string objectName, float height, float width = PopupWidth)
        {
            GameObject panel = CreateImage(objectName, parent, RoundedSprite, PanelColor);
            Image image = panel.GetComponent<Image>();
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 0.6f;
            panel.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);

            Shadow shadow = panel.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(0f, -8f);
            return panel;
        }

        /// <summary>Text centred horizontally; topOffset.y is measured down from the top of the parent.</summary>
        public static TextMeshProUGUI CreateText(Transform parent, string text, float fontSize, bool bold,
            Color color, Vector2 topOffset, Vector2 size)
        {
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TopAnchor(textObject.GetComponent<RectTransform>(), topOffset, size);

            LoadFonts();
            TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
            TMP_FontAsset font = bold ? boldFont : regularFont;
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = bold && boldFont == null ? FontStyles.Bold : FontStyles.Normal;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        public static Button CreateButton(Transform parent, string labelText, Color color, Vector2 topOffset,
            Vector2 size, UnityAction onClick)
        {
            GameObject buttonObject = CreateImage(labelText + "Button", parent, RoundedSprite, color);
            Image image = buttonObject.GetComponent<Image>();
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 0.9f;
            TopAnchor(buttonObject.GetComponent<RectTransform>(), topOffset, size);

            Shadow shadow = buttonObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
            shadow.effectDistance = new Vector2(0f, -3f);

            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.70f, 0.70f, 0.70f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            if (onClick != null) button.onClick.AddListener(onClick);

            if (!string.IsNullOrEmpty(labelText))
            {
                TextMeshProUGUI label = CreateText(buttonObject.transform, labelText, 30f, true, Color.white, Vector2.zero, size);
                Stretch(label.rectTransform);
            }
            return button;
        }

        public static void CreateDivider(Transform parent, float topY, float width = PopupWidth - 120f)
        {
            GameObject divider = CreateImage("Divider", parent, null, DividerColor);
            TopAnchor(divider.GetComponent<RectTransform>(), new Vector2(0f, topY), new Vector2(width, 2f));
            divider.GetComponent<Image>().raycastTarget = false;
        }

        public static GameObject CreateImage(string objectName, Transform parent, Sprite sprite, Color color)
        {
            var imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return imageObject;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void TopAnchor(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
        }

        private static Sprite RoundedSprite
        {
            get
            {
                if (roundedSprite == null) roundedSprite = CreateRoundedSprite(64, 24);
                return roundedSprite;
            }
        }

        private static void LoadFonts()
        {
            if (regularFont == null) regularFont = Resources.Load<TMP_FontAsset>(FontFolder + "Poppins-Medium SDF");
            if (boldFont == null) boldFont = Resources.Load<TMP_FontAsset>(FontFolder + "Poppins-SemiBold SDF");
            if ((regularFont == null || boldFont == null) && !fontWarningLogged)
            {
                fontWarningLogged = true;
                Debug.LogWarning("Firefly Garden UI: Poppins fonts not found in Resources/" + FontFolder + "; using the TMP default font.");
            }
        }

        /// <summary>White rounded square with soft edges; sliced borders keep the corners round at any size.</summary>
        private static Sprite CreateRoundedSprite(int size, int radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
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
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - distance + 0.5f)));
                }
            }
            texture.Apply();

            float border = Mathf.Min(radius, size / 2 - 1);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }
    }
}
