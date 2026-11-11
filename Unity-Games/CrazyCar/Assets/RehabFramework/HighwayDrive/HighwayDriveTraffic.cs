using System.Collections.Generic;
using UnityEngine;

namespace RehabFramework.HighwayDrive
{
    // ─────────────────────────────────────────────────────────────────────────
    //  HIGHWAY DRIVE TRAFFIC (HD-v1)
    //  Spawns, moves, recycles, and collision-tests the other cars. All
    //  traffic travels the same direction as the player and slower, so in the
    //  player's frame it drifts down the screen -- see HighwayDriveRoad's
    //  header for why the road underneath must scroll faster still for that to
    //  read as overtaking rather than as reversing.
    //
    //  Cars are pooled: a car that leaves the view is deactivated and reused,
    //  never destroyed. At default density the pool settles at well under
    //  twenty objects.
    //
    //  PASSABILITY. Two separate things keep the road from becoming an
    //  unwinnable wall, and it is worth being clear that the second one is the
    //  real guarantee:
    //
    //    1. MinFreeLanes (comfort). The spawner refuses to place a car if
    //       doing so would leave fewer than that many lanes clear in the spawn
    //       band. This keeps the road looking navigable and stops the obvious
    //       "solid rank of cars appears at the top of the screen" case.
    //
    //    2. Braking (the actual guarantee). Because full deceleration puts the
    //       player BELOW the slowest traffic speed (see HighwayDriveConfig's
    //       speed-budget note), any arrangement of traffic, however it came
    //       about, can be escaped by easing off -- the whole formation recedes
    //       instead of approaching. There is no configuration of this game
    //       that a patient cannot back out of, which matters a great deal when
    //       the failure state is a crash that ends the run.
    //
    //  Deliberately NOT here: lane-changing traffic, braking traffic, or any
    //  AI reaction to the player. Section-by-section this is the simplest
    //  thing that produces a real obstacle field, and unpredictable traffic
    //  behaviour would make the task about reacting to the game's decisions
    //  rather than about the patient's own steering and speed control.
    // ─────────────────────────────────────────────────────────────────────────

    public class HighwayDriveTraffic : MonoBehaviour
    {
        private const int SortingTrafficCar = 0;

        [Header("Dependencies")]
        [SerializeField] private HighwayDriveConfig _config;
        [SerializeField] private HighwayDriveRoad _road;

        [Header("Traffic vehicle art")]
        // THREE POOLS, NOT ONE, because size is a property of the CLASS rather
        // than of the sprite. The art pack draws every vehicle to fill its own
        // canvas, so a motorcycle crops to roughly the same pixel size as a
        // saloon -- relative scale simply is not in the artwork. Splitting by
        // class lets each be given a sensible body width (and, through its own
        // aspect, a sensible length), so bikes are genuinely narrow and trucks
        // genuinely large, and the spawn mix can be weighted.
        [Tooltip("Ordinary cars. The bulk of the traffic.")]
        [SerializeField] private Sprite[] _carSprites;
        [Tooltip("Trucks: wider and longer, so they block more of a lane.")]
        [SerializeField] private Sprite[] _truckSprites;
        [Tooltip("Motorcycles: narrow enough to leave a gap a car could not take.")]
        [SerializeField] private Sprite[] _motorcycleSprites;

        /// <summary>One pooled car. Speed is its own TRUE forward speed, not a
        /// speed relative to the player -- the relative motion is computed at
        /// move time so that easing off the throttle immediately changes how
        /// every existing car behaves, without touching any of them.</summary>
        private class TrafficCar
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public float TrueSpeedWorldPerS;
            public int Lane;
            public bool Active;
            /// <summary>This vehicle own world size, and therefore its own
            /// collision box. Per-vehicle rather than shared, because a
            /// motorcycle must not be hittable from a car width away.</summary>
            public Vector2 Size;
            /// <summary>Already reported through OnCarPassed this trip.</summary>
            public bool Passed;
        }

