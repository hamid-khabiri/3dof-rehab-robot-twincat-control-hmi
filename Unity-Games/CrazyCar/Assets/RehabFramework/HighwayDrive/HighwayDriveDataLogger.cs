using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace RehabFramework.HighwayDrive
{
    // ─────────────────────────────────────────────────────────────────────────
    //  HIGHWAY DRIVE DATA LOGGER (Simon's spec, 2026-08-24)
    //  Records the raw assessment trajectory INDEPENDENTLY OF THE GRAPHICS.
    //
    //  THE POINT IS THE WORD "RAW". HighwayDriveInput applies a dead zone and
    //  a short smoothing pass so the car is pleasant to control; that
    //  filtering exists to serve the patient's experience and must never be
    //  what later analysis sees. So this logs BOTH:
    //    - RawLateral / RawForward: the unfiltered normalised end-effector
    //      position, dead zone and smoothing not applied. This is the
    //      trajectory. Tremor, drift and micro-corrections are all still in
    //      it, which is exactly why it is the column that matters.
    //    - the mapped car position and the speed actually in effect: what the
    //      patient saw, so a reviewer can reconcile the two.
    //  Recording only the filtered signal would silently discard the movement
    //  quality the assessment is trying to measure.
    //
    //  Buffered in memory and written once at Flush(), not appended per
    //  sample: this runs every frame during a live session driving real
    //  hardware, and a per-frame file write is exactly the kind of stall that
    //  shows up as a hitch in the patient's control.
    //
    //  Matches the project convention that offline recomputation from raw
    //  samples is authoritative (CLAUDE.md) -- nothing here is a summary or a
    //  derived metric, only the samples.
    // ─────────────────────────────────────────────────────────────────────────

    public class HighwayDriveDataLogger : MonoBehaviour
    {
        [Header("Output")]
        [Tooltip("Written under Application.persistentDataPath. One file per session, stamped at Flush().")]
        [SerializeField] private string _fileNamePrefix = "HighwayDrive";
        [Tooltip("Samples per second. 100 Hz matches the robot poll rate used across this project.")]
        [SerializeField] private float _sampleRateHz = 100f;
        [SerializeField] private bool _enabled = true;

        private struct Sample
        {
            public float T;
            public float RawLateral, RawForward;
            public float MappedCarX, SpeedWorldPerS, NormalizedSpeed;
            public int State;
            public float WristDeg, GasPercent;
            public int TargetLane;
        }

        private bool _laneColumns;

        private readonly List<Sample> _samples = new List<Sample>();
        private readonly List<string> _collisions = new List<string>();
        private float _sampleAccumulator;
        private bool _flushed;

        /// <summary>Called every frame by HighwayDriveController. Decimates to
        /// the configured rate rather than logging per rendered frame, so the
        /// sample spacing is a known quantity instead of a function of the
        /// machine's frame rate.</summary>
        public void Record(HighwayDriveController controller, HighwayDriveInput input, HighwayDriveState state)
        {
            if (!_enabled || controller == null || input == null) return;

            float period = _sampleRateHz > 0f ? 1f / _sampleRateHz : 0.01f;
            _sampleAccumulator += Time.deltaTime;
            if (_sampleAccumulator < period) return;
            _sampleAccumulator = 0f;

            _samples.Add(new Sample
            {
                T = controller.TrialElapsedSeconds,
                RawLateral = input.RawLateral,
                RawForward = input.RawForward,
                MappedCarX = controller.MappedCarX,
                SpeedWorldPerS = controller.CurrentSpeedWorldPerS,
                NormalizedSpeed = controller.NormalizedSpeed,
                State = (int)state,
                WristDeg = input.WristAngleDeg,
                GasPercent = input.GasPercent,
                TargetLane = input.TargetLane,
            });
            if (input.UsesLaneSteering) _laneColumns = true;
        }

        /// <summary>Starts a fresh recording: Crazy Car plays several games
        /// per launch and writes one file pair per game.</summary>
        public void BeginNewSession()
        {
            _samples.Clear();
            _collisions.Clear();
            _sampleAccumulator = 0f;
            _flushed = false;
        }

        public void RecordCollision(float trialElapsedSeconds, Vector2 contactPoint)
        {
            if (!_enabled) return;
            _collisions.Add(string.Format(CultureInfo.InvariantCulture,
                "{0:F3},{1:F3},{2:F3}", trialElapsedSeconds, contactPoint.x, contactPoint.y));
        }

        /// <summary>Writes the session to disk. Safe to call more than once --
        /// only the first call writes, so an end-of-assessment flush followed
        /// by an OnDisable flush does not produce two files.</summary>
        public void Flush()
        {
            if (!_enabled || _flushed || _samples.Count == 0) return;
            _flushed = true;

            string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string dir = Application.persistentDataPath;
            string samplePath = Path.Combine(dir, $"{_fileNamePrefix}_{stamp}_samples.csv");
            string collisionPath = Path.Combine(dir, $"{_fileNamePrefix}_{stamp}_collisions.csv");

            var sb = new StringBuilder();
            // Lane steering (Crazy Car) appends the raw wrist angle, gas and
            // target lane; Highway Drive files keep their original columns.
            sb.AppendLine("t_s,raw_lateral,raw_forward,mapped_car_x,speed_world_per_s,normalized_speed,state" +
                          (_laneColumns ? ",wrist_deg,gas_percent,target_lane" : string.Empty));
            foreach (Sample s in _samples)
            {
                sb.Append(string.Format(CultureInfo.InvariantCulture,
                    "{0:F4},{1:F5},{2:F5},{3:F4},{4:F4},{5:F4},{6}",
                    s.T, s.RawLateral, s.RawForward, s.MappedCarX, s.SpeedWorldPerS, s.NormalizedSpeed, s.State));
                if (_laneColumns)
                    sb.Append(string.Format(CultureInfo.InvariantCulture,
                        ",{0:F3},{1:F2},{2}", s.WristDeg, s.GasPercent, s.TargetLane));
                sb.AppendLine();
            }

            var cb = new StringBuilder();
            cb.AppendLine("t_s,contact_x,contact_y");
            foreach (string c in _collisions) cb.AppendLine(c);

            try
            {
                File.WriteAllText(samplePath, sb.ToString());
                File.WriteAllText(collisionPath, cb.ToString());
                Debug.Log($"[HighwayDriveDataLogger] Wrote {_samples.Count} samples to {samplePath}");
            }
            catch (System.Exception e)
            {
                // A failed write must never take the session down with it --
                // the patient is mid-assessment on real hardware.
                Debug.LogError($"[HighwayDriveDataLogger] Could not write session data: {e.Message}");
            }
        }
    }
}
