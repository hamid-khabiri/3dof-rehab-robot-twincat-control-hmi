using System;
using System.Collections.Generic;
using UnityEngine;

namespace RehabFramework.HighwayDrive
{
    // ─────────────────────────────────────────────────────────────────────────
    //  HIGHWAY DRIVE ROAD (HD-v1)
    //  Assembles the bird's-eye scene once in Start() from the MODULAR art set
    //  (Art/<theme>_asphalt, _lane_dash, _edge_left/right, plus the theme prop
    //  sprites) and then scrolls the repeating parts of it.
    //
    //  NO VERGE STRIPS. The art pack ships a textured roadside strip per theme
    //  and an earlier version laid one down each side. The problem is inherent
    //  to the idea rather than to the execution: a fixed-width textured band
    //  pasted onto flat ground has a hard vertical edge where it ends, and that
    //  dead-straight cutoff running the full height of the screen is far more
    //  conspicuous than the bare ground it was meant to dress. Widening it just
    //  moves the line. The scattered ground cover carries the whole off-road
    //  area instead, starting almost at the edge line, which has no boundary to
    //  give away because it never had an edge in the first place. The strip
    //  PNGs remain in Art/ if a future perspective view wants them.
    //
    //  THIS CLASS IS THE FORWARD-MOTION ILLUSION, so it is worth being precise
    //  about what scrolls at what rate:
    //
    //    - The ROAD SURFACE and LANE DASHES scroll at the player's TRUE speed.
    //      They are what reports absolute speed, and what makes easing off the
    //      throttle feel like slowing down rather than like traffic speeding up.
    //    - The SCATTERED GROUND COVER scrolls MUCH SLOWER
    //      (RoadsideParallaxFactor, capped). It used to run slightly FASTER
    //      than the road on the theory that peripheral optic flow sells speed,
    //      and that was wrong in practice: at full throttle the grass and rocks
    //      became a blur nobody could resolve, so the environment art might as
    //      well not have existed. The speed cue belongs to the traffic and the
    //      lane markings; the sides of the road are there to be looked at.
    //    - EDGE LINES are static: a solid unbroken line carries no motion
    //      information, so scrolling it would cost draw calls and buy nothing.
    //    - The OTHER CARS move at the DIFFERENCE between their true speed and
    //      the player's (HighwayDriveTraffic), so they read as slower traffic
    //      being overtaken rather than as objects falling.
    //
    //  ══ TWO WAYS TO SCROLL, AND WHY BOTH EXIST ══
    //
    //  MODULO-WRAP, for the regular grids (asphalt, lane dashes). Each
    //  element sits at baseY minus (accumulated distance modulo P). Nothing is
    //  ever moved to the back of a queue, so there is no state to get wrong at
    //  large distances or large frame times.
    //
    //  This is only valid when the laid-out pattern is GENUINELY PERIODIC with
    //  P -- every element at Y must be identical to the element at Y+P, same
    //  sprite, same x, same size, same flip. Violating that does not merely
    //  look slightly off: at each wrap the entire layer jumps by P and lands on
    //  DIFFERENT content, which reads as the scenery lurching upward and
    //  resetting several times a second. Two ways it was violated here:
    //
    //    1. Mirrored tiles. Asphalt tiles flip every other tile to hide the
    //       seam, which makes the true pattern period 2P, not P. They therefore
    //       wrap on TWICE the tile length -- see _roadWrapPeriod. Wrapping on P
    //       swapped flipped for unflipped at every wrap.
    //    2. Scattered ground cover. Randomised position, sprite and scale per
    //       prop is not periodic with anything, so modulo-wrap can never work
    //       for it at all.
    //
    //  PER-ITEM RECYCLING, for the scattered ground cover. Each prop simply
    //  moves down, and when it leaves the bottom it is re-placed above the top
    //  with fresh randomisation. No wrap, no periodicity requirement, and the
    //  scatter can be as irregular as it likes. This is the same approach
    //  HighwayDriveTraffic, HighwayDriveHearts and HighwayDriveLandmarks all
    //  already use for their pooled objects.
    //
    //  LANES ARE GENERATED, NOT AUTHORED. A LaneCount-lane road is built with
    //  LaneCount-1 dashed dividers at the boundaries between generated lane
    //  centres (HighwayDriveControlMath.LaneCenterX / LaneWidth), so 3, 4 and
    //  5 lanes are one implementation and one code path.
    // ─────────────────────────────────────────────────────────────────────────

