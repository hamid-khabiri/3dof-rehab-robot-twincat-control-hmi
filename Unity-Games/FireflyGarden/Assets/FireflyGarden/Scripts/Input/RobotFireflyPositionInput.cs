using FireflyGarden.Configuration;
using FireflyGarden.Robot;
using UnityEngine;

namespace FireflyGarden.Input
{
    /// <summary>
    /// Places the firefly where the robot end effector is. The robot workspace (calibrated in
    /// TwinCatSettings) is mapped directly onto the gameplay area: the desktop safe-area rectangle
    /// around HomeFlower, so the robot's centre is the home flower and its edges reach every target.
    /// No smoothing. Falls back to the mouse while no robot position is available.
    /// </summary>
    public sealed class RobotFireflyPositionInput : MonoBehaviour, IFireflyPositionInput
    {
        [SerializeField] private Transform homeAnchor;
        [SerializeField] private DesktopGameplaySafeArea gameplayArea;
        [SerializeField] private float gameplayPlaneZ;
        [Header("Fallback")]
        [SerializeField] private MouseFireflyPositionInput mouseFallback;
        [SerializeField] private bool useMouseWhenRobotUnavailable = true;
        [Header("Runtime Debugging")]
        [SerializeField] private bool usingRobot;

        private Vector3 lastRequestedWorldPosition;

        public bool UsingRobot => usingRobot;

        public Vector3 RequestedWorldPosition
        {
            get
            {
                TwinCatFireflyConnector connector = TwinCatFireflyConnector.Instance;
                usingRobot = connector != null && connector.HasRobotPosition;
                if (usingRobot) lastRequestedWorldPosition = MapToWorld(connector.NormalizedPosition);
                else if (useMouseWhenRobotUnavailable && mouseFallback != null)
                    lastRequestedWorldPosition = mouseFallback.RequestedWorldPosition;
                return lastRequestedWorldPosition;
            }
        }

        private void Awake()
        {
            if (homeAnchor == null || gameplayArea == null)
            {
                Debug.LogError("RobotFireflyPositionInput requires the home anchor and the gameplay area.", this);
                enabled = false;
                return;
            }
            lastRequestedWorldPosition = new Vector3(homeAnchor.position.x, homeAnchor.position.y, gameplayPlaneZ);
        }

        /// <summary>-1..+1 per axis -> the gameplay rectangle around the home flower.</summary>
        private Vector3 MapToWorld(Vector2 normalized)
        {
            float tx = (normalized.x + 1f) * 0.5f;
            float ty = (normalized.y + 1f) * 0.5f;
            Vector3 home = homeAnchor.position;
            float x = home.x + Mathf.Lerp(gameplayArea.MinimumXOffsetFromHome, gameplayArea.MaximumXOffsetFromHome, tx);
            float y = home.y + Mathf.Lerp(gameplayArea.MinimumYOffsetFromHome, gameplayArea.MaximumYOffsetFromHome, ty);
            return new Vector3(x, y, gameplayPlaneZ);
        }

        private void OnDrawGizmosSelected()
        {
            if (homeAnchor == null || gameplayArea == null) return;
            Vector3 home = homeAnchor.position;
            Vector3 min = home + new Vector3(gameplayArea.MinimumXOffsetFromHome, gameplayArea.MinimumYOffsetFromHome, 0f);
            Vector3 max = home + new Vector3(gameplayArea.MaximumXOffsetFromHome, gameplayArea.MaximumYOffsetFromHome, 0f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube((min + max) * 0.5f, max - min);
        }
    }
}
