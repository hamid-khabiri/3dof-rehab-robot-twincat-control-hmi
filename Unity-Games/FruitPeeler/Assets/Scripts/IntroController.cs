using UnityEngine;

// The old "Move forward to start" intro screen is no longer used: the game
// now starts from the main menu popup (PeelerMenuUI). The intro object is
// still in the scenes, so it removes itself before the first frame is drawn.
public class IntroController : MonoBehaviour
{
    void Awake()
    {
        if (GlobalLevelManager.Instance != null)
        {
            GlobalLevelManager.Instance.hasSeenIntro = true;
        }

        Destroy(gameObject);
    }
}
