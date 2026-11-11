using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RehabFramework.HighwayDrive
{
    // ─────────────────────────────────────────────────────────────────────────
    //  HIGHWAY DRIVE INSTRUCTION UI (HD-v1)
    //  Builds its own patient-facing UI at runtime under the scene's
    //  PatientCanvas, same reasoning as RallyPaddleInstructionUI and
    //  ClearTheView/PatientCanvas: no scene-authored prefab to keep in sync.
    //
    //  THE ACTIVE HUD IS DELIBERATELY SPARSE (Simon, 2026-08-24). While the
    //  patient is driving, the top of the screen carries only what matters --
    //  the standing instruction banner, the trial clock, and the pause
    //  control. Everything else is either transient (the tutorial, collision
    //  messages, the occasional praise badge) or lives out of the central
    //  roadway entirely (hearts top-left, speed gauge right). Small secondary
    //  chatter over live traffic is exactly what this layout removes.
    //
    //  DIGITS. CLAUDE.md's project-wide rule is "no digits, ever, in any
    //  patient-visible string or active text component". Two components here
    //  are carved-out exceptions, both on Simon's explicit instruction:
    //    - the SAFE-TIME / trial clock (2026-08-19), a literal M:SS readout;
    //    - nothing else. The SPEED GAUGE deliberately shows NO number -- no
    //      km/h, because a speed figure adds nothing to this assessment. It
    //      is a rabbit at the fast end, a turtle at the slow end, a neutral
    //      notch, and a cyan indicator showing the current command.
    //  Every authored string still comes from Core/HighwayDrivePatientStrings
    //  and is still covered by HighwayDriveDigitFreeTests; the clock is
    //  generated text, structurally outside that catalogue.
    // ─────────────────────────────────────────────────────────────────────────

    public class HighwayDriveInstructionUI : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private Canvas _patientCanvas;
        [SerializeField] private TMP_FontAsset _fontAsset;
        [SerializeField] private HighwayDriveConfig _config;

        [Header("HUD art")]
        [SerializeField] private Sprite _bannerSprite;
        [SerializeField] private Sprite _timerIconSprite;
        [SerializeField] private Sprite _pauseButtonSprite;
        [SerializeField] private Sprite _gaugeTrackSprite;
        [SerializeField] private Sprite _gaugeKnobSprite;
        [SerializeField] private Sprite _speedTextSprite;
        [SerializeField] private Sprite _rabbitSprite;
        [SerializeField] private Sprite _turtleSprite;
        [SerializeField] private Sprite _tutorialCrossSprite;
        [SerializeField] private Sprite _centerTargetSprite;
        [Tooltip("Art/heart_full.png -- an earned heart.")]
        [SerializeField] private Sprite _heartFullSprite;
        [Tooltip("Art/heart_empty.png -- a spent slot. A distinct greyed heart rather than the same sprite " +
                 "dimmed, so the row is readable at a glance.")]
        [SerializeField] private Sprite _heartEmptySprite;
        [Tooltip("Art/heart_broken.png -- flashed briefly on the slot just spent, so a collision and its " +
                 "cost are unmistakably connected.")]
        [SerializeField] private Sprite _heartBrokenSprite;

        [Header("Layout (reference resolution 1920x1080)")]
        [Tooltip("Banner box at the 1920x1080 reference resolution. Matched to hud_banner_keepclear's own " +
                 "aspect (about 5.6:1) so preserveAspect does not letterbox it inside a mismatched box -- " +
                 "an over-tall box would fit the sprite to the WIDTH and leave dead space, which is why " +
                 "simply raising the height would not have made the text any bigger.")]
        [SerializeField] private Vector2 _bannerSize = new Vector2(880f, 157f);
        [SerializeField] private float _bannerTopInset = 30f;
        [SerializeField] private Vector2 _gaugeTrackSize = new Vector2(74f, 470f);
        [SerializeField] private float _gaugeInsetFromRight = 110f;
        [SerializeField] private float _gaugeKnobSize = 54f;
        [SerializeField] private float _heartIconSize = 44f;
        [SerializeField] private float _heartIconSpacing = 50f;
        [SerializeField] private Vector2 _heartsRowInset = new Vector2(46f, 42f);
        [Tooltip("Duration of the broken-heart flash on the slot just spent.")]
        [SerializeField] private float _heartFlashSeconds = 0.65f;
        [Tooltip("Easing time for the speed indicator, seconds. Small -- enough to guarantee a continuous " +
                 "sweep rather than visible steps, without lagging behind the command.")]
        [SerializeField] private float _gaugeSmoothingS = 0.09f;
        [Tooltip("Crazy Car: speed only goes up from cruise (no braking), so only the TOP half of the gauge " +
                 "is shown -- turtle at its bottom (cruise), rabbit at its top (fastest).")]
        [SerializeField] private bool _oneSidedSpeedGauge = false;

        private TMP_Text _instructionText;
        private TMP_Text _feedbackText;
        private TMP_Text _clockText;
        private RectTransform _gaugeKnob;
        private float _gaugeHalfTravel;
        private Image[] _hearts;
        private Sprite _heartFilled;
        private Sprite _heartEmpty;
        private int _heartCount;
        private int _flashSlot = -1;
        private float _flashElapsed;
        private float _gaugeTarget;
        private float _gaugeShown;
        private GameObject _tutorialPanel;
        private GameObject _centerTarget;
        private GameObject _bannerObject;
        private CanvasGroup _feedbackGroup;

        private void Start()
        {
            if (_patientCanvas == null)
            {
                Debug.LogError("[HighwayDriveInstructionUI] Assign _patientCanvas in the Inspector.");
                enabled = false;
                return;
            }

            BuildUI();
            SetSpeedIndicator(0f);
            SetTrialClockSeconds(0f);
            ShowCenterTarget(false);
        }

        private void BuildUI()
        {
            Transform parent = _patientCanvas.transform;

            BuildBanner(parent);
            BuildClock(parent);
            BuildPauseButton(parent);
            BuildHeartsRow(parent);
            BuildSpeedGauge(parent);
            BuildTutorial(parent);
            BuildCenterTarget(parent);

            // The transient line: collision instruction, Ready, and the praise
            // badge all share it, since only one of them is ever appropriate
            // at a time and stacking them would re-clutter the centre.
            _feedbackText = CreateText(parent, "FeedbackText", new Vector2(0.5f, 0.5f),
                new Vector2(0f, 150f), new Vector2(900f, 90f), 46f);
            _feedbackGroup = _feedbackText.gameObject.AddComponent<CanvasGroup>();
            _feedbackGroup.alpha = 0f;

            // THE BANNER ART ALREADY CONTAINS ITS OWN TEXT. The supplied
            // hud_banner_keepclear sprite has "Keep clear of the other cars"
            // baked into the artwork, so drawing a TMP string on top of it
            // rendered the sentence twice, very slightly offset -- which is
            // what made the top of the screen look bold and illegible rather
            // than merely cluttered. The banner is now the whole instruction
            // for the driving state, and this text object exists only for the
            // states that have NO banner art (paused, get-ready). It is
            // positioned BELOW the banner rather than on top of it, so the two
            // can never collide even if both were ever shown at once.
            _instructionText = CreateText(parent, "InstructionText", new Vector2(0.5f, 1f),
                new Vector2(0f, -_bannerTopInset - _bannerSize.y - 34f), new Vector2(1100f, 60f), 34f);
            _instructionText.text = string.Empty;
        }

        private void BuildBanner(Transform parent)
        {
            if (_bannerSprite == null) return;
            var go = new GameObject("Banner", typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -_bannerTopInset - _bannerSize.y * 0.5f);
            rect.sizeDelta = _bannerSize;
            var img = go.GetComponent<Image>();
            img.sprite = _bannerSprite;
            img.preserveAspect = true;
            _bannerObject = go;
        }

        /// <summary>Trial clock: the supplied timer art has 00:42 baked into
        /// it, so only the stopwatch disc is used as an icon and the live
        /// value is rendered as text beside it.</summary>
        /// <summary>Top-right cluster: stopwatch icon, live clock, pause
        /// button, laid out right-to-left with explicit non-overlapping spans.
        ///
        /// The first version placed these by eye and they collided -- the icon
        /// sat on top of the digits. Anchored to (1,1) every anchoredPosition
        /// here is a NEGATIVE offset from the right edge and every rect is
        /// centred on it, so the spans are worth writing down rather than
        /// re-deriving: pause -102..-42, clock rect -265..-135 (glyphs sit
        /// well inside that, centred), icon -323..-277. Comfortable gaps at
        /// both joins, and the whole cluster clears the centred banner.</summary>
        private void BuildClock(Transform parent)
        {
            Color theme = ThemeColor();
            float rowY = -_bannerTopInset - 28f;

            if (_timerIconSprite != null)
            {
                var iconGO = new GameObject("ClockIcon", typeof(RectTransform), typeof(Image));
                var iconRect = iconGO.GetComponent<RectTransform>();
                iconRect.SetParent(parent, false);
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(1f, 1f);
                iconRect.anchoredPosition = new Vector2(-300f, rowY);
                iconRect.sizeDelta = new Vector2(46f, 46f);
                var img = iconGO.GetComponent<Image>();
                img.sprite = _timerIconSprite;
                img.preserveAspect = true;
            }

            _clockText = CreateText(parent, "ClockText", new Vector2(1f, 1f),
                new Vector2(-200f, rowY), new Vector2(130f, 56f), 38f);
            _clockText.alignment = TextAlignmentOptions.Center;
            _clockText.color = theme;
        }

        private void BuildPauseButton(Transform parent)
        {
            if (_pauseButtonSprite == null) return;
            var go = new GameObject("PauseButton", typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-72f, -_bannerTopInset - 28f);
            rect.sizeDelta = new Vector2(60f, 60f);
            var img = go.GetComponent<Image>();
            img.sprite = _pauseButtonSprite;
            img.preserveAspect = true;
        }

        private void BuildHeartsRow(Transform parent)
        {
            int count = _config != null ? Mathf.Max(1, _config.MaxHearts) : 5;
            Color color = _config != null ? _config.HeartColor : new Color(0.90f, 0.22f, 0.32f, 1f);

            if (_heartFullSprite != null && _heartEmptySprite != null)
            {
                _heartFilled = _heartFullSprite;
                _heartEmpty = _heartEmptySprite;
            }
            else
            {
                const int px = 64;
                float ppu = px / Mathf.Max(1f, _heartIconSize);
                _heartFilled = HighwayDriveSpriteFactory.BuildHeart(px, px, ppu, color);
                _heartEmpty = HighwayDriveSpriteFactory.BuildHeartOutline(px, px, 0.16f, ppu,
                    new Color(1f, 1f, 1f, 0.35f));
            }

            var row = new GameObject("HeartsRow", typeof(RectTransform));
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.SetParent(parent, false);
            rowRect.anchorMin = rowRect.anchorMax = new Vector2(0f, 1f);
            rowRect.anchoredPosition = new Vector2(
                _heartsRowInset.x + _heartIconSize * 0.5f,
                -(_heartsRowInset.y + _heartIconSize * 0.5f));
            rowRect.sizeDelta = new Vector2(count * _heartIconSpacing, _heartIconSize);

            _hearts = new Image[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"Heart_{i}", typeof(RectTransform), typeof(Image));
                var rect = go.GetComponent<RectTransform>();
                rect.SetParent(rowRect, false);
                rect.sizeDelta = new Vector2(_heartIconSize, _heartIconSize);
                rect.anchoredPosition = new Vector2(i * _heartIconSpacing, 0f);

                var image = go.GetComponent<Image>();
                image.sprite = _heartFilled;
                image.preserveAspect = true;
                _hearts[i] = image;
            }
        }

        /// <summary>SPEED gauge, right edge: the SPEED wordmark on top, the
        /// rabbit at the fast end, the turtle at the slow end, a clearly
        /// visible neutral notch across the middle, and the cyan indicator
        /// that rides up and down with the patient's current forward/backward
        /// command. No km/h anywhere -- a speed figure adds nothing here.</summary>
        private void BuildSpeedGauge(Transform parent)
        {
            Color theme = ThemeColor();
            _gaugeHalfTravel = _gaugeTrackSize.y * 0.42f;

            var gauge = new GameObject("SpeedGauge", typeof(RectTransform));
            var gaugeRect = gauge.GetComponent<RectTransform>();
            gaugeRect.SetParent(parent, false);
            gaugeRect.anchorMin = gaugeRect.anchorMax = new Vector2(1f, 0.5f);
            // One-sided: only the top half is drawn, so drop the whole gauge by
            // a quarter track to keep the visible part centred on the screen.
            float centreShift = _oneSidedSpeedGauge ? -_gaugeTrackSize.y * 0.25f : 0f;
            gaugeRect.anchoredPosition = new Vector2(-_gaugeInsetFromRight, centreShift);
            gaugeRect.sizeDelta = _gaugeTrackSize;

            // Stacked outward from the track ends with real clearance. The
            // first pass put the rabbit at +18 from a track half-height of
            // 235, so its 40px box straddled the track end and the two
            // overlapped; these offsets are measured from the END of the
            // track plus half the icon plus a gap, so they cannot collide
            // however the track is resized.
            float trackHalf = _gaugeTrackSize.y * 0.5f;
            var animalSize = new Vector2(76f, 54f);
            float animalY = trackHalf + animalSize.y * 0.5f + 14f;

            if (_rabbitSprite != null)
                AddImage(gaugeRect, "Rabbit", _rabbitSprite, new Vector2(0f, animalY), animalSize);

            if (_turtleSprite != null)
            {
                // One-sided: the turtle sits just under the visible top half.
                float turtleY = _oneSidedSpeedGauge ? -(animalSize.y * 0.5f + 14f) : -animalY;
                AddImage(gaugeRect, "Turtle", _turtleSprite, new Vector2(0f, turtleY), animalSize);
            }

            if (_speedTextSprite != null)
                AddImage(gaugeRect, "SpeedWord", _speedTextSprite,
                    new Vector2(0f, animalY + animalSize.y * 0.5f + 32f), new Vector2(140f, 42f));

            if (_gaugeTrackSprite != null)
            {
                var track = AddImage(gaugeRect, "Track", _gaugeTrackSprite, Vector2.zero, _gaugeTrackSize);
                track.preserveAspect = true;
                if (_oneSidedSpeedGauge)
                {
                    // Draw only the upper half of the track art.
                    track.type = Image.Type.Filled;
                    track.fillMethod = Image.FillMethod.Vertical;
                    track.fillOrigin = (int)Image.OriginVertical.Top;
                    track.fillAmount = 0.5f;
                }
            }

            // Neutral notch, drawn over the track so it stays legible with the
            // indicator sitting on it. This is the reference the patient reads
            // "am I above or below traffic speed" against, so it must never be
            // ambiguous.
            var notch = new GameObject("NeutralNotch", typeof(RectTransform), typeof(Image));
            var notchRect = notch.GetComponent<RectTransform>();
            notchRect.SetParent(gaugeRect, false);
            notchRect.anchorMin = notchRect.anchorMax = new Vector2(0.5f, 0.5f);
            notchRect.anchoredPosition = Vector2.zero;
            notchRect.sizeDelta = new Vector2(_gaugeTrackSize.x + 26f, 5f);
            notch.GetComponent<Image>().sprite = HighwayDriveSpriteFactory.BuildRectFill(8, 8, 8f, Color.white);
            notch.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.85f);
            // One-sided: there is no "below neutral", so no neutral notch.
            if (_oneSidedSpeedGauge) notch.SetActive(false);

            var knobGO = new GameObject("Indicator", typeof(RectTransform), typeof(Image));
            _gaugeKnob = knobGO.GetComponent<RectTransform>();
            _gaugeKnob.SetParent(gaugeRect, false);
            _gaugeKnob.anchorMin = _gaugeKnob.anchorMax = new Vector2(0.5f, 0.5f);
            _gaugeKnob.anchoredPosition = Vector2.zero;
            _gaugeKnob.sizeDelta = new Vector2(_gaugeKnobSize, _gaugeKnobSize);
            var knobImg = knobGO.GetComponent<Image>();
            knobImg.preserveAspect = true;
            if (_gaugeKnobSprite != null) knobImg.sprite = _gaugeKnobSprite;
            else
            {
                knobImg.sprite = HighwayDriveSpriteFactory.BuildDisc(64, 64f / _gaugeKnobSize, Color.white);
                knobImg.color = theme;
            }
        }

        /// <summary>The planar-control tutorial: up is faster, down is slower,
        /// left and right move the car. Shown for a couple of seconds before
        /// gameplay and then destroyed outright -- it must not linger over
        /// live traffic.</summary>
        private void BuildTutorial(Transform parent)
        {
            if (_tutorialCrossSprite == null) return;
            var go = new GameObject("TutorialCross", typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(660f, 372f);
            var img = go.GetComponent<Image>();
            img.sprite = _tutorialCrossSprite;
            img.preserveAspect = true;
            _tutorialPanel = go;
            go.SetActive(false);
        }

        /// <summary>The cyan recentering target, shown only during collision
        /// recovery. It marks the neutral position the patient has to return
        /// the end effector to before the run resumes.</summary>
        private void BuildCenterTarget(Transform parent)
        {
            var go = new GameObject("CenterTarget", typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(220f, 220f);
            var img = go.GetComponent<Image>();
            img.preserveAspect = true;
            if (_centerTargetSprite != null) img.sprite = _centerTargetSprite;
            else
            {
                img.sprite = HighwayDriveSpriteFactory.BuildRectOutline(128, 128, 0.06f, 0.6f, Color.white);
                img.color = ThemeColor();
            }
            _centerTarget = go;
            go.SetActive(false);
        }

        private Image AddImage(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            return img;
        }

        private TMP_Text CreateText(Transform parent, string name, Vector2 anchor, Vector2 anchoredPos,
            Vector2 size, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = _fontAsset;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.text = string.Empty;
            return text;
        }

        private Color ThemeColor() =>
            _config != null ? _config.ThemeColor : new Color(0.35f, 0.78f, 0.92f, 1f);

        // ── Public API -- called by HighwayDriveController ───────────────

        public void ShowTutorial(bool visible)
        {
            if (_tutorialPanel != null) _tutorialPanel.SetActive(visible);
        }

        /// <summary>Destroys the tutorial outright once it has been shown --
        /// not merely hides it. It is a one-time pre-gameplay overlay and
        /// nothing should be able to bring it back over live traffic.</summary>
        public void DisposeTutorial()
        {
            if (_tutorialPanel == null) return;
            Destroy(_tutorialPanel);
            _tutorialPanel = null;
        }

        public void ShowCenterTarget(bool visible)
        {
            if (_centerTarget != null) _centerTarget.SetActive(visible);
        }

        /// <summary>The driving instruction is the BANNER ART, which carries
        /// its own text -- so the TMP line is cleared rather than duplicating
        /// it. The other two states have no banner art, so they hide the
        /// banner and use the text. Banner and text are never both populated.</summary>
        public void ShowKeepClear()
        {
            SetBannerVisible(true);
            SetInstruction(string.Empty);
        }

        public void ShowGetReady()
        {
            SetBannerVisible(false);
            SetInstruction(HighwayDrivePatientStrings.GetReady);
        }

        public void ShowPaused()
        {
            SetBannerVisible(false);
            SetInstruction(HighwayDrivePatientStrings.Paused);
        }

        private void SetBannerVisible(bool visible)
        {
            if (_bannerObject != null) _bannerObject.SetActive(visible);
        }

        /// <summary>Transient centre message: collision instruction, Ready, or
        /// the praise badge. Alpha is driven separately by SetFeedbackAlpha so
        /// the controller can fade it out.</summary>
        public void ShowFeedback(string asciiMessage)
        {
            if (_feedbackText == null) return;
            _feedbackText.text = asciiMessage;
            if (_feedbackGroup != null) _feedbackGroup.alpha = string.IsNullOrEmpty(asciiMessage) ? 0f : 1f;
        }

        public void SetFeedbackAlpha(float alpha)
        {
            if (_feedbackGroup != null) _feedbackGroup.alpha = Mathf.Clamp01(alpha);
        }

        /// <summary>The trial clock. One of the two deliberate digit
        /// exceptions in this instrument.</summary>
        public void SetTrialClockSeconds(float elapsedSeconds)
        {
            if (_clockText != null)
                _clockText.text = HighwayDriveControlMath.FormatElapsedClock(elapsedSeconds);
        }

        /// <summary>Moves the gauge indicator from a -1..+1 command, where 0
        /// sits exactly on the neutral notch. No number is displayed.</summary>
        public void SetSpeedIndicator(float normalizedSpeed)
        {
            _gaugeTarget = HighwayDriveControlMath.Clamp(normalizedSpeed, _oneSidedSpeedGauge ? 0f : -1f, 1f);
        }

        public void SetHeartsDisplay(int heartCount)
        {
            if (_hearts == null) return;
            _heartCount = Mathf.Clamp(heartCount, 0, _hearts.Length);
            for (int i = 0; i < _hearts.Length; i++)
            {
                if (i == _flashSlot) continue;   // mid-flash: RefreshHeartFlash owns it
                _hearts[i].sprite = i < _heartCount ? _heartFilled : _heartEmpty;
                _hearts[i].color = i < _heartCount ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                _hearts[i].rectTransform.localScale = Vector3.one;
            }
        }

        /// <summary>Flashes the broken-heart icon on the slot just spent and
        /// pulses it, so the participant sees WHICH heart went and connects it
        /// to the collision that took it. Purely presentational -- the count
        /// itself has already changed.</summary>
        public void FlashHeartLost(int slotIndex)
        {
            if (_hearts == null || slotIndex < 0 || slotIndex >= _hearts.Length) return;
            _flashSlot = slotIndex;
            _flashElapsed = 0f;
            if (_heartBrokenSprite != null) _hearts[slotIndex].sprite = _heartBrokenSprite;
            _hearts[slotIndex].color = Color.white;
        }

        private void Update()
        {
            RefreshHeartFlash();
            RefreshGaugeIndicator();
        }

        private void RefreshHeartFlash()
        {
            if (_flashSlot < 0 || _hearts == null) return;

            _flashElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_flashElapsed / _heartFlashSeconds);

            // Quick swell then settle, so the eye is pulled to the slot.
            float scale = 1f + 0.5f * Mathf.Sin(t * Mathf.PI);
            _hearts[_flashSlot].rectTransform.localScale = Vector3.one * scale;

            if (t < 1f) return;

            int slot = _flashSlot;
            _flashSlot = -1;
            _hearts[slot].rectTransform.localScale = Vector3.one;
            SetHeartsDisplay(_heartCount);
        }

        /// <summary>Eases the gauge indicator toward its target every frame.
        /// The speed command itself is already smoothed, but the indicator is
        /// the thing being watched, so it gets its own easing -- that
        /// guarantees a continuous sweep rather than visible steps however the
        /// underlying command is quantised or however the frame rate varies.</summary>
        private void RefreshGaugeIndicator()
        {
            if (_gaugeKnob == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _gaugeShown = _gaugeSmoothingS <= 0f
                ? _gaugeTarget
                : Mathf.Lerp(_gaugeShown, _gaugeTarget, 1f - Mathf.Exp(-dt / _gaugeSmoothingS));
            _gaugeKnob.anchoredPosition = new Vector2(0f, _gaugeShown * _gaugeHalfTravel);
        }

        private void SetInstruction(string text)
        {
            if (_instructionText != null) _instructionText.text = text;
        }
    }
}