    public class HighwayDriveRoad : MonoBehaviour
    {
        /// <summary>Scenery-to-road speed ratio in the pre-2026-08-25 tuning.</summary>
        private const float LegacyRoadsideFactor = 1.15f;

        private const int SortingGround = -22;
        private const int SortingRoadSurface = -20;
        private const int SortingLaneDash = -16;
        private const int SortingEdgeLine = -15;
        private const int SortingRoadsideProp = -14;

        [Header("Dependencies")]
        [SerializeField] private HighwayDriveConfig _config;
        [Tooltip("Used only to size the scene to the visible area. Falls back to Camera.main.")]
        [SerializeField] private Camera _camera;

        [Header("Modular road art -- Green (reference environment)")]
        [SerializeField] private Sprite _greenAsphalt;
        [SerializeField] private Sprite _greenLaneDash;
        [SerializeField] private Sprite _greenEdgeLeft;
        [SerializeField] private Sprite _greenEdgeRight;
        [Tooltip("Ground cover scattered across the off-road band: grass, flowers, small rocks. Sparse " +
                 "distinctive landmarks are a separate system (HighwayDriveLandmarks).")]
        [SerializeField] private Sprite[] _greenProps;

        [Header("Modular road art -- Desert")]
        [SerializeField] private Sprite _desertAsphalt;
        [SerializeField] private Sprite _desertLaneDash;
        [SerializeField] private Sprite _desertEdgeLeft;
        [SerializeField] private Sprite _desertEdgeRight;
        [SerializeField] private Sprite[] _desertProps;

        [Header("Modular road art -- Snow")]
        [SerializeField] private Sprite _snowAsphalt;
        [SerializeField] private Sprite _snowLaneDash;
        [SerializeField] private Sprite _snowEdgeLeft;
        [SerializeField] private Sprite _snowEdgeRight;
        [SerializeField] private Sprite[] _snowProps;

        [Header("Layout")]
        [Tooltip("Along-road length of one asphalt tile, world units.")]
        [SerializeField] private float _asphaltTileLengthWorld = 6f;
        [Tooltip("Along-road length of one dashed-divider segment sprite.")]
        [SerializeField] private float _dashSegmentLengthWorld = 6f;
        [Tooltip("Gap between the painted road edge and where ground cover starts, world units. Small: the " +
                 "scatter is what blends the road into the surroundings, so it should begin almost at the " +
                 "edge line.")]
        [SerializeField] private float _scatterRoadClearance = 0.12f;
        [Tooltip("Ground-cover props per row, per side, scattered across the whole off-road band.")]
        [SerializeField] private int _propsPerRowPerSide = 7;
        [Tooltip("Candidate positions tried before a prop gives up for this frame and waits off-screen. " +
                 "80 was measured, not guessed: at the shipped density it leaves a prop waiting on about " +
                 "10 frames in 3 minutes of driving, so the scatter is never visibly short.")]
        [SerializeField] private int _scatterPlacementAttempts = 80;
        [Tooltip("Breathing gap required between two ground-cover props, world units, so ones that merely " +
                 "touch still read as separate plants.")]
        [SerializeField] private float _scatterSpacingPad = 0.06f;
        [Tooltip("Height of the off-screen band a recycled prop re-enters through, in row spacings. Every " +
                 "prop funnels through this window, so a narrow one becomes a crowding hotspot even when " +
                 "the band as a whole has plenty of room -- widening it costs nothing because the whole " +
                 "window sits above the camera.")]
        [SerializeField] private float _scatterReentryWindowRows = 4f;

        public bool IsBuilt { get; private set; }

        /// <summary>Vertical bounds of the visible area and the off-view margin
        /// band. Single source of truth for where traffic, hearts and landmarks
        /// may appear and where it is safe to remove them.</summary>
        public float ViewTopY { get; private set; }
        public float ViewBottomY { get; private set; }
        public float SpawnY { get; private set; }
        public float DespawnBelowY { get; private set; }
        public float DespawnAboveY { get; private set; }

        /// <summary>Half-width of the visible area, world units.</summary>
        public float ViewHalfWidth { get; private set; }

        public float DistanceTravelled { get; private set; }
        public HighwayDriveEnvironmentTheme ActiveTheme { get; private set; }

        /// <summary>Prop sprites for the active theme.</summary>
        public Sprite[] ActiveProps { get; private set; }

        /// <summary>One modulo-wrapped element. Period is carried per item
        /// rather than inferred, because asphalt and dashes wrap on
        /// different lengths -- an earlier version sniffed the GameObject name
        /// to decide, which was both fragile and easy to get wrong.</summary>
        private struct ScrollItem
        {
            public Transform T;
            public float BaseX;
            public float BaseY;
            public float Period;
        }

