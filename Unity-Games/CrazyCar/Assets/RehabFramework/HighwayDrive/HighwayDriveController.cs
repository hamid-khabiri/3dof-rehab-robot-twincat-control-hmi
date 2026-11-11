using System;
using UnityEngine;

namespace RehabFramework.HighwayDrive
{
    // ─────────────────────────────────────────────────────────────────────────
    //  HIGHWAY DRIVE CONTROLLER (HD-v1)
    //  Owns the assessment state machine, the player's car, the speed
    //  integration, collision, and the resistance command. Road, traffic,
    //  hearts and landmarks are driven from here every frame; none of them
    //  knows about state.
    //
    //  RECOVERY IS GATED ON RETURNING TO NEUTRAL, NOT ON WAITING (Simon's
    //  spec, 2026-08-24). A collision does not start a countdown: it puts the
    //  patient into Recentering, shows the cyan target, and holds there until
    //  the end effector is actually back inside the neutral tolerance and has
    //  STAYED there briefly. Only then does Ready flash and the road resume.
    //  This turns the dead time after a crash from a pause into a task, which
    //  is the whole point -- returning to neutral is itself a rehabilitation
    //  movement worth demanding.
    //
    //  THE TRIAL CLOCK MEASURES ONE THING: elapsed assessment time. It does
    //  NOT reset on collision. It pauses while the patient is recentering and
    //  resumes when they drive again, so the number on screen always answers
    //  "how far through the assessment am I" and never silently turns into
    //  "how long since my last crash". Showing 0:00 after every collision
    //  would imply a new trial had begun when it had not.
    //
    //  RESISTANCE IS A FORCE ON A PATIENT'S ARM, and this class is the only
    //  thing that decides when it is applied. The rule, without exception:
    //  resistance is commanded ONLY while actually driving. Tutorial,
    //  collision, recentering, ready, paused, finished, component disabled,
    //  scene unloaded -- all call ClearResistance().
    //
    //  THE POST-CRASH COAST scales the road scroll and the traffic motion by
    //  the SAME factor, by passing both an effective delta time rather than by
    //  slowing one of them directly. Scaling the world but not the traffic
    //  makes the other cars appear to accelerate away at the exact moment
    //  everything is supposed to be stopping.
    //
    //  HEARTS ARE SESSION-SCOPED. TrialElapsed and the run reset on their own
    //  schedules; CurrentHearts must not follow, or losing one would become
    //  invisible the moment the next run began. Only a collision (-1) and a
    //  pickup (+1) ever change it.
    //
    //  CRAZY CAR adds four switches, all off for Highway Drive: Held (a menu
    //  or popup owns the screen -- see CrazyCarSessionFlow), lane steering
    //  (HighwayDriveInput.TargetLane instead of the continuous mapping), the
    //  speed ramp (cruise speed steps up as the drive goes on), and
    //  EndWhenHeartsRunOut (the last lost heart finishes the game).
    // ─────────────────────────────────────────────────────────────────────────

    public enum HighwayDriveState
    {
        /// <summary>The planar-control cross, shown briefly before gameplay
        /// and then destroyed. No traffic, no clock, no resistance.</summary>
        Tutorial,
        Driving,
        /// <summary>Impact registered; the world is coasting to a stop.</summary>
        Collision,
        /// <summary>Held until the patient returns the end effector to neutral
        /// and holds it there. The gate is the movement, not a timer.</summary>
        Recentering,
        /// <summary>Neutral achieved; a brief confirmation before resuming.</summary>
        Ready,
        /// <summary>The robot controller reports itself not awake. TwinCAT
        /// handshake only -- there is no self-pausing safety heuristic in this
        /// instrument, so nothing else can put the game here, and it clears
        /// itself when the robot wakes.</summary>
        Paused,
        /// <summary>The assessment is over. All gameplay is stopped BEFORE the
        /// completion message appears -- no traffic keeps spawning behind
        /// it.</summary>
        Finished,
    }

    public class HighwayDriveController : MonoBehaviour
    {
        private const int SortingPlayerCar = 5;
        private const int SortingPlayerGlow = 4;
        private const int SortingImpactBurst = 8;

