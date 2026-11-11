using System.Collections.Generic;
using UnityEngine;

namespace RehabFramework.HighwayDrive
{
    // ─────────────────────────────────────────────────────────────────────────
    //  HIGHWAY DRIVE LANDMARKS (Simon's spec, 2026-08-24)
    //  Distinctive top-down scenery -- ponds, houses, wind turbines, farms --
    //  placed OUTSIDE the road so it occasionally travels past the patient and
    //  reinforces forward speed.
    //
    //  SPARSE ON PURPOSE, and that restraint is the entire design. Roughly one
    //  every several seconds is enough: filling every empty verge with scenery
    //  means nothing stands out, nothing registers as passing, and the
    //  peripheral field turns into visual noise competing with the traffic the
    //  patient actually has to watch. The small repeating grass-and-rock
    //  scatter is a separate, much denser system that lives in
    //  HighwayDriveRoad; this class is only for the occasional landmark.
    //
    //  AND THE CENTRAL ROADWAY STAYS CLEAN. Landmarks are pushed out past
    //  LandmarkClearanceWorld from the road edge, so they never crowd the lane
    //  the patient is steering in, and they are placed only where they will
    //  actually be visible (inside the camera's half-width).
    //
    //  Pooled and scrolled exactly like HighwayDriveHearts: a landmark is a
    //  STATIONARY world object, so its apparent motion is simply the player's
    //  own true speed downward -- the same relative-motion formula traffic
    //  uses, with the object's own speed fixed at zero. It therefore travels
    //  at the same rate as the verge it is sitting on.
    // ─────────────────────────────────────────────────────────────────────────

    public class HighwayDriveLandmarks : MonoBehaviour
    {
        private const int SortingLandmark = -13;

        [Header("Dependencies")]
        [SerializeField] private HighwayDriveConfig _config;
        [SerializeField] private HighwayDriveRoad _road;

        [Header("Art")]
        [Tooltip("Distinctive top-down landmark tiles. Assigned by HighwayDriveSceneBuilder from Art/" +
                 "landmark_*.png; if empty this component simply does nothing.")]
        [SerializeField] private Sprite[] _landmarkSprites;
        [Tooltip("Along-road gap required between two landmarks on the same verge, world units.")]
        [SerializeField] private float _landmarkSpacingPad = 1.5f;

        private class Landmark
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public bool Active;
            /// <summary>World footprint, so a new spawn can check it is not
            /// landing on top of this one.</summary>
            public Vector2 Size;
        }

        private readonly List<Landmark> _pool = new List<Landmark>();
        private float _spawnTimer;
        private Transform _root;
        private bool _built;
        private bool _lastSideWasLeft;

        private void Start()
        {
            _root = new GameObject("Landmarks").transform;
            _root.SetParent(transform, false);
            _spawnTimer = RandomInterval() * 0.5f;
            _built = true;
        }

        public void Tick(float playerSpeedWorldPerS, float deltaTime, bool allowSpawn)
        {
            if (!_built || _road == null || !_road.IsBuilt) return;
            if (_config != null && !_config.ShowLandmarks) return;
            if (_landmarkSprites == null || _landmarkSprites.Length == 0) return;

            // THE SLOWEST TIER. Landmarks drift at LandmarkParallaxFactor of
            // the true speed, capped -- slower even than the ground cover they
            // stand among. That is what lets a house or a pond enter from the
            // top, stay on screen for several seconds, and travel gently down
            // the side, which is the whole point of having them: it is what
            // makes the desert, snow and green environments feel like different
            // PLACES rather than different-coloured moving textures.
            float parallaxSpeed;
            if (_config != null && _config.LegacySceneryCoupling)
            {
                parallaxSpeed = playerSpeedWorldPerS * 1.15f;   // pre-2026-08-25 tuning
            }
            else
            {
                float factor = _config != null ? _config.LandmarkParallaxFactor : 0.2f;
                float cap = _config != null ? _config.LandmarkMaxSpeedWorldPerS : 3f;
                parallaxSpeed = HighwayDriveControlMath.ParallaxSpeed(playerSpeedWorldPerS, factor, cap);
            }
            for (int i = 0; i < _pool.Count; i++)
            {
                Landmark l = _pool[i];
                if (!l.Active) continue;

                Vector3 p = l.Transform.localPosition;
                p.y -= parallaxSpeed * deltaTime;
                l.Transform.localPosition = p;
                if (p.y < _road.DespawnBelowY) Deactivate(l);
            }

            if (!allowSpawn) return;
            _spawnTimer -= deltaTime;
            if (_spawnTimer > 0f) return;

            Spawn();
            _spawnTimer = RandomInterval();
        }