        /// <summary>One recycled ground-cover prop. Y is tracked here rather
        /// than read back off the Transform so the recycle test is exact.</summary>
        private class ScatterProp
        {
            public Transform T;
            public SpriteRenderer SR;
            public float Y;
            /// <summary>Current world footprint, kept here so neighbours can be
            /// tested without reading it back off the renderer every time.</summary>
            public float X;
            public Vector2 Size;
            /// <summary>False until first placed. Unplaced props sit at the
            /// origin, which is ON THE ROAD and therefore outside the scatter
            /// band, so they could never actually collide -- but skipping them
            /// explicitly keeps the intent obvious rather than relying on that
            /// coincidence holding.</summary>
            public bool Placed;
        }

        private readonly List<ScrollItem> _roadItems = new List<ScrollItem>();
        private readonly List<ScatterProp> _scatter = new List<ScatterProp>();

        private float _roadPeriod;
        private float _roadWrapPeriod;
        private float _dashPeriod;
        private float _propRowSpacing;

        private float _roadScrollDistance;
        private float _roadsideScrollDistance;

        private System.Random _scatterRng;
        private Sprite[] _scatterSprites;
        private float _scatterInnerX;
        private float _scatterOuterX;
        private float _scatterTopY;
        private float _scatterBaseHeight;

        private Sprite _whiteSprite;

        // Kept so SetTheme can re-skin the road in place (Crazy Car).
        private SpriteRenderer _groundRenderer;
        private readonly List<SpriteRenderer> _asphaltRenderers = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _dashRenderers = new List<SpriteRenderer>();
        private readonly SpriteRenderer[] _edgeRenderers = new SpriteRenderer[2];

        private void Start()
        {
            if (_camera == null) _camera = Camera.main;

            ActiveTheme = ResolveTheme();
            ComputeBounds();

            BuildGround();
            BuildRoadSurface();
            BuildLaneDashes();
            BuildEdgeLines();
            BuildScatter();
            ApplyScroll();

            IsBuilt = true;
        }

        /// <summary>Scroll the world by one frame.
        ///
        /// Takes SPEED AND DELTA rather than a pre-multiplied distance because
        /// the roadside tier is CAPPED, and a cap is a statement about speed --
        /// it cannot be applied to a distance that has already had an unknown
        /// delta folded into it.
        ///
        /// The road surface and lane markings move at the full true speed; the
        /// scattered ground cover moves at RoadsideParallaxFactor of it, capped
        /// at RoadsideMaxSpeedWorldPerS. See HighwayDriveControlMath.ParallaxSpeed
        /// for why the tiers are decoupled at all.</summary>
        public void Advance(float speedWorldPerS, float deltaTime)
        {
            if (!IsBuilt || deltaTime <= 0f || speedWorldPerS <= 0f) return;

            float roadsideSpeed;
            if (_config != null && _config.LegacySceneryCoupling)
            {
                // The 2026-08-24 behaviour: scenery ran slightly FASTER than
                // the road, on the theory that peripheral optic flow sells
                // speed. Kept only so the Crazy Car scene can be compared
                // against the current tuning.
                roadsideSpeed = speedWorldPerS * LegacyRoadsideFactor;
            }
            else
            {
                float factor = _config != null ? _config.RoadsideParallaxFactor : 0.4f;
                float cap = _config != null ? _config.RoadsideMaxSpeedWorldPerS : 5.5f;
                roadsideSpeed = HighwayDriveControlMath.ParallaxSpeed(speedWorldPerS, factor, cap);
            }

            float roadDelta = speedWorldPerS * deltaTime;
            float roadsideDelta = roadsideSpeed * deltaTime;

            _roadScrollDistance += roadDelta;
            _roadsideScrollDistance += roadsideDelta;
            DistanceTravelled += roadDelta;

            ApplyScroll();
            AdvanceScatter(roadsideDelta);
        }

        /// <summary>Start-of-run reset. Zeroes the accumulated DISTANCE but
        /// leaves the scroll phase and the scatter alone -- resetting either
        /// would snap the whole scene in one frame, a visible jolt that says
        /// nothing to the patient.</summary>
        public void ResetScroll()
        {
            DistanceTravelled = 0f;
        }