        [Header("Dependencies")]
        [SerializeField] private HighwayDriveConfig _config;
        [SerializeField] private HighwayDriveRoad _road;
        [SerializeField] private HighwayDriveTraffic _traffic;
        [SerializeField] private HighwayDriveInput _input;
        [SerializeField] private HighwayDriveInstructionUI _instructionUI;
        [Tooltip("Optional -- if unassigned, hearts never spawn; everything else is unchanged.")]
        [SerializeField] private HighwayDriveHearts _hearts;
        [Tooltip("Optional -- sparse roadside landmarks.")]
        [SerializeField] private HighwayDriveLandmarks _landmarks;
        [Tooltip("Optional -- raw trajectory logging, independent of the graphics.")]
        [SerializeField] private HighwayDriveDataLogger _logger;

        [Header("Player car art")]
        [SerializeField] private Sprite _playerCarSprite;
        [SerializeField] private Sprite _haloSprite;
        [SerializeField] private Sprite _impactBurstSprite;

        [Header("Collision feedback")]
        [Tooltip("Warning colour driven onto the halo during collision and recentering, so the patient's " +
                 "own car carries the alert rather than needing a separate overlay.")]
        [SerializeField] private Color _warningColor = new Color(1f, 0.42f, 0.18f, 1f);
        [SerializeField] private float _impactBurstSeconds = 0.55f;
        [SerializeField] private float _impactBurstWorldSize = 2.2f;

        public HighwayDriveState State { get; private set; } = HighwayDriveState.Tutorial;
        public event Action<HighwayDriveState> OnStateChanged;
        public event Action<Vector2> OnCrash;
        /// <summary>The speed ramp took another step (argument = steps so far).</summary>
        public event Action<int> OnSpeedRamp;
        /// <summary>A heart pickup was driven through.</summary>
        public event Action OnHeartCollected;

        public float DistanceTravelled => _road != null ? _road.DistanceTravelled : 0f;
        public float CurrentSpeedWorldPerS { get; private set; }

        /// <summary>Elapsed assessment time. Advances only while driving --
        /// see this class's header on why it never resets at a collision.</summary>
        public float TrialElapsedSeconds { get; private set; }

        /// <summary>Hearts remaining. SESSION-scoped; only a collision and a
        /// pickup change it.</summary>
        public int CurrentHearts { get; private set; }

        /// <summary>The car's mapped lateral world position, for logging.</summary>
        public float MappedCarX { get; private set; }

        /// <summary>Normalised -1..+1 speed command actually in effect.</summary>
        public float NormalizedSpeed { get; private set; }

        /// <summary>A menu or popup owns the screen: the state machine, the
        /// trial clock and lane changes are frozen, resistance is cleared, and
        /// the road idles underneath. Crazy Car only.</summary>
        public bool Held { get; set; }

        /// <summary>The player car and the road exist; StartRun is safe.</summary>
        public bool IsInitialized => _initialized;

        /// <summary>How many speed-ramp steps are in effect right now.</summary>
        public int SpeedRampStep => CurrentRampStep();

        private bool _initialized;
        private bool _snapCarToLane;
        private int _announcedRampStep;
        private Transform _playerCar;
        private SpriteRenderer _playerRenderer;
        private SpriteRenderer _playerGlowRenderer;
        private Vector2 _playerCarSize;
        private Vector2 _collisionBoxSize;
        private Color _haloRestingColor;

        private SpriteRenderer _burstRenderer;
        private float _burstTimer = -1f;

        private float _stateElapsed;
        private float _neutralHeldSeconds;
        private float _controlledDrivingSeconds;
        private float _praiseVisibleSeconds = -1f;
        private float _previousLateral;
        private float _smoothedCarX;

        private void Update()
        {
            if (!_initialized && !TryInitialize()) return;

            // Polled in every state, including Paused and Recentering -- the
            // recentering gate depends on reading the live end-effector
            // position, so input must never be frozen.
            _input.LaneChangesEnabled = !Held && State == HighwayDriveState.Driving;
            _input.Poll();

            if (Held)
            {
                UpdateHeld();
                return;
            }

            bool robotAsleep = !_input.IsRobotAwake;
            bool sessionOver = State == HighwayDriveState.Finished;

            if (!sessionOver && robotAsleep)
            {
                if (State != HighwayDriveState.Paused) EnterPaused();
                UpdatePlayerGlowPulse();
                UpdatePlayerCarPosition();
                return;
            }
            if (State == HighwayDriveState.Paused)
            {
                EnterDriving();
                return;
            }

            _stateElapsed += Time.deltaTime;

            switch (State)
            {
                case HighwayDriveState.Tutorial: UpdateTutorial(); break;
                case HighwayDriveState.Driving: UpdateDriving(); break;
                case HighwayDriveState.Collision: UpdateCollision(); break;
                case HighwayDriveState.Recentering: UpdateRecentering(); break;
                case HighwayDriveState.Ready: UpdateReady(); break;
                case HighwayDriveState.Finished: UpdateFinished(); break;
            }

            UpdateImpactBurst();
            UpdatePlayerGlowPulse();
            UpdatePlayerCarPosition();
            _instructionUI?.SetTrialClockSeconds(TrialElapsedSeconds);
            _logger?.Record(this, _input, State);
        }

