using UnityEngine;

namespace FireflyGarden.Gameplay
{
    public sealed class LogicalFlowerRegion : MonoBehaviour
    {
        [SerializeField] private Transform regionCentre;
        [SerializeField] private float interactionRadius = 1f;

        public Vector3 Centre => regionCentre != null ? regionCentre.position : transform.position;
        public float InteractionRadius => interactionRadius;

        public bool Contains(Vector3 worldPoint)
        {
            Vector3 centre = Centre;
            float deltaX = worldPoint.x - centre.x;
            float deltaY = worldPoint.y - centre.y;
            return (deltaX * deltaX) + (deltaY * deltaY) <= interactionRadius * interactionRadius;
        }

        private void OnValidate()
        {
            if (interactionRadius <= 0f)
            {
                Debug.LogError("LogicalFlowerRegion requires a positive interaction radius.", this);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Transform centreTransform = regionCentre != null ? regionCentre : transform;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(centreTransform.position, interactionRadius);
        }
    }
}