        /// <summary>Switches the environment while driving (Crazy Car): ground
        /// colour, asphalt, lane dashes and edge lines change in place, and the
        /// roadside scatter is re-dressed from the new theme's props. Tiles
        /// keep their positions and scale-fits, so nothing jumps. Only valid
        /// when the theme's sprites are wired; missing ones keep the old art.</summary>
        public void SetTheme(HighwayDriveEnvironmentTheme theme)
        {
            if (!IsBuilt || theme == ActiveTheme) return;
            ActiveTheme = theme;

            if (_groundRenderer != null) _groundRenderer.color = GroundColorForTheme();

            Sprite asphalt = ThemeAsphalt();
            if (asphalt != null)
                foreach (SpriteRenderer sr in _asphaltRenderers) Reskin(sr, asphalt);

            Sprite dash = ThemeLaneDash();
            if (dash != null)
                foreach (SpriteRenderer sr in _dashRenderers) Reskin(sr, dash);

            for (int side = 0; side < 2; side++)
            {
                Sprite edge = ThemeEdge(side == 0);
                if (edge != null && _edgeRenderers[side] != null) Reskin(_edgeRenderers[side], edge);
            }

            Sprite[] props = ThemeProps();
            if (props != null && props.Length > 0 && _scatter.Count > 0)
            {
                _scatterSprites = props;
                ActiveProps = props;
                RescatterAll();
            }
        }

        /// <summary>Swaps a renderer's sprite while keeping its world size, so
        /// a new theme tile lands exactly where the old one was (FitToWorldSize
        /// depends on the sprite's own pixel size, which differs per theme).</summary>
        private static void Reskin(SpriteRenderer sr, Sprite sprite)
        {
            Vector2 worldSize = sr.bounds.size;
            float flipY = Mathf.Sign(sr.transform.localScale.y);
            sr.sprite = sprite;
            HighwayDriveSpriteFactory.FitToWorldSize(sr, worldSize);
            if (flipY < 0f) FlipVertically(sr.transform);
        }

        /// <summary>Places every ground-cover prop afresh, row by row, exactly
        /// as BuildScatter's first fill does.</summary>
        private void RescatterAll()
        {
            foreach (ScatterProp p in _scatter) { p.Placed = false; p.SR.enabled = false; }

            int perRow = Mathf.Max(1, _propsPerRowPerSide) * 2;
            float buildHalfHeight = BuildHalfHeight();
            for (int i = 0; i < _scatter.Count; i++)
            {
                int row = i / perRow;
                TryPlaceScatterProp(_scatter[i], -buildHalfHeight + row * _propRowSpacing, _propRowSpacing * 0.98f);
            }
        }

        // ── Theme ────────────────────────────────────────────────────────

        private HighwayDriveEnvironmentTheme ResolveTheme()
        {
            if (_config == null) return HighwayDriveEnvironmentTheme.Green;
            if (_config.ThemeMode == HighwayDriveEnvironmentThemeMode.Fixed) return _config.FixedTheme;

            int count = Enum.GetValues(typeof(HighwayDriveEnvironmentTheme)).Length;
            int index = HighwayDriveControlMath.ThemeIndexForDay(DateTime.Now.DayOfYear, count);
            return (HighwayDriveEnvironmentTheme)index;
        }

        private Sprite ThemeAsphalt() => ActiveTheme switch
        {
            HighwayDriveEnvironmentTheme.Desert => _desertAsphalt,
            HighwayDriveEnvironmentTheme.Snow => _snowAsphalt,
            _ => _greenAsphalt,
        };

        private Sprite ThemeLaneDash() => ActiveTheme switch
        {
            HighwayDriveEnvironmentTheme.Desert => _desertLaneDash,
            HighwayDriveEnvironmentTheme.Snow => _snowLaneDash,
            _ => _greenLaneDash,
        };

        private Sprite ThemeEdge(bool left) => ActiveTheme switch
        {
            HighwayDriveEnvironmentTheme.Desert => left ? _desertEdgeLeft : _desertEdgeRight,
            HighwayDriveEnvironmentTheme.Snow => left ? _snowEdgeLeft : _snowEdgeRight,
            _ => left ? _greenEdgeLeft : _greenEdgeRight,
        };


        private Sprite[] ThemeProps() => ActiveTheme switch
        {
            HighwayDriveEnvironmentTheme.Desert => _desertProps,
            HighwayDriveEnvironmentTheme.Snow => _snowProps,
            _ => _greenProps,
        };

        /// <summary>Base colour under the scattered ground cover. Deliberately
        /// darker and less saturated than the props on it: this reads as shaded
        /// ground BETWEEN the grass and rocks, not as the grass itself. A bright
        /// flat green competes with the scatter and makes the gaps look like
        /// gaps.</summary>
        private Color GroundColorForTheme() => ActiveTheme switch
        {
            HighwayDriveEnvironmentTheme.Desert => new Color(0.68f, 0.53f, 0.33f, 1f),
            HighwayDriveEnvironmentTheme.Snow => new Color(0.80f, 0.84f, 0.89f, 1f),
            _ => new Color(0.28f, 0.42f, 0.22f, 1f),
        };