        private void OnDisable()
        {
            _input?.ClearResistance();
            _input?.SetGameAwake(false);
            _logger?.Flush();
        }

        private bool TryInitialize()
        {
            if (_road == null || _traffic == null || _input == null)
            {
                Debug.LogError("[HighwayDriveController] Assign _road, _traffic, and _input in the Inspector.");
                enabled = false;
                return false;
            }
            if (!_road.IsBuilt) return false;

            BuildPlayerCar();
            BuildImpactBurst();

            CurrentHearts = MaxHearts();
            _instructionUI?.SetHeartsDisplay(CurrentHearts);

            CurrentSpeedWorldPerS = CruiseSpeed();
            _initialized = true;
            EnterTutorial();
            return true;
        }

        // ── Therapist-facing API ─────────────────────────────────────────

        /// <summary>Restart the assessment from the beginning: clock back to
        /// zero, traffic cleared, hearts refilled.</summary>
        public void StartRun()
        {
            TrialElapsedSeconds = 0f;
            CurrentHearts = MaxHearts();
            _instructionUI?.SetHeartsDisplay(CurrentHearts);
            _traffic.ClearAll();
            _hearts?.ClearAll();
            _landmarks?.ClearAll();
            _road.ResetScroll();
            _input.ResetLane();
            _snapCarToLane = true;
            _announcedRampStep = 0;
            _controlledDrivingSeconds = 0f;
            _praiseVisibleSeconds = -1f;
            CurrentSpeedWorldPerS = CruiseSpeed();
            EnterDriving();
        }

        public void EndSession() => EnterFinished();

        /// <summary>Swaps the player's vehicle (Crazy Car: Taxi / Van /
        /// Truck). <paramref name="size"/> is the new collision box, exactly as
        /// PlayerCarSize. A null sprite falls back to the procedural car in
        /// <paramref name="fallbackColor"/>. Safe before initialisation -- the
        /// car is then simply built with these values.</summary>
        public void SetPlayerVehicle(Sprite sprite, Vector2 size, Color fallbackColor)
        {
            _playerCarSprite = sprite;
            if (_config != null)
            {
                _config.PlayerCarSize = size;
                _config.PlayerCarColor = fallbackColor;
            }
            if (_playerCar == null) return;

            Destroy(_playerCar.gameObject);
            BuildPlayerCar();
            UpdatePlayerCarPosition();
        }

        // ── Held (Crazy Car menus and popups) ────────────────────────────

        private void UpdateHeld()
        {
            _input.ClearResistance();
            _instructionUI?.SetSpeedIndicator(0f);

            // The road keeps rolling gently behind the main menu so the scene
            // is alive. Under the pause popup Time.timeScale is 0, so nothing
            // moves at all.
            AdvanceWorld(CruiseSpeed() * 0.5f, Time.deltaTime, allowSpawn: false);
            UpdateImpactBurst();
            UpdatePlayerGlowPulse();
            UpdatePlayerCarPosition();
        }

        // ── Tutorial ─────────────────────────────────────────────────────

        private void EnterTutorial()
        {
            ChangeState(HighwayDriveState.Tutorial);
            _input.SetGameAwake(true);
            _input.ClearResistance();
            _instructionUI?.ShowTutorial(true);
            _instructionUI?.ShowFeedback(string.Empty);
            _instructionUI?.SetSpeedIndicator(0f);
        }

        private void UpdateTutorial()
        {
            // The road already scrolls gently underneath, so the patient's
            // first impression is of a road they are on rather than a still
            // image that lurches into motion. No traffic and no resistance.
            AdvanceWorld(CruiseSpeed() * 0.5f, Time.deltaTime, allowSpawn: false);
            _input.ClearResistance();

            float seconds = _config != null ? Mathf.Max(0f, _config.TutorialSeconds) : 2.5f;
            if (_stateElapsed >= seconds)
            {
                _instructionUI?.DisposeTutorial();
                EnterDriving();
            }
        }

        // ── Driving ──────────────────────────────────────────────────────

