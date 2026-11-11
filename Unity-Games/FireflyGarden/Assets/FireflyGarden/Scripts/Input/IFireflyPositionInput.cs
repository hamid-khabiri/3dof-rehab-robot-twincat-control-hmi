using UnityEngine;

namespace FireflyGarden.Input
{
    public interface IFireflyPositionInput
    {
        Vector3 RequestedWorldPosition { get; }
    }
}