        // ── Bounds ───────────────────────────────────────────────────────

        private void ComputeBounds()
        {
            float halfHeight = _camera != null && _camera.orthographic ? _camera.orthographicSize : 5.4f;
            float aspect = _camera != null && _camera.aspect > 0.1f ? _camera.aspect : 16f / 9f;
            float margin = _config != null ? _config.VerticalBuildMargin : 4f;

            ViewTopY = halfHeight;
            ViewBottomY = -halfHeight;
            ViewHalfWidth = halfHeight * aspect;
            SpawnY = halfHeight + margin * 0.5f;
            DespawnBelowY = -halfHeight - margin;
            DespawnAboveY = halfHeight + margin;
        }

        private float RoadHalfWidth() => _config != null ? _config.RoadHalfWidth : 3.9f;

        private int LaneCount() => _config != null ? Mathf.Max(2, _config.LaneCount) : 3;

        private float GroundHalfWidth() => ViewHalfWidth + 2f;

        private float BuildHalfHeight()
        {
            float margin = _config != null ? _config.VerticalBuildMargin : 4f;
            return (ViewTopY - ViewBottomY) * 0.5f + margin * 2f;
        }

        // ── Static layers ────────────────────────────────────────────────

        // NO BACKDROP LAYER, DELIBERATELY. The art pack ships a horizon
        // landscape per theme and an earlier version built one. It was never
        // visible -- the ground quad is opaque, spans the whole build area and
        // sorts above it. Fixing the sort order would not have helped: the
        // asset is a perspective landscape with a sky and a receding horizon,
        // and this is a strict bird's-eye camera looking straight down. There
        // is no horizon to draw.

        private void BuildGround()
        {
            Transform ground = CreateSolid("Ground", new Vector2(GroundHalfWidth() * 2f, BuildHalfHeight() * 2f),
                Vector2.zero, GroundColorForTheme(), SortingGround);
            _groundRenderer = ground.GetComponent<SpriteRenderer>();
        }