        /// <summary>Raised once per car when it passes the player's position
        /// (overtaken, or overtaking a slow player). Crazy Car plays a horn.</summary>
        public event System.Action OnCarPassed;

        private readonly List<TrafficCar> _pool = new List<TrafficCar>();
        private Sprite[] _fallbackSprites;
        private Vector2 _carSize;
        private float _spawnTimer;
        private Transform _root;
        private bool _built;

        /// <summary>Captured at build time and used for BOTH the sprite and
        /// the collision box, so the rectangle the patient sees is exactly the
        /// rectangle that can hit them even if the config is edited mid-run.</summary>
        public Vector2 CarSize => _carSize;

        private void Start()
        {
            _carSize = _config != null ? _config.TrafficCarSize : new Vector2(0.95f, 1.5f);
            _carSize = new Vector2(Mathf.Max(0.05f, _carSize.x), Mathf.Max(0.05f, _carSize.y));

            BuildCarSprites();

            _root = new GameObject("TrafficCars").transform;
            _root.SetParent(transform, false);

            _spawnTimer = 0f;
            _built = true;
        }

        /// <summary>Advance traffic by one frame.
        ///
        /// <paramref name="playerSpeedWorldPerS"/> is the player's TRUE speed;
        /// each car moves by (its own true speed - that) so the whole field
        /// responds to the throttle instantly.
        ///
        /// <paramref name="deltaTime"/> is an EFFECTIVE delta, not necessarily
        /// Time.deltaTime -- the controller scales it down during the post-crash
        /// coast so the road and the traffic slow to a stop together. Scaling
        /// one and not the other would make traffic appear to accelerate away
        /// at the exact moment the patient has been told to stop.</summary>
        public void Tick(float playerSpeedWorldPerS, float deltaTime, bool allowSpawn)
        {
            if (!_built || _road == null || !_road.IsBuilt) return;

            MoveCars(playerSpeedWorldPerS, deltaTime);

            if (!allowSpawn) return;
            _spawnTimer -= deltaTime;
            if (_spawnTimer > 0f) return;

            TrySpawn();
            float min = _config != null ? Mathf.Max(0.05f, _config.SpawnIntervalMinS) : 0.7f;
            float max = _config != null ? Mathf.Max(min, _config.SpawnIntervalMaxS) : 1.8f;
            _spawnTimer = Random.Range(min, max);
        }

        /// <summary>Deactivates every car. Called at the start of each run so a
        /// new run never begins with the wreckage of the previous one still on
        /// screen -- and, more importantly, never begins with a car already
        /// overlapping the player's start position.</summary>
        public void ClearAll()
        {
            for (int i = 0; i < _pool.Count; i++) Deactivate(_pool[i]);
            _spawnTimer = 0f;
        }

        /// <summary>Axis-aligned overlap test of the player's box against every
        /// active car. Returns the first car hit, so the caller can place a
        /// crash marker at a real position rather than guessing.</summary>
        public bool TryFindCollision(Vector2 playerCenter, Vector2 playerSize, out Vector2 contactPoint)
        {
            contactPoint = Vector2.zero;
            for (int i = 0; i < _pool.Count; i++)
            {
                TrafficCar car = _pool[i];
                if (!car.Active) continue;

                Vector3 p = car.Transform.localPosition;
                if (!HighwayDriveControlMath.RectsOverlap(
                        playerCenter.x, playerCenter.y, playerSize.x, playerSize.y,
                        p.x, p.y, car.Size.x, car.Size.y))
                    continue;

                contactPoint = new Vector2((playerCenter.x + p.x) * 0.5f, (playerCenter.y + p.y) * 0.5f);
                return true;
            }
            return false;
        }

