using UnityEngine;
using UnityEngine.InputSystem;

namespace RehabFramework.HighwayDrive
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Throwaway test harness for Highway Drive, mirroring BJ's
    //  QuickPlayBootStrap, CV's QuickPlayBootstrapCV, and RP's
    //  QuickPlayBootstrapRP (console-only). The game itself is owned by
    //  HighwayDriveController; this just logs its state transitions and binds
    //  dev-only keys to the two THERAPIST controls that have no UI yet.
    //
    //  Those two controls are genuinely different things and the keys are
    //  separate on purpose:
    //    _restartRunKey  -> StartRun()   -- another drive after a crash
    //    _endSessionKey  -> EndSession() -- the open-ended session is over
    //  Production hardware would put both on a real clinician panel.
    //
    //  NO RESUME KEY, because there is nothing to resume from: this instrument
    //  has no self-pausing safety heuristic (Simon's call, 2026-08-17 -- see
    //  HighwayDriveController). The only thing that can hold a run is the
    //  robot reporting itself not awake, and that clears itself the moment the
    //  robot wakes back up, with no operator action.
    //
    //  SETUP: put on the HighwayDrive hub GameObject, drag
    //  HighwayDriveController into its field, press Play. Move the mouse:
    //  left/right steers, up/down is the throttle.
    // ─────────────────────────────────────────────────────────────────────────

    public class QuickPlayBootstrapHD : MonoBehaviour
    {
        [SerializeField] private HighwayDriveController _controller;

        [Header("Dev keys (stand-ins for therapist controls)")]
        [Tooltip("Starts another drive. Only useful when AutoRestartSeconds is 0, or to cut a run short.")]
        [SerializeField] private Key _restartRunKey = Key.R;
        [Tooltip("Ends the whole open-ended session.")]
        [SerializeField] private Key _endSessionKey = Key.F;

        private void Start()
        {
            if (_controller == null)
            {
                Debug.LogError("[QuickPlayHD] Assign _controller in the Inspector.");
                enabled = false;
                return;
            }

            _controller.OnStateChanged += s => Debug.Log($"[QuickPlayHD] state -> {s}");
            _controller.OnCrash += p => Debug.Log($"[QuickPlayHD] CRASH at ({p.x:F2}, {p.y:F2}) after " +
                                                 $"{_controller.DistanceTravelled:F0} world units this run. " +
                                                 $"Hearts remaining: {_controller.CurrentHearts}.");

            Debug.Log("[QuickPlayHD] Move the mouse to drive: left/right steers, up is faster, down is slower. " +
                      $"Press {_restartRunKey} for another drive, {_endSessionKey} to end the session.");
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb[_restartRunKey].wasPressedThisFrame) _controller.StartRun();
            else if (kb[_endSessionKey].wasPressedThisFrame) _controller.EndSession();
        }
    }
}