        /// <summary>The asphalt, tiled along the road with every other tile
        /// mirrored vertically so the joins match exactly whatever the source
        /// tile edges look like. Mirroring makes the true pattern period TWO
        /// tiles, which is what _roadWrapPeriod carries -- see this class's
        /// header.</summary>
        private void BuildRoadSurface()
        {
            _roadPeriod = Mathf.Max(0.5f, _asphaltTileLengthWorld);
            _roadWrapPeriod = _roadPeriod * 2f;

            Sprite sprite = ThemeAsphalt();
            float width = RoadHalfWidth() * 2f;

            if (sprite == null)
            {
                CreateSolid("RoadSurface", new Vector2(width, BuildHalfHeight() * 2f),
                    Vector2.zero, _config != null ? _config.RoadColor : new Color(0.16f, 0.17f, 0.19f, 1f),
                    SortingRoadSurface);
                return;
            }

            var root = new GameObject("RoadSurface");
            root.transform.SetParent(transform, false);

            float buildHalfHeight = BuildHalfHeight();
            int rows = Mathf.CeilToInt((buildHalfHeight * 2f + _roadWrapPeriod) / _roadPeriod) + 1;

            for (int row = 0; row < rows; row++)
            {
                float y = -buildHalfHeight + row * _roadPeriod;
                var go = new GameObject($"Asphalt_{row}");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(0f, y, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = SortingRoadSurface;
                HighwayDriveSpriteFactory.FitToWorldSize(sr, new Vector2(width, _roadPeriod));
                _asphaltRenderers.Add(sr);

                if (row % 2 == 1) FlipVertically(go.transform);
                _roadItems.Add(new ScrollItem { T = go.transform, BaseX = 0f, BaseY = y, Period = _roadWrapPeriod });
            }
        }


        /// <summary>Dashes on the INTERIOR lane boundaries only -- boundary i
        /// sits between lane i-1 and lane i, so a LaneCount-lane road gets
        /// LaneCount-1 of them and neither edge gets one. Not mirrored, so
        /// these wrap on a single segment length.</summary>
        private void BuildLaneDashes()
        {
            _dashPeriod = Mathf.Max(0.5f, _dashSegmentLengthWorld);
            Sprite sprite = ThemeLaneDash();

            int laneCount = LaneCount();
            float halfWidth = RoadHalfWidth();
            float laneWidth = HighwayDriveControlMath.LaneWidth(halfWidth, laneCount);
            float buildHalfHeight = BuildHalfHeight();
            int rows = Mathf.CeilToInt((buildHalfHeight * 2f + _dashPeriod) / _dashPeriod) + 1;

            var root = new GameObject("LaneDashes");
            root.transform.SetParent(transform, false);

            for (int boundary = 1; boundary < laneCount; boundary++)
            {
                float x = -halfWidth + laneWidth * boundary;
                for (int row = 0; row < rows; row++)
                {
                    float y = -buildHalfHeight + row * _dashPeriod;
                    Transform t;

                    if (sprite != null)
                    {
                        var go = new GameObject($"Dash_{boundary}_{row}");
                        go.transform.SetParent(root.transform, false);
                        go.transform.localPosition = new Vector3(x, y, 0f);

                        var sr = go.AddComponent<SpriteRenderer>();
                        sr.sprite = sprite;
                        sr.sortingOrder = SortingLaneDash;
                        float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
                        HighwayDriveSpriteFactory.FitToWorldSize(sr,
                            new Vector2(_dashPeriod * aspect, _dashPeriod));
                        _dashRenderers.Add(sr);
                        t = go.transform;
                    }
                    else
                    {
                        t = CreateSolid($"Dash_{boundary}_{row}", new Vector2(0.12f, 1.2f),
                            new Vector2(x, y), _config != null ? _config.LaneMarkingColor : Color.white,
                            SortingLaneDash, root.transform);
                    }

                    _roadItems.Add(new ScrollItem { T = t, BaseX = x, BaseY = y, Period = _dashPeriod });
                }
            }
        }

        /// <summary>Solid unbroken lines at both road edges. Static on purpose
        /// -- a solid line carries no motion information.</summary>
        private void BuildEdgeLines()
        {
            float halfWidth = RoadHalfWidth();
            float lineWidth = _config != null ? _config.EdgeLineWidthWorld : 0.18f;
            float inset = lineWidth * 0.5f;
            float height = BuildHalfHeight() * 2f;

            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                float x = left ? -halfWidth + inset : halfWidth - inset;
                Sprite sprite = ThemeEdge(left);

                if (sprite != null)
                {
                    var go = new GameObject(left ? "EdgeLineLeft" : "EdgeLineRight");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(x, 0f, 0f);

                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sortingOrder = SortingEdgeLine;
                    HighwayDriveSpriteFactory.FitToWorldSize(sr, new Vector2(lineWidth, height));
                    _edgeRenderers[side] = sr;
                }
                else
                {
                    CreateSolid(left ? "EdgeLineLeft" : "EdgeLineRight", new Vector2(lineWidth, height),
                        new Vector2(x, 0f),
                        _config != null ? _config.EdgeLineColor : Color.yellow, SortingEdgeLine);
                }
            }
        }

        // ── Scattered ground cover (recycled, NOT modulo-wrapped) ────────

        /// <summary>Ground cover scattered across the whole off-road band. The
        /// visible area reaches roughly 9.6 units from centre against a
        /// 3.9-unit road half-width, so without this the whole off-road area --
        /// nearly six units per side -- is a flat slab of one colour. This is
        /// now the ONLY thing dressing it, which is why it runs from just
        /// outside the edge line all the way past the screen edge: no band, no
        /// boundary, nothing to cut off.
        ///
        /// RECYCLED PER PROP rather than modulo-wrapped, because randomised
        /// position/sprite/scale is not periodic with any length and modulo
        /// scrolling of non-periodic content jumps visibly at every wrap -- see
        /// this class's header.
        ///
        /// Seeded System.Random, not UnityEngine.Random: the scenery sequence
        /// is then reproducible, and seeding it here cannot perturb the global
        /// sequence that traffic spawning draws from.</summary>
        private void BuildScatter()
        {
            Sprite[] props = ThemeProps();
            ActiveProps = props;
            if (props == null || props.Length == 0) return;

            _scatterSprites = props;
            _scatterRng = new System.Random(20260824);
            _propRowSpacing = Mathf.Max(0.5f, _config != null ? _config.RoadsidePropIntervalWorld : 3.2f);
            _scatterBaseHeight = _config != null ? Mathf.Max(0.05f, _config.RoadsidePropHeightWorld) : 0.9f;

            float roadHalf = RoadHalfWidth();
            _scatterInnerX = roadHalf + _scatterRoadClearance;
            _scatterOuterX = Mathf.Max(_scatterInnerX + 0.5f, ViewHalfWidth + 1.5f);

            float buildHalfHeight = BuildHalfHeight();
            _scatterTopY = buildHalfHeight;

            int rows = Mathf.CeilToInt(buildHalfHeight * 2f / _propRowSpacing);
            int perSide = Mathf.Max(1, _propsPerRowPerSide);

            var root = new GameObject("GroundCover");
            root.transform.SetParent(transform, false);

            for (int row = 0; row < rows; row++)
            {
                for (int side = 0; side < 2; side++)
                {
                    for (int k = 0; k < perSide; k++)
                    {
                        var go = new GameObject($"Prop_{_scatter.Count}");
                        go.transform.SetParent(root.transform, false);
                        var sr = go.AddComponent<SpriteRenderer>();
                        sr.sortingOrder = SortingRoadsideProp;

                        var prop = new ScatterProp { T = go.transform, SR = sr };
                        _scatter.Add(prop);

                        // Initial fill spreads the whole build height; recycled
                        // props re-enter from above thereafter.
                        TryPlaceScatterProp(prop, -buildHalfHeight + row * _propRowSpacing,
                            _propRowSpacing * 0.98f);
                    }
                }
            }
        }

