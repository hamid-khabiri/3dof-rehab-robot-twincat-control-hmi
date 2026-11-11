using FireflyGarden.Input;
using UnityEngine;

namespace FireflyGarden.Gameplay
{
    public sealed class FireflyCursorController : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour positionInputBehaviour;

        private IFireflyPositionInput positionInput;
        public bool InputEnabled { get; set; } = true;

        private void Awake()
        {
            positionInput = positionInputBehaviour as IFireflyPositionInput;
            if (positionInput == null)
            {
                Debug.LogError(
                    "FireflyCursorController requires a component implementing IFireflyPositionInput.",
                    this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (InputEnabled) transform.position = positionInput.RequestedWorldPosition;
        }
    }
}
