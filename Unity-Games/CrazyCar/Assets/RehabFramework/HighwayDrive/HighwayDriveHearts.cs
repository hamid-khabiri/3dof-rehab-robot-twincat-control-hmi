using System.Collections.Generic;
using UnityEngine;

namespace RehabFramework.HighwayDrive
{
    // ─────────────────────────────────────────────────────────────────────────
    //  HIGHWAY DRIVE HEARTS (Simon's request, 2026-08-19)
    //  Spawns, moves, and collects the on-road heart pickups that refill
    //  HighwayDriveController.CurrentHearts. Same pooled-object shape as
    //  HighwayDriveTraffic, deliberately kept as its own class rather than
    //  folded into that one -- pickups and traffic are different kinds of
    //  thing (one is collided with to end a run, the other is driven into on
    //  purpose to gain something) and mixing their spawn/pool bookkeeping
    //  would make both harder to read for no shared benefit.
    //
    //  A HEART APPROACHES AT ITS OWN FIXED RATE, not at the road speed. It
    //  used to move as a stationary road object would, which meant it swept
    //  past in well under a second at cruise -- far too fast for a
    //  rehabilitation participant to notice it, plan a reach and execute one,
    //  so collection was effectively luck. It now closes at whatever speed
    //  gives HeartApproachSeconds of visible approach, independent of the
    //  throttle, so the planning window is identical every time. See
    //  MoveHearts and HighwayDriveControlMath.HeartClosingSpeed.
    //
    //  THE DDA PART lives in HighwayDriveControlMath.HeartsMaySpawn/
    //  HeartSpawnIntervalS, not here -- this class just supplies the live
    //  heart count every Tick() and asks those two pure functions whether and
    //  how soon to try again. See that file for the actual tuning curve.
    //
    //  Deliberately NOT here: any attempt to avoid spawning a heart near an
    //  existing traffic car. The two spawners are independent pools with no
    //  shared bookkeeping, so an occasional visual overlap is possible --
    //  harmless, since a heart sitting near a traffic car just means the
    //  player reaches the car first (ending the run) before ever reaching
    //  the heart behind it. Not worth the cross-referencing complexity for a
    //  purely cosmetic edge case.
    // ─────────────────────────────────────────────────────────────────────────

    public class HighwayDriveHearts : MonoBehaviour
    {
        private const int SortingHeartPickup = 1; // just above traffic (0), below the player car (5)

        [Header("Dependencies")]
        [SerializeField] private HighwayDriveConfig _config;
        [SerializeField] private HighwayDriveRoad _road;

        [Header("Art")]
        [Tooltip("Art/heart_glow.png -- the glowing collectible. Falls back to the procedural heart if " +
                 "unassigned.")]
        [SerializeField] private Sprite _pickupSprite;

        private class HeartPickup
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public bool Active;
            /// <summary>Scale that renders the sprite at exactly _size. The
            /// pulse multiplies this rather than accumulating on the live
            /// scale, which would compound frame over frame.</summary>
            public Vector3 BaseScale;
        }

        private readonly List<HeartPickup> _pool = new List<HeartPickup>();
        private Sprite _heartSprite;
        private Vector2 _size;
        private float _spawnTimer;
        private Transform _root;
        private bool _built;

        private void Start()
        {
            _size = _config != null ? _config.HeartPickupSize : new Vector2(0.55f, 0.55f);
            _size = new Vector2(Mathf.Max(0.05f, _size.x), Mathf.Max(0.05f, _size.y));

            if (_pickupSprite != null)
            {
                _heartSprite = _pickupSprite;
            }
            else
            {
                Color color = _config != null ? _config.HeartColor : new Color(0.90f, 0.22f, 0.32f, 1f);
                const int widthPx = 64;
                float ppu = widthPx / _size.x;
                int heightPx = Mathf.Max(2, Mathf.RoundToInt(_size.y * ppu));
                _heartSprite = HighwayDriveSpriteFactory.BuildHeart(widthPx, heightPx, ppu, color);
            }

            _root = new GameObject("HeartPickups").transform;
            _root.SetParent(transform, false);

            // Starting with a short delay rather than 0 -- an instant spawn
            // the moment a fresh run begins would put a pickup on screen
            // before the patient has even started driving, which reads as
            // scenery placed there rather than as something that "appeared".
            _spawnTimer = 3f;
            _built = true;
        }