        private void EnterDriving()
        {
            ChangeState(HighwayDriveState.Driving);
            _input.SetGameAwake(true);
            _instructionUI?.ShowTutorial(false);
            _instructionUI?.ShowCenterTarget(false);
            _instructionUI?.ShowKeepClear();
            _instructionUI?.ShowFeedback(string.Empty);
            SetHaloWarning(false);
            _neutralHeldSeconds = 0f;
        }

        private void UpdateDriving()
        {
            float dt = Time.deltaTime;
            TrialElapsedSeconds += dt;

            float cruise = CruiseSpeed();
            float swing = _config != null ? _config.SpeedSwingWorldPerS : 7f;
            float minSpeed = _config != null ? _config.MinSpeedWorldPerS : 2.5f;
            float speedGain = _config != null ? _config.SpeedGain : 1f;

            float demanded = HighwayDriveControlMath.PlayerSpeedWorldPerS(
                _input.SpeedInput * speedGain, cruise, swing, minSpeed);

            float responseTime = _config != null ? Mathf.Max(0f, _config.SpeedResponseTimeS) : 0.15f;
            CurrentSpeedWorldPerS = responseTime <= 0f
                ? demanded
                : Mathf.Lerp(CurrentSpeedWorldPerS, demanded, 1f - Mathf.Exp(-dt / responseTime));

            NormalizedSpeed = swing > 0f
                ? HighwayDriveControlMath.Clamp((CurrentSpeedWorldPerS - cruise) / swing, -1f, 1f)
                : 0f;

            AdvanceWorld(CurrentSpeedWorldPerS, dt, allowSpawn: true);
            _input.ApplyResistance();
            _instructionUI?.SetSpeedIndicator(NormalizedSpeed);

            AnnounceSpeedRamp();
            UpdateControlledDrivingPraise(dt);
            CheckCollision();
            if (State != HighwayDriveState.Driving) return;
            CheckHeartPickup();

            float duration = _config != null ? _config.TrialDurationSeconds : 180f;
            if (duration > 0f && TrialElapsedSeconds >= duration) EnterFinished();
        }

        /// <summary>Praise is earned, not decorative: it needs a sustained
        /// stretch of driving with no collision AND no violent steering
        /// correction. Any large, fast lateral change resets the clock, so the
        /// badge means "you held this well" rather than "time passed".</summary>
        /// <summary>Shows "Speeding up!" through the praise line each time the
        /// speed ramp takes another step, so the change is not a surprise.</summary>
        private void AnnounceSpeedRamp()
        {
            int step = CurrentRampStep();
            if (step <= _announcedRampStep) return;
            _announcedRampStep = step;
            _praiseVisibleSeconds = 0f;
            _instructionUI?.ShowFeedback(HighwayDrivePatientStrings.SpeedingUp);
            OnSpeedRamp?.Invoke(step);
        }

        private void UpdateControlledDrivingPraise(float dt)
        {
            float rateLimit = _config != null ? _config.ExcessiveSteeringRatePerS : 2.5f;
            // Lane changes ARE the task in lane steering, so a quick one is
            // never an "excessive correction".
            float rate = dt > 0f && !_input.UsesLaneSteering
                ? Mathf.Abs(_input.LateralInput - _previousLateral) / dt
                : 0f;
            _previousLateral = _input.LateralInput;

            if (rate > rateLimit) _controlledDrivingSeconds = 0f;
            else _controlledDrivingSeconds += dt;

            if (_praiseVisibleSeconds >= 0f)
            {
                _praiseVisibleSeconds += dt;
                float visible = _config != null ? Mathf.Max(0.1f, _config.NiceControlVisibleSeconds) : 1f;
                _instructionUI?.SetFeedbackAlpha(1f - Mathf.Clamp01(_praiseVisibleSeconds / visible));
                if (_praiseVisibleSeconds >= visible)
                {
                    _praiseVisibleSeconds = -1f;
                    _instructionUI?.ShowFeedback(string.Empty);
                }
                return;
            }

            float threshold = _config != null ? Mathf.Max(1f, _config.NiceControlAfterSeconds) : 8f;
            if (_controlledDrivingSeconds < threshold) return;

            _controlledDrivingSeconds = 0f;
            _praiseVisibleSeconds = 0f;
            _instructionUI?.ShowFeedback(PickPraise());
        }

        private string _lastPraise;

