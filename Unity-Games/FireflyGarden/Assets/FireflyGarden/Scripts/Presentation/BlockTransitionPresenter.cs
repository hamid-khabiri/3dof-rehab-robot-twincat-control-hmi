using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FireflyGarden.Presentation
{
    public sealed class BlockTransitionPresenter : MonoBehaviour
    {
        private GameObject panel;
        private TMP_Text completedText;
        private TMP_Text nextText;
        private Button continueButton;
        private bool acceptingContinue;
        private Action continuation;

        public bool IsVisible => panel != null && panel.activeSelf;

        private void Awake()
        {
            BuildPresentation();
            Hide();
        }

        public void Show(string completedMode, string nextMode, bool requireManualContinue,
            float minimumDuration, Action onContinue)
        {
            StopAllCoroutines();
            continuation = onContinue;
            acceptingContinue = false;
            completedText.text = "Completed: " + completedMode;
            nextText.text = "Next: " + nextMode + "\n\nTake a moment before continuing.";
            continueButton.gameObject.SetActive(requireManualContinue);
            continueButton.interactable = false;
            panel.SetActive(true);
            StartCoroutine(EnableAfterDelay(Mathf.Max(0f, minimumDuration), requireManualContinue));
        }

        public void Hide()
        {
            acceptingContinue = false;
            continuation = null;
            if (panel != null) panel.SetActive(false);
        }

        private IEnumerator EnableAfterDelay(float delay, bool requireManualContinue)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            acceptingContinue = true;
            if (requireManualContinue) continueButton.interactable = true;
            else AcceptContinue();
        }

        private void AcceptContinue()
        {
            if (!acceptingContinue) return;
            acceptingContinue = false;
            continueButton.interactable = false;
            Action accepted = continuation;
            continuation = null;
            accepted?.Invoke();
        }

        private void BuildPresentation()
        {
            panel = new GameObject("BlockTransitionPanel", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
            panel.transform.SetParent(transform, false);
            Canvas canvas = panel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            panel.GetComponent<Image>().color = new Color(0.06f, 0.12f, 0.12f, 0.96f);
            completedText = CreateText("CompletedBlockText", new Vector2(0f, 70f), 34, "");
            CreateText("TitleText", new Vector2(0f, 150f), 46, "Block complete");
            nextText = CreateText("NextBlockText", new Vector2(0f, -15f), 30, "");
            GameObject buttonObject = new GameObject("ContinueButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(panel.transform, false);
            RectTransform buttonRect = (RectTransform)buttonObject.transform;
            buttonRect.anchoredPosition = new Vector2(0f, -150f);
            buttonRect.sizeDelta = new Vector2(260f, 70f);
            buttonObject.GetComponent<Image>().color = new Color(0.24f, 0.62f, 0.45f, 1f);
            continueButton = buttonObject.GetComponent<Button>();
            continueButton.onClick.AddListener(AcceptContinue);
            TMP_Text label = CreateText("ContinueLabel", Vector2.zero, 28, "Continue", buttonObject.transform);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.sizeDelta = Vector2.zero;
        }

        private TMP_Text CreateText(string objectName, Vector2 position, int size, string value, Transform parent = null)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent == null ? panel.transform : parent, false);
            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            RectTransform rect = text.rectTransform;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(760f, 120f);
            return text;
        }
    }
}