        private void AdvanceScatter(float delta)
        {
            float window = _propRowSpacing * Mathf.Max(1f, _scatterReentryWindowRows);

            for (int i = 0; i < _scatter.Count; i++)
            {
                ScatterProp p = _scatter[i];

                if (!p.Placed)
                {
                    // Waiting for clear ground. Retried every frame, and it is
                    // invisible while it waits, so the only thing at stake is
                    // how soon it rejoins the scenery.
                    TryPlaceScatterProp(p, _scatterTopY, window);
                    continue;
                }

                p.Y -= delta;

                if (p.Y < DespawnBelowY)
                {
                    TryPlaceScatterProp(p, _scatterTopY, window);
                    continue;
                }

                p.T.localPosition = new Vector3(p.X, p.Y, 0f);
            }
        }

        /// <summary>Places one prop: pick the sprite and scale FIRST, then
        /// choose x from its resulting width.
        ///
        /// PROPS ARE POSITIONED BY THEIR INNER EDGE, NOT THEIR CENTRE. Sprite
        /// and scale are both randomised and the widest props are over two
        /// world units across at full scale, so choosing a centre anywhere in
        /// [inner, outer] let up to half a prop's width hang across the edge
        /// line and onto the carriageway -- about a third of a lane for a
        /// full-scale rock. Offsetting the lower bound by the prop's own
        /// half-width makes overhang impossible for any sprite at any scale,
        /// rather than merely unlikely for the ones that happen to be narrow.
        ///
        /// This is also why the size has to be resolved before the position
        /// and not after: the width IS an input to the placement.</summary>
        /// <summary>Places one prop on ground clear of every other prop, or
        /// leaves it WAITING off-screen if there is nowhere clear right now.
        ///
        /// Plain uniform sampling put plants and rocks on top of each other
        /// often enough to look like a rendering fault rather than scenery. An
        /// intermediate version rejected overlapping candidates but fell back
        /// to the least-bad position after N tries -- which sounds harmless and
        /// is not: at the shipped density that fallback fired on roughly a
        /// quarter of placements, so overlaps persisted, just fewer of them.
        ///
        /// Waiting instead of compromising is what makes the result exact. A
        /// prop with nowhere to go keeps its renderer off and retries next
        /// frame; because it waits ABOVE the camera it is invisible either way,
        /// so the only cost of waiting is a prop that would have been off-screen
        /// anyway. Density self-regulates to whatever genuinely fits.
        ///
        /// Simulated over 3 minutes of driving at the shipped settings: zero
        /// overlapping pairs, with a prop waiting on about 10 frames total.
        ///
        /// The guarantee holds permanently WITHIN this tier, because every prop
        /// here moves at the same speed -- two that are clear when placed can
        /// never drift into each other. It deliberately does NOT extend across
        /// tiers: landmarks run at a different parallax rate and so must pass
        /// over the ground cover, which is depth rather than a defect.
        ///
        /// Candidates vary Y as well as X within the caller's window. Searching
        /// only in X makes a crowded row unsolvable when the answer is simply
        /// to sit slightly higher or lower.</summary>
        private bool TryPlaceScatterProp(ScatterProp prop, float yMin, float yRange)
        {
            Sprite sprite = _scatterSprites[_scatterRng.Next(_scatterSprites.Length)];
            float scale = Mathf.Lerp(0.65f, 1.45f, (float)_scatterRng.NextDouble());
            Vector2 size = PropSizeForSprite(sprite, _scatterBaseHeight * scale);

            float halfWidth = size.x * 0.5f;
            float minCenter = _scatterInnerX + halfWidth;
            // Guard the degenerate case of a prop wider than the whole band, so
            // the range never inverts and flips the prop back over the road.
            float maxCenter = Mathf.Max(minCenter, _scatterOuterX);

            int attempts = Mathf.Max(1, _scatterPlacementAttempts);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                float side = _scatterRng.Next(2) == 0 ? -1f : 1f;
                float x = side * Mathf.Lerp(minCenter, maxCenter, (float)_scatterRng.NextDouble());
                float y = yMin + (float)_scatterRng.NextDouble() * yRange;

                if (Overlaps(prop, x, y, size)) continue;

                prop.SR.sprite = sprite;
                HighwayDriveSpriteFactory.FitToWorldSize(prop.SR, size);
                prop.SR.enabled = true;
                prop.X = x;
                prop.Y = y;
                prop.Size = size;
                prop.Placed = true;
                prop.T.localPosition = new Vector3(x, y, 0f);
                return true;
            }

