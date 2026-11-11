using UnityEngine;
using UnityEngine.InputSystem;

public sealed class KeyboardInputProvider : MonoBehaviour, IHorizontalInputProvider
{
    public float GetHorizontalInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return 0f;
        }

        bool leftHeld = keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed;
        bool rightHeld = keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed;

        if (leftHeld == rightHeld)
        {
            return 0f;
        }

        return leftHeld ? -1f : 1f;
    }
}
