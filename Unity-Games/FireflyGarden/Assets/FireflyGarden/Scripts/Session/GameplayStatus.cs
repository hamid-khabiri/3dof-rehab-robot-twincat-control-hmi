using FireflyGarden.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FireflyGarden.Session
{
    /// <summary>
    /// Read-only view of what the player is doing right now, for feedback systems (music, robot status).
    /// Code values sent to the PLC as nGameStatus:
    /// 0 = not in a level (menu, results, between levels), 1/2/3 = playing Level 1/2/3, 4 = paused.
    /// </summary>
    public static class GameplayStatus
    {
        public const int NotPlaying = 0;
        public const int Paused = 4;

        private static Scene cachedScene;
        private static bool cachedSceneValid;
        private static SingleFlowerBlockRunner blockRunner;
        private static GameplayPauseController pauseController;

        public static bool IsLevelRunning
        {
            get { Refresh(); return blockRunner != null && blockRunner.IsBlockRunning; }
        }

        public static bool IsPaused
        {
            get { Refresh(); return pauseController != null && pauseController.IsPaused; }
        }

        /// <summary>1 = Reach the Flower, 2 = Find the Similar Flower, 3 = Remember the Flower; 0 when no level runs.</summary>
        public static int LevelNumber
        {
            get
            {
                if (!IsLevelRunning) return 0;
                switch (blockRunner.CurrentGameMode)
                {
                    case GameMode.SingleFlower: return 1;
                    case GameMode.FindTheMatch: return 2;
                    case GameMode.RememberTheMatch: return 3;
                    default: return 0;
                }
            }
        }

        public static int StatusCode
        {
            get
            {
                if (!IsLevelRunning) return NotPlaying;
                return IsPaused ? Paused : LevelNumber;
            }
        }

        private static void Refresh()
        {
            Scene active = SceneManager.GetActiveScene();
            // Look the scene objects up once per scene, not every frame (the menu has none to find).
            if (cachedSceneValid && active == cachedScene) return;
            cachedScene = active;
            cachedSceneValid = true;
            blockRunner = Object.FindAnyObjectByType<SingleFlowerBlockRunner>();
            pauseController = Object.FindAnyObjectByType<GameplayPauseController>();
        }
    }
}