        /// <summary>"Nice control!" for Highway Drive; for Crazy Car a random
        /// sentence from the group that matches how long the drive has gone
        /// on, never the same sentence twice in a row.</summary>
        private string PickPraise()
        {
            if (_config == null || !_config.MotivationalPraise) return HighwayDrivePatientStrings.NiceControl;

            float groupSeconds = Mathf.Max(1f, _config.PraiseGroupSeconds);
            int group = Mathf.FloorToInt(TrialElapsedSeconds / groupSeconds);
            string[] pool = group <= 0 ? HighwayDrivePatientStrings.PraiseGroup1
                          : group == 1 ? HighwayDrivePatientStrings.PraiseGroup2
                          : HighwayDrivePatientStrings.PraiseGroup3;

            string pick = pool[UnityEngine.Random.Range(0, pool.Length)];
            if (pool.Length > 1 && pick == _lastPraise)
                pick = pool[(Array.IndexOf(pool, pick) + 1) % pool.Length];
            _lastPraise = pick;
            return pick;
        }

        private void CheckCollision()
        {
            Vector2 center = _playerCar.localPosition;
            if (!_traffic.TryFindCollision(center, _collisionBoxSize, out Vector2 contact)) return;
            EnterCollision(contact);
        }

        private void CheckHeartPickup()
        {
            if (_hearts == null) return;
            Vector2 center = _playerCar.localPosition;
            if (!_hearts.TryCollect(center, _collisionBoxSize, out _)) return;
            OnHeartCollected?.Invoke();

            int max = MaxHearts();
            if (CurrentHearts >= max) return;

            CurrentHearts++;
            _instructionUI?.SetHeartsDisplay(CurrentHearts);
        }

        // ── Collision ────────────────────────────────────────────────────

        private void EnterCollision(Vector2 contactPoint)
        {
            ChangeState(HighwayDriveState.Collision);

            _input.ClearResistance();
            _instructionUI?.SetSpeedIndicator(0f);
            SetHaloWarning(true);
            TriggerImpactBurst(contactPoint);

            // Losing a heart has to be immediately legible, or the
            // participant cannot connect the collision to the cost. The UI
            // pulses the counter and flashes the broken-heart icon on the slot
            // that was just spent.
            int lostIndex = CurrentHearts - 1;
            CurrentHearts = Mathf.Max(0, CurrentHearts - 1);
            _instructionUI?.SetHeartsDisplay(CurrentHearts);
            if (lostIndex >= 0) _instructionUI?.FlashHeartLost(lostIndex);

            _controlledDrivingSeconds = 0f;
            _praiseVisibleSeconds = -1f;
            _logger?.RecordCollision(TrialElapsedSeconds, contactPoint);
            OnCrash?.Invoke(contactPoint);
        }

        private void UpdateCollision()
        {
            float stopSeconds = _config != null ? Mathf.Max(0.01f, _config.CrashStopSeconds) : 0.35f;
            float coast = 1f - Mathf.Clamp01(_stateElapsed / stopSeconds);
            coast *= coast;
            AdvanceWorld(CurrentSpeedWorldPerS, Time.deltaTime * coast, allowSpawn: false);
            _input.ClearResistance();

            if (_stateElapsed < stopSeconds) return;

            bool outOfHearts = _config != null && _config.EndWhenHeartsRunOut && CurrentHearts <= 0;
            if (outOfHearts) EnterFinished();
            else if (_config != null && _config.SkipRecentering)
            {
                // Same road clearing as EnterRecentering, so the car does not
                // resume still overlapping what it hit.
                _traffic.ClearAll();
                EnterReady();
            }
            else EnterRecentering();
        }

        // ── Recentering ──────────────────────────────────────────────────

        private void EnterRecentering()
        {
            ChangeState(HighwayDriveState.Recentering);

            // CLEAR THE ROAD BEFORE THE PATIENT RESUMES. Without this the car
            // is still overlapping whatever it just hit when driving restarts,
            // so CheckCollision fires again on the very first frame -- and
            // again, and again: an unrecoverable crash loop that costs a heart
            // each time and can never be steered out of, because recentering
            // deliberately parks the end effector at neutral, which is the
            // middle lane. Cleared here rather than at impact so the cars stay
            // visible through the coast, where they are the explanation for
            // what just happened.
            _traffic.ClearAll();

            _neutralHeldSeconds = 0f;
            _instructionUI?.ShowFeedback(HighwayDrivePatientStrings.CollisionReturnToCenter);
            _instructionUI?.ShowCenterTarget(true);
        }