        public void ClearAll()
        {
            for (int i = 0; i < _pool.Count; i++) Deactivate(_pool[i]);
            _spawnTimer = RandomInterval();
        }

        private float RandomInterval()
        {
            float min = _config != null ? Mathf.Max(0.5f, _config.LandmarkIntervalMinS) : 4.5f;
            float max = _config != null ? Mathf.Max(min, _config.LandmarkIntervalMaxS) : 9f;
            return Random.Range(min, max);
        }

        private void Spawn()
        {
            Sprite sprite = _landmarkSprites[Random.Range(0, _landmarkSprites.Length)];
            if (sprite == null) return;

            float height = _config != null ? Mathf.Max(0.2f, _config.LandmarkHeightWorld) : 2.6f;
            Vector2 size = HighwayDriveRoad.PropSizeForSprite(sprite, height);

            float roadHalf = _config != null ? _config.RoadHalfWidth : 3.9f;
            float clearance = _config != null ? _config.LandmarkClearanceWorld : 1.4f;

            // Alternate sides so landmarks do not cluster on one verge purely
            // by chance, which would leave the other side looking abandoned.
            _lastSideWasLeft = !_lastSideWasLeft;
            float inner = roadHalf + clearance + size.x * 0.5f;
            float outer = Mathf.Max(inner, _road.ViewHalfWidth - size.x * 0.4f);
            float x = Random.Range(inner, outer) * (_lastSideWasLeft ? -1f : 1f);

            float y = _road.SpawnY + size.y;

            // Do not stack one landmark on another. Every landmark moves at the
            // same rate, so a pair that is clear at spawn stays clear for good
            // -- one check here is a permanent guarantee, not a per-frame cost.
            // Skipping rather than nudging keeps them genuinely sparse: a
            // landmark that cannot be placed cleanly is one the scene did not
            // need, and the next interval comes round soon enough.
            for (int i = 0; i < _pool.Count; i++)
            {
                Landmark other = _pool[i];
                if (!other.Active) continue;

                Vector3 op = other.Transform.localPosition;
                if (Mathf.Sign(op.x) != Mathf.Sign(x)) continue;   // other verge, cannot clash

                float dy = Mathf.Abs(y - op.y);
                if (dy < (size.y + other.Size.y) * 0.5f + _landmarkSpacingPad) return;
            }

            Landmark l = TakeFromPool();
            l.Renderer.sprite = sprite;
            HighwayDriveSpriteFactory.FitToWorldSize(l.Renderer, size);
            l.Transform.localPosition = new Vector3(x, y, 0f);
            l.Size = size;
            l.Renderer.enabled = true;
            l.Active = true;
        }

        private Landmark TakeFromPool()
        {
            for (int i = 0; i < _pool.Count; i++)
                if (!_pool[i].Active) return _pool[i];

            var go = new GameObject($"Landmark_{_pool.Count}");
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = SortingLandmark;

            var l = new Landmark { Transform = go.transform, Renderer = sr, Active = false };
            _pool.Add(l);
            return l;
        }

        private static void Deactivate(Landmark l)
        {
            l.Active = false;
            l.Renderer.enabled = false;
        }
    }
}
