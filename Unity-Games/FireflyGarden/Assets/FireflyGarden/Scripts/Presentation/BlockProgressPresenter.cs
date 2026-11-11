using UnityEngine;
using UnityEngine.UI;

namespace FireflyGarden.Presentation
{
    public sealed class BlockProgressPresenter : MonoBehaviour
    {
        [SerializeField] private Text progressText;
        public bool IsConfigured => progressText != null;

        private void Awake() => Clear();

        public void ShowProgress(int currentTrial, int totalTrials)
        {
            Clear();
        }

        public void ShowBlockComplete(int totalTrials)
        {
            progressText.text = "Block complete";
            progressText.enabled = true;
        }

        public void Clear()
        {
            if (progressText == null) return;
            progressText.text = string.Empty;
            progressText.enabled = false;
        }
    }
}