        private void UpdateRecentering()
        {
            // The world is stopped and the trial clock is NOT advancing -- the
            // patient is not driving, so this time does not count against the
            // assessment.
            _input.ClearResistance();

            float latTol = _config != null ? _config.NeutralToleranceLateral : 0.12f;
            float fwdTol = _config != null ? _config.NeutralToleranceForward : 0.15f;
            bool neutral = _input.IsAtNeutral(latTol, fwdTol);

            // Held, not merely touched: a patient sweeping through neutral on
            // the way somewhere else has not recentred.
            _neutralHeldSeconds = neutral ? _neutralHeldSeconds + Time.deltaTime : 0f;

            float hold = _config != null ? Mathf.Max(0f, _config.NeutralHoldSeconds) : 0.4f;
            if (_neutralHeldSeconds >= hold) EnterReady();

            // Legacy timer fallback, off by default -- only for a patient who
            // genuinely cannot reach the neutral gate.
            float autoRestart = _config != null ? _config.AutoRestartSeconds : 0f;
            if (autoRestart > 0f && _stateElapsed >= autoRestart) EnterReady();
        }

        // ── Ready ────────────────────────────────────────────────────────

        private void EnterReady()
        {
            ChangeState(HighwayDriveState.Ready);
            _instructionUI?.ShowCenterTarget(false);
            _instructionUI?.ShowFeedback(HighwayDrivePatientStrings.Ready);
            SetHaloWarning(false);
            CurrentSpeedWorldPerS = CruiseSpeed();
            NormalizedSpeed = 0f;
        }

        private void UpdateReady()
        {
            _input.ClearResistance();
            float seconds = _config != null ? Mathf.Max(0f, _config.ReadySeconds) : 0.6f;
            if (_stateElapsed >= seconds) EnterDriving();
        }

        // ── Paused / Finished ────────────────────────────────────────────

        private void EnterPaused()
        {
            ChangeState(HighwayDriveState.Paused);
            _input.ClearResistance();
            _input.Neutralize();
            _input.SetGameAwake(false);
            _instructionUI?.ShowPaused();
            _instructionUI?.SetSpeedIndicator(0f);
        }

        /// <summary>Stops ALL gameplay first, then shows the completion
        /// message -- traffic must not keep spawning behind it.</summary>
        private void EnterFinished()
        {
            _traffic.ClearAll();
            _hearts?.ClearAll();
            _landmarks?.ClearAll();

            ChangeState(HighwayDriveState.Finished);
            _input.ClearResistance();
            _input.SetGameAwake(false);
            _instructionUI?.ShowCenterTarget(false);
            _instructionUI?.SetSpeedIndicator(0f);
            _instructionUI?.ShowFeedback(HighwayDrivePatientStrings.AssessmentComplete);
            SetHaloWarning(false);
            _logger?.Flush();
        }

        private void UpdateFinished()
        {
            _input.ClearResistance();
        }

        // ── Shared ───────────────────────────────────────────────────────

        private void AdvanceWorld(float speedWorldPerS, float deltaTime, bool allowSpawn)
        {
            if (deltaTime <= 0f) return;
            _road.Advance(speedWorldPerS, deltaTime);
            _traffic.Tick(speedWorldPerS, deltaTime, allowSpawn);
            _hearts?.Tick(speedWorldPerS, deltaTime, allowSpawn, CurrentHearts, MappedCarX);
            _landmarks?.Tick(speedWorldPerS, deltaTime, allowSpawn);
        }

        private void UpdatePlayerCarPosition()
        {
            if (_playerCar == null) return;

            float roadHalfWidth = _config != null ? _config.RoadHalfWidth : 3.9f;
            float margin = _config != null ? _config.LateralSafetyMarginWorld : 0.12f;
            float range = _config != null ? _config.LateralWorkspaceRange : 1f;
            float gain = _config != null ? _config.SteeringGain : 1f;
            float anchorY = _config != null ? _config.PlayerAnchorY : -2.6f;
            float shift = _config != null ? _config.PlayerForwardShiftWorld : 0.35f;

            if (_input.UsesLaneSteering)
            {
                // Slides to the target lane's centre at one lane per
                // LaneChangeSeconds. Time.deltaTime is 0 under the pause popup,
                // so the car holds still there too.
                int laneCount = Mathf.Max(1, _config.LaneCount);
                float laneX = HighwayDriveControlMath.LaneCenterX(_input.TargetLane, laneCount, roadHalfWidth);
                float laneSeconds = _config.LaneChangeSeconds;
                if (_snapCarToLane || laneSeconds <= 0f) _smoothedCarX = laneX;
                else
                {
                    float laneWidth = HighwayDriveControlMath.LaneWidth(roadHalfWidth, laneCount);
                    _smoothedCarX = Mathf.MoveTowards(_smoothedCarX, laneX, laneWidth / laneSeconds * Time.deltaTime);
                }
                _snapCarToLane = false;
            }
            else
            {
                _smoothedCarX = HighwayDriveControlMath.MapLateralToRoad(
                    _input.LateralInput, roadHalfWidth, _playerCarSize.x * 0.5f, margin, range, gain);
            }
            MappedCarX = _smoothedCarX;

            float y = HighwayDriveControlMath.PlayerForwardWorldY(NormalizedSpeed, anchorY, shift);
            _playerCar.localPosition = new Vector3(_smoothedCarX, y, 0f);
        }