        /// <summary>Builds the procedural fallback recolours, used only when
        /// no real art is wired. With real art nothing is prepared up front:
        /// the class, sprite and size are all chosen per spawn in TrySpawn.</summary>
        private void BuildCarSprites()
        {
            if (HasRealArt()) return;

            Color[] colors = _config != null ? _config.TrafficCarColors : null;
            if (colors == null || colors.Length == 0)
                colors = new[] { new Color(0.85f, 0.35f, 0.30f, 1f) };

            const int widthPx = 64;
            float ppu = widthPx / _carSize.x;
            int heightPx = Mathf.Max(2, Mathf.RoundToInt(_carSize.y * ppu));

            _fallbackSprites = new Sprite[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                _fallbackSprites[i] = HighwayDriveSpriteFactory.BuildCar(widthPx, heightPx, ppu, colors[i]);
        }

        private bool HasRealArt()
            => (_carSprites != null && _carSprites.Length > 0)
            || (_truckSprites != null && _truckSprites.Length > 0)
            || (_motorcycleSprites != null && _motorcycleSprites.Length > 0);

        /// <summary>Picks a vehicle class by weighted share, then a sprite from
        /// that pool, then sizes it: CLASS decides the body width, and the
        /// sprite own aspect decides the length, so nothing is ever distorted
        /// and every class keeps a believable footprint.</summary>
        private void ChooseVehicle(out Sprite sprite, out Vector2 size)
        {
            if (_config != null && _config.LegacyTrafficPool)
            {
                // Pre-2026-08-25: one flat pool, every vehicle rendered and
                // collided at the same shared size.
                sprite = _carSprites != null && _carSprites.Length > 0
                    ? _carSprites[Random.Range(0, _carSprites.Length)]
                    : (_fallbackSprites != null && _fallbackSprites.Length > 0
                        ? _fallbackSprites[Random.Range(0, _fallbackSprites.Length)]
                        : null);
                size = _carSize;
                return;
            }

            float truckShare = _config != null ? Mathf.Clamp01(_config.TrafficTruckShare) : 0.18f;
            float motoShare = _config != null ? Mathf.Clamp01(_config.TrafficMotorcycleShare) : 0.16f;

            Sprite[] pool;
            float width;
            float roll = Random.value;

            if (roll < truckShare && _truckSprites != null && _truckSprites.Length > 0)
            {
                pool = _truckSprites;
                width = _config != null ? _config.TrafficTruckWidthWorld : 1.08f;
            }
            else if (roll < truckShare + motoShare && _motorcycleSprites != null && _motorcycleSprites.Length > 0)
            {
                pool = _motorcycleSprites;
                width = _config != null ? _config.TrafficMotorcycleWidthWorld : 0.5f;
            }
            else if (_carSprites != null && _carSprites.Length > 0)
            {
                pool = _carSprites;
                width = _config != null ? _config.TrafficCarWidthWorld : 0.92f;
            }
            else
            {
                sprite = _fallbackSprites != null && _fallbackSprites.Length > 0
                    ? _fallbackSprites[Random.Range(0, _fallbackSprites.Length)]
                    : null;
                size = _carSize;
                return;
            }

            sprite = pool[Random.Range(0, pool.Length)];
            size = sprite != null && sprite.rect.width > 0f
                ? new Vector2(width, width * (sprite.rect.height / sprite.rect.width))
                : _carSize;
        }

        private void MoveCars(float playerSpeedWorldPerS, float deltaTime)
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                TrafficCar car = _pool[i];
                if (!car.Active) continue;

                float relativeSpeed = car.TrueSpeedWorldPerS - playerSpeedWorldPerS;
                Vector3 p = car.Transform.localPosition;
                float previousY = p.y;
                p.y += relativeSpeed * deltaTime;
                car.Transform.localPosition = p;

                float playerY = _config != null ? _config.PlayerAnchorY : -2.6f;
                if (!car.Passed && (previousY - playerY) * (p.y - playerY) <= 0f && previousY != p.y)
                {
                    car.Passed = true;
                    OnCarPassed?.Invoke();
                }

                // Both directions: normally cars fall off the bottom, but a
                // braking patient is slower than all traffic and watches it
                // stream off the top instead.
                if (p.y < _road.DespawnBelowY || p.y > _road.DespawnAboveY) Deactivate(car);
            }
        }

        private void TrySpawn()
        {
            int laneCount = _config != null ? Mathf.Max(2, _config.LaneCount) : 4;
            float roadHalfWidth = _config != null ? _config.RoadHalfWidth : 4.2f;
            int minFreeLanes = _config != null ? Mathf.Clamp(_config.MinFreeLanes, 0, laneCount - 1) : 1;

            // Collect the lanes with nothing already sitting in the spawn band.
            var freeLanes = new List<int>(laneCount);
            for (int lane = 0; lane < laneCount; lane++)
                if (!LaneBlockedAtSpawn(lane)) freeLanes.Add(lane);

            // Taking one of these would leave fewer than the guaranteed number
            // of clear lanes, so take none. See this class's header.
            if (freeLanes.Count <= minFreeLanes) return;

            int chosenLane = freeLanes[Random.Range(0, freeLanes.Count)];
            float speedMin = _config != null ? _config.TrafficSpeedMinWorldPerS : 5.5f;
            float speedMax = _config != null ? Mathf.Max(speedMin, _config.TrafficSpeedMaxWorldPerS) : 8f;

            TrafficCar car = TakeFromPool();
            car.Lane = chosenLane;
            car.TrueSpeedWorldPerS = Random.Range(speedMin, speedMax);
            car.Transform.localPosition = new Vector3(
                HighwayDriveControlMath.LaneCenterX(chosenLane, laneCount, roadHalfWidth),
                _road.SpawnY, 0f);

            ChooseVehicle(out Sprite sprite, out Vector2 size);
            car.Size = size;
            if (sprite != null)
            {
                car.Renderer.sprite = sprite;
                // Refitted every spawn, not once at creation: a pooled entry
                // comes back as a DIFFERENT vehicle next time, with its own
                // sprite, its own native pixel size and its own world size.
                HighwayDriveSpriteFactory.FitToWorldSize(car.Renderer, size);
            }

            car.Renderer.enabled = true;
            car.Active = true;
            car.Passed = false;
        }

        /// <summary>Whether lane <paramref name="lane"/> already has a car close
        /// enough to the spawn point that adding another would look like (or
        /// become) an unbroken wall. Tested as absolute distance rather than
        /// "is there a car above": a braking player pushes traffic UP past the
        /// spawn line, and dropping a new car underneath one that is on its way
        /// out is just as bad as dropping one on top of it.</summary>
        private bool LaneBlockedAtSpawn(int lane)
        {
            // Measured against the LONGEST vehicle that could occupy the lane
            // rather than a nominal car, so a truck cannot be spawned nose to
            // tail with the one in front of it.
            float longest = _carSize.y;
            for (int i = 0; i < _pool.Count; i++)
                if (_pool[i].Active && _pool[i].Size.y > longest) longest = _pool[i].Size.y;
            float gap = _config != null ? Mathf.Max(longest, _config.MinLaneGapWorld) : 6f;

            for (int i = 0; i < _pool.Count; i++)
            {
                TrafficCar car = _pool[i];
                if (!car.Active || car.Lane != lane) continue;
                if (Mathf.Abs(car.Transform.localPosition.y - _road.SpawnY) < gap) return true;
            }
            return false;
        }

        private TrafficCar TakeFromPool()
        {
            for (int i = 0; i < _pool.Count; i++)
                if (!_pool[i].Active) return _pool[i];

            var go = new GameObject($"TrafficCar_{_pool.Count}");
            go.transform.SetParent(_root, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = SortingTrafficCar;

            var car = new TrafficCar { Transform = go.transform, Renderer = sr, Active = false };
            _pool.Add(car);
            return car;
        }

        private static void Deactivate(TrafficCar car)
        {
            car.Active = false;
            car.Renderer.enabled = false;
        }
    }
}