        /// <summary>Advance heart pickups by one frame. currentHearts is what
        /// feeds HighwayDriveControlMath's DDA curve -- see this class's
        /// header. deltaTime is an EFFECTIVE delta, matching
        /// HighwayDriveTraffic.Tick's own parameter (the controller scales it
        /// during the post-crash coast so nothing here appears to speed up at
        /// the exact moment the patient has been told to stop).</summary>
        public void Tick(float playerSpeedWorldPerS, float deltaTime, bool allowSpawn, int currentHearts,
            float playerX)
        {
            if (!_built || _road == null || !_road.IsBuilt) return;

            MoveHearts(playerSpeedWorldPerS, deltaTime);
            PulseHearts();

            if (!allowSpawn) return;

            int maxHearts = _config != null ? Mathf.Max(1, _config.MaxHearts) : 5;
            if (!HighwayDriveControlMath.HeartsMaySpawn(currentHearts, maxHearts))
            {
                // Not counting down while spawning is disallowed -- otherwise
                // the timer could run out while the patient sits at full
                // hearts and then fire the INSTANT a heart is lost, which
                // would read as suspiciously well-timed rather than random.
                return;
            }

            _spawnTimer -= deltaTime;
            if (_spawnTimer > 0f) return;

            TrySpawn(playerX);
            float baseS = _config != null ? Mathf.Max(0.5f, _config.HeartSpawnIntervalBaseS) : 25f;
            float minS = _config != null ? Mathf.Max(0.5f, _config.HeartSpawnIntervalMinS) : 8f;
            _spawnTimer = HighwayDriveControlMath.HeartSpawnIntervalS(currentHearts, maxHearts, baseS, minS);
        }

        /// <summary>Deactivates every pickup. Called at the start of each run,
        /// same reasoning as HighwayDriveTraffic.ClearAll -- a new run should
        /// not begin with a leftover pickup sitting on top of the player's
        /// start position.</summary>
        public void ClearAll()
        {
            for (int i = 0; i < _pool.Count; i++) Deactivate(_pool[i]);
            _spawnTimer = 3f;
        }

        /// <summary>Axis-aligned overlap test of the player's box against every
        /// active pickup, same shape as HighwayDriveTraffic.TryFindCollision.
        /// The FIRST overlapping pickup is collected and immediately
        /// deactivated -- the caller (HighwayDriveController.CheckHeartPickup)
        /// decides whether that collection actually raises the heart count,
        /// but the pickup disappears from the road either way.</summary>
        public bool TryCollect(Vector2 playerCenter, Vector2 playerSize, out Vector2 contactPoint)
        {
            contactPoint = Vector2.zero;
            for (int i = 0; i < _pool.Count; i++)
            {
                HeartPickup h = _pool[i];
                if (!h.Active) continue;

                Vector3 p = h.Transform.localPosition;
                if (!HighwayDriveControlMath.RectsOverlap(
                        playerCenter.x, playerCenter.y, playerSize.x, playerSize.y,
                        p.x, p.y, _size.x, _size.y))
                    continue;

                contactPoint = p;
                Deactivate(h);
                return true;
            }
            return false;
        }

        /// <summary>Moves every active pickup at a FIXED closing speed, chosen
        /// so it takes HeartApproachSeconds to cross the visible distance from
        /// the top of the view down to the car.
        ///
        /// Deliberately NOT the vehicle-relative rate. A pickup travelling at
        /// the road speed sweeps past in well under a second at cruise, which
        /// is far too fast for a rehabilitation participant to notice, plan a
        /// reach, and execute it -- so the collectible was effectively
        /// unobtainable except by luck. Fixing the approach rate gives the same
        /// planning window every time regardless of what the participant is
        /// doing with the throttle, which is the property that makes it
        /// trainable. The small physical inconsistency (a road object drifting
        /// at its own pace) is a price worth paying for that.</summary>
        private void MoveHearts(float playerSpeedWorldPerS, float deltaTime)
        {
            float closing;
            if (_config != null && _config.LegacyHeartPacing)
            {
                // Pre-2026-08-25: the pickup behaved as a stationary road
                // object, so it closed at the full driving speed and swept past
                // in under a second.
                closing = playerSpeedWorldPerS;
            }
            else
            {
                float visibleApproach = Mathf.Max(1f, _road.ViewTopY - PlayerAnchorY());
                float approachSeconds = _config != null ? Mathf.Max(0.2f, _config.HeartApproachSeconds) : 2.5f;
                closing = HighwayDriveControlMath.HeartClosingSpeed(visibleApproach, approachSeconds);
            }

            for (int i = 0; i < _pool.Count; i++)
            {
                HeartPickup h = _pool[i];
                if (!h.Active) continue;

                Vector3 p = h.Transform.localPosition;
                p.y -= closing * deltaTime;
                h.Transform.localPosition = p;

                if (p.y < _road.DespawnBelowY) Deactivate(h);
            }
        }