        /// <summary>The halo is a TRANSIENT event indicator. During normal
        /// driving it is switched off entirely: the cyan player car already
        /// stands out against red, orange, green, white and purple traffic, so
        /// a standing ring is clutter, and a bright circle around the car in a
        /// game whose failure state is collision actively suggests a collision
        /// boundary that does not exist.
        ///
        /// It earns its place on the events where a ring genuinely means
        /// something: warning orange through collision and recentering, theme
        /// cyan on Ready to confirm recovery.</summary>
        private void UpdatePlayerGlowPulse()
        {
            if (_playerGlowRenderer == null) return;

            switch (State)
            {
                case HighwayDriveState.Collision:
                case HighwayDriveState.Recentering:
                {
                    // Steady and bright, not breathing -- this is an alert, and
                    // a pulsing alert reads as decoration.
                    Color w = _warningColor;
                    w.a = 0.95f;
                    _playerGlowRenderer.enabled = true;
                    _playerGlowRenderer.color = w;
                    return;
                }

                case HighwayDriveState.Ready:
                {
                    Color ok = _haloRestingColor;
                    ok.a = 0.9f;
                    _playerGlowRenderer.enabled = true;
                    _playerGlowRenderer.color = ok;
                    return;
                }
            }

            bool permanent = _config != null && _config.ShowPlayerHighlight;
            _playerGlowRenderer.enabled = permanent;
            if (!permanent) return;

            float hz = _config != null ? Mathf.Max(0.01f, _config.PlayerHighlightPulseHz) : 0.5f;
            float minAlpha = _config != null ? _config.PlayerHighlightMinAlpha : 0.35f;
            float maxAlpha = _config != null ? _config.PlayerHighlightMaxAlpha : 0.75f;

            float t = (Mathf.Sin(Time.time * hz * Mathf.PI * 2f) + 1f) * 0.5f;
            Color c = _haloRestingColor;
            c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
            _playerGlowRenderer.color = c;
        }

        private void SetHaloWarning(bool warning)
        {
            if (_playerGlowRenderer == null) return;
            _playerGlowRenderer.color = warning ? _warningColor : _haloRestingColor;
        }

        // ── Player car + impact burst ────────────────────────────────────