            // Nowhere clear: wait off-screen. Hidden rather than parked at the
            // origin, which would put it in the middle of the carriageway.
            prop.Placed = false;
            prop.SR.enabled = false;
            prop.Y = yMin;
            return false;
        }

        /// <summary>Whether a candidate footprint would touch any prop already
        /// placed, allowing a small breathing gap so props that merely graze
        /// still read as separate plants.</summary>
        private bool Overlaps(ScatterProp self, float x, float y, Vector2 size)
        {
            float pad = _scatterSpacingPad;

            for (int i = 0; i < _scatter.Count; i++)
            {
                ScatterProp other = _scatter[i];
                if (other == self || !other.Placed) continue;

                float dy = Mathf.Abs(y - other.Y);
                float limitY = (size.y + other.Size.y) * 0.5f + pad;
                if (dy >= limitY) continue;             // cheap early-out on the long axis

                float dx = Mathf.Abs(x - other.X);
                float limitX = (size.x + other.Size.x) * 0.5f + pad;
                if (dx < limitX) return true;
            }
            return false;
        }

        /// <summary>World size to render a prop at: exactly heightWorld tall,
        /// width scaled to preserve THE SPRITE'S OWN aspect ratio. Not a fixed
        /// Vector2 from config, because the theme sprites are genuinely
        /// different shapes and one shared pair would squash some of them.</summary>
        public static Vector2 PropSizeForSprite(Sprite sprite, float heightWorld)
        {
            if (sprite == null || sprite.rect.height <= 0f) return new Vector2(heightWorld, heightWorld);
            float aspect = sprite.rect.width / sprite.rect.height;
            return new Vector2(heightWorld * aspect, heightWorld);
        }

        // ── Scroll ───────────────────────────────────────────────────────

        /// <summary>Each modulo-wrapped element sits at its base position
        /// shifted down by the accumulated distance modulo ITS OWN period.
        /// Mathf.Repeat (not the % operator) because the result must stay
        /// non-negative.</summary>
        private void ApplyScroll()
        {
            for (int i = 0; i < _roadItems.Count; i++)
            {
                ScrollItem it = _roadItems[i];
                float shift = Mathf.Repeat(_roadScrollDistance, it.Period);
                it.T.localPosition = new Vector3(it.BaseX, it.BaseY - shift, 0f);
            }

        }

        private static void FlipVertically(Transform t)
        {
            Vector3 s = t.localScale;
            s.y = -s.y;
            t.localScale = s;
        }

        /// <summary>One flat-coloured rectangle, for the ground fill and for
        /// fallbacks when a sprite is unassigned. Every solid shares ONE white
        /// sprite tinted through SpriteRenderer.color, so they batch rather
        /// than costing a draw call each.</summary>
        private Transform CreateSolid(string name, Vector2 worldSize, Vector2 position, Color color,
            int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = new Vector3(position.x, position.y, 0f);
            go.transform.localScale = new Vector3(Mathf.Max(0.001f, worldSize.x), Mathf.Max(0.001f, worldSize.y), 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SharedWhiteSprite();
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return go.transform;
        }

        private Sprite SharedWhiteSprite()
        {
            if (_whiteSprite == null)
                _whiteSprite = HighwayDriveSpriteFactory.BuildRectFill(8, 8, 8f, Color.white);
            return _whiteSprite;
        }
    }
}
