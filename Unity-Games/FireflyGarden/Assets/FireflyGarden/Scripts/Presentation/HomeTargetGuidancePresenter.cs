using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace FireflyGarden.Presentation
{
    public sealed class HomeTargetGuidancePresenter : MonoBehaviour
    {
        [SerializeField] private Transform homeTransform;
        [SerializeField] private Transform targetTransform;
        [Header("Dot Appearance")]
        [SerializeField, Min(0.01f)] private float dotSpacing = 0.25f;
        [SerializeField, Min(0.01f)] private float dotDiameter = 0.13f;
        [SerializeField, Min(0f)] private float homeClearance = 0.75f;
        [SerializeField, Min(0f)] private float targetClearance = 0.75f;
        [SerializeField] private Color dotColor = new Color(1f, 0.48f, 0f, 1f);
        [FormerlySerializedAs("maximumDotCount")]
        [SerializeField, Range(1, 64)] private int maxDotCount = 32;

        private readonly List<SpriteRenderer> dots = new List<SpriteRenderer>();
        private Sprite dotSprite;
        private bool isVisible;
        private bool blockGuidanceEnabled = true;
        private Vector3 lastHome;
        private Vector3 lastTarget;

        public bool IsConfigured => homeTransform != null && targetTransform != null;

        private void Awake()
        {
            if (!IsConfigured) { Debug.LogError("HomeTargetGuidancePresenter requires both flower transforms.", this); enabled = false; return; }
            dotSprite = CreateCircleSprite();
            EnsurePool();
            RefreshDots();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (homeTransform.position != lastHome || targetTransform.position != lastTarget) RefreshDots();
        }

        public void SetVisible(bool visible) { isVisible = visible; ApplyVisibility(); }
        public void SetBlockGuidanceEnabled(bool enabledForBlock) { blockGuidanceEnabled = enabledForBlock; ApplyVisibility(); }

        private void EnsurePool()
        {
            int count = Mathf.Clamp(maxDotCount, 1, 64);
            while (dots.Count < count)
            {
                var child = new GameObject($"GuidanceDot_{dots.Count}");
                child.transform.SetParent(transform, false);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sprite = dotSprite; renderer.color = dotColor; renderer.sortingOrder = -2;
                dots.Add(renderer);
            }
        }

        private void RefreshDots()
        {
            EnsurePool();
            lastHome = homeTransform.position; lastTarget = targetTransform.position;
            Vector3 offset = lastTarget - lastHome;
            float usableLength = offset.magnitude - Mathf.Max(0f, homeClearance) - Mathf.Max(0f, targetClearance);
            int count = usableLength < 0f ? 0 : Mathf.Min(dots.Count, Mathf.FloorToInt(usableLength / Mathf.Max(0.01f, dotSpacing)) + 1);
            Vector3 direction = offset.sqrMagnitude > 0f ? offset.normalized : Vector3.zero;
            float spacing = count > 1 ? usableLength / (count - 1) : 0f;
            for (int i = 0; i < dots.Count; i++)
            {
                bool active = i < count;
                if (active)
                {
                    dots[i].transform.position = lastHome + direction * (homeClearance + spacing * i);
                    dots[i].transform.localScale = Vector3.one * dotDiameter;
                    dots[i].color = dotColor;
                }
                dots[i].enabled = active && isVisible && blockGuidanceEnabled;
            }
        }

        private void ApplyVisibility()
        {
            for (int i = 0; i < dots.Count; i++) dots[i].enabled = i < ActiveDotCount() && isVisible && blockGuidanceEnabled;
        }

        private int ActiveDotCount()
        {
            float usable = (lastTarget - lastHome).magnitude - homeClearance - targetClearance;
            return usable < 0f ? 0 : Mathf.Min(dots.Count, Mathf.FloorToInt(usable / Mathf.Max(0.01f, dotSpacing)) + 1);
        }

        private static Sprite CreateCircleSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "GuidanceCircle" };
            var pixels = new Color32[size * size];
            float centre = (size - 1) * 0.5f; float radiusSquared = centre * centre;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                pixels[y * size + x] = ((x - centre) * (x - centre) + (y - centre) * (y - centre) <= radiusSquared)
                    ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            texture.SetPixels32(pixels); texture.Apply(); texture.wrapMode = TextureWrapMode.Clamp; texture.filterMode = FilterMode.Bilinear;
            // A pixels-per-unit value equal to the texture size makes the sprite exactly
            // one world unit wide. The per-dot uniform scale can therefore represent the
            // requested diameter directly instead of turning each dot into a 3.2-unit disc.
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
