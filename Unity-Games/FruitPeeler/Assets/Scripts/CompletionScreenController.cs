using UnityEngine;
using UnityEngine.SceneManagement;

public class CompletionScreenController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";

    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle summaryTitleStyle;
    private GUIStyle summaryStyle;
    private GUIStyle buttonStyle;

    private void OnGUI()
    {
        EnsureStyles();

        float panelWidth = Mathf.Min(560f, Screen.width - 80f);
        float panelHeight = 440f;
        Rect panel = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);

        GUILayout.BeginArea(panel);
        GUILayout.FlexibleSpace();

        GUILayout.Label("You did it!", titleStyle);
        GUILayout.Space(16f);
        GUILayout.Label("Great job completing all levels.", subtitleStyle);
        GUILayout.Space(18f);

        DrawSessionSummary();

        GUILayout.Space(18f);

        if (GUILayout.Button("Play Again", buttonStyle, GUILayout.Height(56f)))
        {
            PlayAgain();
        }

        GUILayout.Space(18f);

        if (GUILayout.Button("Back to Menu", buttonStyle, GUILayout.Height(56f)))
        {
            BackToMenu();
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndArea();
    }

    private void EnsureStyles()
    {
        if (titleStyle != null) return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 48,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        titleStyle.normal.textColor = Color.white;

        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 26,
            wordWrap = true
        };
        subtitleStyle.normal.textColor = Color.white;

        summaryTitleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        summaryTitleStyle.normal.textColor = Color.white;

        summaryStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            wordWrap = true
        };
        summaryStyle.normal.textColor = Color.white;

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 26,
            fontStyle = FontStyle.Bold
        };
    }

    private void DrawSessionSummary()
    {
        GlobalLevelManager levelManager = GlobalLevelManager.Instance;
        SessionMetrics metrics = levelManager != null ? levelManager.SessionMetrics : null;

        if (metrics == null)
        {
            GUILayout.Label("Session data unavailable", summaryTitleStyle);
            return;
        }

        GUILayout.Label("Session Summary", summaryTitleStyle);
        GUILayout.Space(6f);
        GUILayout.Label("Starting Level: " + GetLevelName(metrics.StartingLevel), summaryStyle);
        GUILayout.Label("Successful Peels: " + metrics.SuccessfulPeels + "    Missed Attempts: " + metrics.FailedOrMissedAttempts, summaryStyle);
        GUILayout.Label("Peel Success Rate: " + (metrics.PeelSuccessRate * 100f).ToString("F2") + "%    Session Duration: " + FormatDuration(metrics.DurationSeconds), summaryStyle);
    }

    private string GetLevelName(int level)
    {
        switch (level)
        {
            case 1: return "Kiwi";
            case 2: return "Orange";
            case 3: return "Grapefruit";
            case 4: return "Melon";
            case 5: return "Pineapple";
            default: return "Level " + level;
        }
    }

    private string FormatDuration(float durationSeconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(durationSeconds));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return minutes.ToString("00") + ":" + seconds.ToString("00");
    }

    public void PlayAgain()
    {
        if (GlobalLevelManager.Instance != null)
        {
            GlobalLevelManager.Instance.RestartFromFirstLevel();
        }
        else
        {
            SceneManager.LoadScene(MainMenuSceneName);
        }
    }

    public void BackToMenu()
    {
        if (GlobalLevelManager.Instance != null)
        {
            GlobalLevelManager.Instance.BackToMainMenu();
        }
        else
        {
            SceneManager.LoadScene(MainMenuSceneName);
        }
    }
}
