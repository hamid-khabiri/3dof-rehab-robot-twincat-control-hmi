using System;
using System.Collections.Generic;
using FireflyGarden.Configuration;
using FireflyGarden.Results;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FireflyGarden.Session
{
    /// <summary>
    /// The ordered list of playable levels (one session definition per level) and the single place
    /// that loads the gameplay scene for a chosen level. Registered by the main menu.
    /// </summary>
    public static class LevelLauncher
    {
        private static readonly List<SessionDefinition> levels = new List<SessionDefinition>();
        private static bool isLaunching;

        public static IReadOnlyList<SessionDefinition> Levels => levels;
        public static string GameplaySceneName { get; private set; } = "FireflyGameplay";

        public static void Register(IEnumerable<SessionDefinition> definitions, string gameplaySceneName)
        {
            levels.Clear();
            if (definitions != null)
                foreach (SessionDefinition definition in definitions)
                    if (definition != null) levels.Add(definition);
            if (!string.IsNullOrWhiteSpace(gameplaySceneName)) GameplaySceneName = gameplaySceneName;
            isLaunching = false;
        }

        public static int IndexOf(SessionDefinition definition) => definition == null ? -1 : levels.IndexOf(definition);

        public static SessionDefinition NextAfter(SessionDefinition definition)
        {
            int index = IndexOf(definition);
            return index >= 0 && index + 1 < levels.Count ? levels[index + 1] : null;
        }

        public static string LevelLabel(SessionDefinition definition)
        {
            if (definition == null) return string.Empty;
            int index = IndexOf(definition);
            string name = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.name : definition.DisplayName;
            return index >= 0 ? $"Level {index + 1}  ·  {name}" : name;
        }

        public static bool TryLaunch(SessionDefinition definition, UnityEngine.Object context)
        {
            if (isLaunching) return false;
            if (definition == null)
            {
                Debug.LogError("Cannot start level: no session definition is assigned.", context);
                return false;
            }
            if (!definition.TryValidate(out string error))
            {
                Debug.LogError("Cannot start level '" + definition.name + "': " + error, context);
                return false;
            }

            isLaunching = true;
            SessionLaunchContext.Clear();
            SessionResultsContext.Clear();
            SessionLaunchContext.Select(definition);
            Time.timeScale = 1f;
            try
            {
                SceneManager.LoadScene(GameplaySceneName);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("Failed to load gameplay scene '" + GameplaySceneName + "': " + exception.Message, context);
                return false;
            }
            finally
            {
                // The flag only guards double clicks within one frame; the next scene starts fresh.
                isLaunching = false;
            }
        }
    }
}