        /// <summary>A subtle breathing scale so the pickup reads as something
        /// to collect rather than as one more obstacle to avoid. Applied on top
        /// of the fitted world size, so it never changes the collision test --
        /// TryCollect always uses the nominal _size.</summary>
        private void PulseHearts()
        {
            if (_config == null) return;
            float hz = Mathf.Max(0f, _config.HeartPulseHz);
            float amount = Mathf.Clamp(_config.HeartPulseAmount, 0f, 0.4f);
            if (hz <= 0f || amount <= 0f) return;

            float pulse = 1f + amount * (Mathf.Sin(Time.time * hz * Mathf.PI * 2f) + 1f) * 0.5f;
            for (int i = 0; i < _pool.Count; i++)
            {
                HeartPickup h = _pool[i];
                if (!h.Active) continue;
                h.Transform.localScale = h.BaseScale * pulse;
            }
        }

        private float PlayerAnchorY() => _config != null ? _config.PlayerAnchorY : -2.6f;

        /// <summary>Spawns WITHIN COMFORTABLE REACH of where the car already
        /// is, not in a random lane.
        ///
        /// Hearts are an optional bonus, so they must never bait a sudden large
        /// lateral movement: a pickup placed across the road tempts exactly the
        /// abrupt swerve most likely to end in a collision, which would make
        /// the reward mechanic actively harmful to the assessment. Offsetting
        /// from the current position by at most HeartLateralReachWorld keeps
        /// every collection a small, deliberate correction -- and missing one
        /// costs nothing, so declining the reach is always a valid choice.</summary>
        private void TrySpawn(float playerX)
        {
            if (_config != null && _config.LegacyHeartPacing)
            {
                // Pre-2026-08-25: a random lane, with no regard for how far
                // that was from the car.
                int laneCount = Mathf.Max(2, _config.LaneCount);
                int lane = Random.Range(0, laneCount);
                HeartPickup legacy = TakeFromPool();
                legacy.Transform.localPosition = new Vector3(
                    HighwayDriveControlMath.LaneCenterX(lane, laneCount, _config.RoadHalfWidth),
                    _road.SpawnY, 0f);
                legacy.Renderer.enabled = true;
                legacy.Active = true;
                return;
            }

            float roadHalfWidth = _config != null ? _config.RoadHalfWidth : 3.9f;
            float reach = _config != null ? Mathf.Max(0.1f, _config.HeartLateralReachWorld) : 2.6f;
            float margin = _config != null ? _config.LateralSafetyMarginWorld : 0.12f;

            float offset = ((float)Random.Range(-1f, 1f)) * reach;
            float limit = Mathf.Max(0f, roadHalfWidth - margin - _size.x * 0.5f);
            float x = Mathf.Clamp(playerX + offset, -limit, limit);

            HeartPickup h = TakeFromPool();
            h.Transform.localPosition = new Vector3(x, _road.SpawnY, 0f);
            h.Renderer.enabled = true;
            h.Active = true;
        }

        private HeartPickup TakeFromPool()
        {
            for (int i = 0; i < _pool.Count; i++)
                if (!_pool[i].Active) return _pool[i];

            var go = new GameObject($"HeartPickup_{_pool.Count}");
            go.transform.SetParent(_root, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _heartSprite;
            sr.sortingOrder = SortingHeartPickup;
            HighwayDriveSpriteFactory.FitToWorldSize(sr, _size);

            var h = new HeartPickup
            {
                Transform = go.transform,
                Renderer = sr,
                Active = false,
                BaseScale = go.transform.localScale,
            };
            _pool.Add(h);
            return h;
        }

        private static void Deactivate(HeartPickup h)
        {
            h.Active = false;
            h.Renderer.enabled = false;
        }
    }
}