        /// <summary>Root + two children. The Car child is what gets stretched
        /// to the configured size; the Halo must NOT inherit that stretch or a
        /// round ring would render as an ellipse, so it is a sibling under an
        /// unscaled root rather than a child of the car.
        ///
        /// The halo is sized just over the car (PlayerHighlightDiameterFactor,
        /// ~1.18) so it reads as identification and not as a collision
        /// boundary -- a large disc around the car actively misleads in a game
        /// whose failure state is collision.</summary>
        private void BuildPlayerCar()
        {
            _playerCarSize = _config != null ? _config.PlayerCarSize : new Vector2(0.92f, 1.52f);
            _playerCarSize = new Vector2(Mathf.Max(0.05f, _playerCarSize.x), Mathf.Max(0.05f, _playerCarSize.y));

            // The collision box may be slightly SMALLER than the art, never
            // larger, so the patient never sees a collision that visually did
            // not happen.
            float forgive = _config != null ? Mathf.Clamp01(_config.CollisionForgiveness) : 0.12f;
            _collisionBoxSize = _playerCarSize * (1f - forgive);

            var root = new GameObject("PlayerCar");
            root.transform.SetParent(transform, false);

            // The halo is built whatever ShowPlayerHighlight says, because it
            // is now an EVENT indicator rather than a standing marker: it is
            // hidden during normal driving and shown only for collision,
            // recentering and Ready. ShowPlayerHighlight decides only whether
            // it ALSO stays on while driving, and defaults to off -- the cyan
            // player car is already unmistakable against the traffic, and a
            // permanent ring reads as a collision boundary.
            {
                Color theme = _config != null ? _config.ThemeColor : new Color(0.35f, 0.78f, 0.92f, 1f);
                float factor = _config != null ? Mathf.Max(1f, _config.PlayerHighlightDiameterFactor) : 1.18f;

                var glowGO = new GameObject("Halo");
                glowGO.transform.SetParent(root.transform, false);
                _playerGlowRenderer = glowGO.AddComponent<SpriteRenderer>();
                _playerGlowRenderer.sprite = _haloSprite != null
                    ? _haloSprite
                    : HighwayDriveSpriteFactory.BuildDisc(96, 96f / (_playerCarSize.magnitude * factor), theme);
                _playerGlowRenderer.sortingOrder = SortingPlayerGlow;
                _playerGlowRenderer.color = _haloSprite != null ? Color.white : theme;
                _haloRestingColor = _playerGlowRenderer.color;
                HighwayDriveSpriteFactory.FitToWorldSize(_playerGlowRenderer, _playerCarSize * factor);
            }

            var carGO = new GameObject("Car");
            carGO.transform.SetParent(root.transform, false);

            const int widthPx = 64;
            float ppu = widthPx / _playerCarSize.x;
            int heightPx = Mathf.Max(2, Mathf.RoundToInt(_playerCarSize.y * ppu));

            _playerRenderer = carGO.AddComponent<SpriteRenderer>();
            _playerRenderer.sprite = _playerCarSprite != null
                ? _playerCarSprite
                : HighwayDriveSpriteFactory.BuildCar(widthPx, heightPx, ppu,
                    _config != null ? _config.PlayerCarColor : new Color(0.25f, 0.70f, 0.95f, 1f));
            _playerRenderer.sortingOrder = SortingPlayerCar;
            HighwayDriveSpriteFactory.FitToWorldSize(_playerRenderer, _playerCarSize);

            _playerCar = root.transform;
        }

        private void BuildImpactBurst()
        {
            var go = new GameObject("ImpactBurst");
            go.transform.SetParent(transform, false);
            _burstRenderer = go.AddComponent<SpriteRenderer>();
            _burstRenderer.sortingOrder = SortingImpactBurst;
            if (_impactBurstSprite != null)
            {
                _burstRenderer.sprite = _impactBurstSprite;
                HighwayDriveSpriteFactory.FitToWorldSize(_burstRenderer,
                    new Vector2(_impactBurstWorldSize, _impactBurstWorldSize));
            }
            _burstRenderer.enabled = false;
        }

        private void TriggerImpactBurst(Vector2 contactPoint)
        {
            if (_burstRenderer == null || _burstRenderer.sprite == null) return;
            _burstRenderer.transform.localPosition = new Vector3(contactPoint.x, contactPoint.y, 0f);
            _burstRenderer.enabled = true;
            _burstTimer = 0f;
        }

        private void UpdateImpactBurst()
        {
            if (_burstTimer < 0f || _burstRenderer == null) return;
            _burstTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_burstTimer / Mathf.Max(0.05f, _impactBurstSeconds));
            Color c = Color.white;
            c.a = 1f - t;
            _burstRenderer.color = c;
            _burstRenderer.transform.localScale *= 1f + Time.deltaTime * 0.6f;
            if (t < 1f) return;

            _burstRenderer.enabled = false;
            _burstTimer = -1f;
            HighwayDriveSpriteFactory.FitToWorldSize(_burstRenderer,
                new Vector2(_impactBurstWorldSize, _impactBurstWorldSize));
        }

        private float CruiseSpeed()
        {
            float cruise = _config != null ? _config.CruiseSpeedWorldPerS : 11f;
            float step = _config != null ? _config.SpeedRampStepWorldPerS : 0f;
            return cruise + CurrentRampStep() * step;
        }

        /// <summary>Speed-ramp steps earned so far: one per
        /// SpeedRampIntervalSeconds of driving, capped. Follows the trial clock,
        /// so recentering after a crash does not count.</summary>
        private int CurrentRampStep()
        {
            if (_config == null || _config.SpeedRampIntervalSeconds <= 0f) return 0;
            int steps = Mathf.FloorToInt(TrialElapsedSeconds / _config.SpeedRampIntervalSeconds);
            return Mathf.Clamp(steps, 0, Mathf.Max(0, _config.SpeedRampMaxSteps));
        }

        private int MaxHearts() => _config != null ? Mathf.Max(1, _config.MaxHearts) : 5;

        private void ChangeState(HighwayDriveState newState)
        {
            State = newState;
            _stateElapsed = 0f;
            OnStateChanged?.Invoke(newState);
        }
    }
}
