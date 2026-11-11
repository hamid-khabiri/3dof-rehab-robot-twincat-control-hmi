using UnityEngine;
using UnityEngine.InputSystem;

namespace FireflyGarden.Input
{
    public sealed class MouseFireflyPositionInput : MonoBehaviour, IFireflyPositionInput
    {
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private float gameplayPlaneZ;

        private Vector3 lastRequestedWorldPosition;

        public Vector3 RequestedWorldPosition
        {
            get
            {
                Mouse mouse = Mouse.current;
                // While the therapist works on the HMI the game has no focus and the Input System resets
                // the mouse to (0, 0); hold the last position so the firefly does not jump to the corner.
                if (mouse == null || !Application.isFocused)
                {
                    return lastRequestedWorldPosition;
                }

                Vector2 screenPosition = mouse.position.ReadValue();
                float distanceToGameplayPlane = gameplayPlaneZ - gameplayCamera.transform.position.z;
                Vector3 worldPosition = gameplayCamera.ScreenToWorldPoint(
                    new Vector3(screenPosition.x, screenPosition.y, distanceToGameplayPlane));
                worldPosition.z = gameplayPlaneZ;
                lastRequestedWorldPosition = worldPosition;
                return lastRequestedWorldPosition;
            }
        }

        private void Awake()
        {
            if (gameplayCamera == null)
            {
                Debug.LogError("MouseFireflyPositionInput requires an assigned gameplay camera.", this);
                enabled = false;
                return;
            }

            float distanceToGameplayPlane = gameplayPlaneZ - gameplayCamera.transform.position.z;
            Vector3 screenCentre = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, distanceToGameplayPlane);
            lastRequestedWorldPosition = gameplayCamera.ScreenToWorldPoint(screenCentre);
            lastRequestedWorldPosition.z = gameplayPlaneZ;

            if (Mouse.current == null)
                Debug.LogWarning("No Input System mouse is currently available; firefly input will remain at its last position.", this);
        }
    }
}
