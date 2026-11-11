using FireflyGarden.Configuration;
using UnityEngine;

namespace FireflyGarden.Presentation
{
    /// <summary>
    /// Shows the guidance flower ("match this") for Find / Remember the Match, framed by two
    /// orange arrows that point inwards at it (">" left of the flower, "<" right of it) and bob towards it.
    /// Presentation only.
    /// </summary>
    public sealed class CueCardPresenter : MonoBehaviour
    {
        // Arrow shape from the design SVG (80 x 80 viewBox): chevron 32,18 -> 52,40 -> 32,62, stroke 8, round caps.
        private const float ViewBox = 80f;
        private const float StrokeWidth = 8f;
        private static readonly Vector2[] ChevronPoints = { new Vector2(32f, 18f), new Vector2(52f, 40f), new Vector2(32f, 62f) };

        [SerializeField] private GameObject cardVisualRoot;
        [SerializeField] private SpriteRenderer flowerRenderer;
        [SerializeField] private StimulusDefinition currentStimulus;

        [Header("Guidance Arrows")]
        [SerializeField] private bool showGuidanceArrows = true;
        [SerializeField] private Color arrowColor = new Color(0.96f, 0.55f, 0.12f, 1f);
        [Tooltip("Arrow size (its 80 x 80 design box) in world units.")]
        [SerializeField, Min(0.1f)] private float arrowSize = 1.1f;
        [Tooltip("Space between the flower's edge and each arrow box, in world units.")]
        [SerializeField, Min(0f)] private float arrowGap = 0.05f;
        [Tooltip("Bob distance towards the flower in design units (the SVG uses 4 of 80).")]
        [SerializeField, Min(0f)] private float bobDesignUnits = 4f;
        [SerializeField, Min(0.1f)] private float bobPeriodSeconds = 1.4f;

        private Transform leftArrow;
        private Transform rightArrow;
        private Vector3 leftRestWorld;
        private Vector3 rightRestWorld;
        private bool arrowsVisible;
        private float bobTime;

        public StimulusDefinition CurrentStimulus => currentStimulus;

        private void Awake()
        {
            if (showGuidanceArrows) BuildArrows();
            SetArrowsVisible(false);
        }

        private void Update()
        {
            if (!arrowsVisible) return;
            bobTime += Time.deltaTime;
            // 0 -> out -> 0 with ease-in-out, like the SVG's spline animation.
            float phase = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * bobTime / bobPeriodSeconds);
            float offset = phase * bobDesignUnits / ViewBox * arrowSize;
            // Each arrow nudges towards the flower, in the direction it points.
            leftArrow.position = leftRestWorld + Vector3.right * offset;
            rightArrow.position = rightRestWorld + Vector3.left * offset;
        }

        public void Show(StimulusDefinition stimulus)
        {
            currentStimulus = stimulus;
            flowerRenderer.sprite = stimulus != null ? stimulus.FlowerSprite : null;
            flowerRenderer.enabled = stimulus != null;
            cardVisualRoot.SetActive(stimulus != null);
            if (stimulus != null) PlaceArrows();
            SetArrowsVisible(stimulus != null);
        }

        public void Hide()
        {
            cardVisualRoot.SetActive(false);
            flowerRenderer.enabled = false;
            SetArrowsVisible(false);
        }

        public void Clear()
        {
            currentStimulus = null;
            flowerRenderer.sprite = null;
            Hide();
        }

        private void PlaceArrows()
        {
            if (leftArrow == null || flowerRenderer.sprite == null) return;
            Bounds flower = flowerRenderer.bounds;
            float distance = flower.extents.x + arrowGap + arrowSize * 0.5f;
            leftRestWorld = new Vector3(flower.center.x - distance, flower.center.y, transform.position.z);
            rightRestWorld = new Vector3(flower.center.x + distance, flower.center.y, transform.position.z);
            leftArrow.position = leftRestWorld;
            rightArrow.position = rightRestWorld;
            bobTime = 0f;
        }

        private void SetArrowsVisible(bool visible)
        {
            arrowsVisible = visible && leftArrow != null;
            if (leftArrow != null) leftArrow.gameObject.SetActive(arrowsVisible);
            if (rightArrow != null) rightArrow.gameObject.SetActive(arrowsVisible);
        }

        private void BuildArrows()
        {
            Sprite rightSprite = CreateChevronSprite(false);
            Sprite leftSprite = CreateChevronSprite(true);
            // Both arrows point inwards at the flower: ">" on its left, "<" on its right.
            leftArrow = CreateArrow("GuidanceArrowLeft", rightSprite);
            rightArrow = CreateArrow("GuidanceArrowRight", leftSprite);
        }

        private Transform CreateArrow(string objectName, Sprite sprite)
        {
            var arrow = new GameObject(objectName, typeof(SpriteRenderer));
            arrow.transform.SetParent(transform, false);
            // The sprite is one world unit across; undo the parent's scale so the arrow is arrowSize wide.
            Vector3 parentScale = transform.lossyScale;
            arrow.transform.localScale = new Vector3(arrowSize / Mathf.Max(parentScale.x, 0.0001f),
                arrowSize / Mathf.Max(parentScale.y, 0.0001f), 1f);

            SpriteRenderer renderer = arrow.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = arrowColor;
            renderer.sortingLayerID = flowerRenderer.sortingLayerID;
            renderer.sortingOrder = flowerRenderer.sortingOrder;
            return arrow.transform;
        }

        /// <summary>Rasterises the design chevron (white, anti-aliased) into a 1-world-unit sprite.</summary>
        private static Sprite CreateChevronSprite(bool pointLeft)
        {
            const int resolution = 128;
            float scale = resolution / ViewBox;
            float halfStroke = StrokeWidth * 0.5f * scale;
            var points = new Vector2[ChevronPoints.Length];
            for (int i = 0; i < points.Length; i++)
            {
                // SVG y grows downwards and the left arrow is the mirror image (80 - x).
                float x = pointLeft ? ViewBox - ChevronPoints[i].x : ChevronPoints[i].x;
                points[i] = new Vector2(x * scale, (ViewBox - ChevronPoints[i].y) * scale);
            }

            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float distance = float.MaxValue;
                    for (int i = 0; i < points.Length - 1; i++)
                        distance = Mathf.Min(distance, DistanceToSegment(p, points[i], points[i + 1]));
                    float alpha = Mathf.Clamp01(halfStroke - distance + 0.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
