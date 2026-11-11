using UnityEngine;
using System.Collections.Generic;

public class PeelerController : MonoBehaviour
{
    #region Input Source / Robot Control
    public enum ControlMode
    {
        Keyboard,
        Robot
    }

    [Header("── Input / Robot Control ──")]
    [SerializeField] public ControlMode controlMode = ControlMode.Robot;

    [Header("── Robot Y Percent Calibration ──")]
    [Tooltip("Incoming normalized Y position from TwinCAT (0.0 to 100.0%)")]
    [Range(0f, 100f)] public float robotYPercent = 0f;

    [Tooltip("Unity Y coordinate corresponding to 0.0% robot position")]
    [SerializeField] private float minYPosition = -3.8f;

    [Tooltip("Unity Y coordinate corresponding to 100.0% robot position")]
    [SerializeField] private float maxYPosition = 4.8f;

    [Tooltip("Minimum percent change per second to detect movement direction")]
    [SerializeField] private float percentDeadband = 0.05f;

    // Keyboard Input Settings
    [SerializeField] private KeyCode forwardKey = KeyCode.UpArrow;
    [SerializeField] private KeyCode backwardKey = KeyCode.DownArrow;

    private float previousRobotYPercent = 0f;
    private float robotYVelocity = 0f;

    // ── Input Helper Methods ──
    private bool IsForwardInputActive()
    {
        switch (controlMode)
        {
            case ControlMode.Robot:
                return robotYVelocity > percentDeadband;
            case ControlMode.Keyboard:
            default:
                return Input.GetKey(forwardKey);
        }
    }

    private bool IsBackwardInputActive()
    {
        switch (controlMode)
        {
            case ControlMode.Robot:
                return robotYVelocity < -percentDeadband;
            case ControlMode.Keyboard:
            default:
                return Input.GetKey(backwardKey);
        }
    }

    private bool WasForwardInputReleased()
    {
        switch (controlMode)
        {
            case ControlMode.Robot:
                return robotYVelocity <= percentDeadband;
            case ControlMode.Keyboard:
            default:
                return Input.GetKeyUp(forwardKey);
        }
    }
    #endregion

    #region Movement Calibration Parameters
    [Header("── Horizontal Movement ──")]
    [SerializeField] private float horizontalSpeed = 1.0f;
    [SerializeField] private float leftLimit = -2.7045f;
    [SerializeField] private float rightLimit = 2.7045f;

    [Header("── Keyboard Speeds (Fallback) ──")]
    [SerializeField] private float peelSpeed = 4f;
    [SerializeField] private float descendSpeed = 5f;

    [Header("── Attempt Activation ──")]
    [SerializeField, Range(0f, 1f)] private float attemptActivationThreshold = 0.20f;

    [Header("── Fruit Reference & Backward Compatibility ──")]
    public float fruitHalfWidth = 1.815f;
    public float fruitHalfHeight = 1.5367f;

    [Header("── Exact Blade Adjustment ──")]
    public float actualCutWidth = 1.133f;
    public float ActualPeelerWidth => actualCutWidth; // Property expected by GameManager

    [Header("── Sprite Mask ──")]
    [SerializeField] private GameObject maskPrefab;
    [SerializeField] private Transform maskContainer;
    [SerializeField] private float maskSpawnInterval = 0.3f;
    [SerializeField] private int maxMasksPerFrame = 8;
    #endregion

    #region Movement Runtime State
    private bool isOnGround = true;
    private bool isAscending = false;
    private bool isDescending = false;
    private bool isLocked = false;
    private bool mustReleaseForwardInput = false;

    private bool hasCountedAttempt = false;
    private bool touchedAtLeastOneFruit = false;

    private int moveDirection = 1;
    private float originalY;
    private float lastMaskY;
    private Vector3 previousPeelerPosition;
    private float swipeStartX;
    private float maxHeightReached;
    private float targetTopY = -999f;
    private float strokeTargetTopY = -999f;
    private bool hasValidStrokeTarget = false;
    #endregion

    #region Unity Lifecycle and Main Movement Dynamics
    void Start()
    {
        originalY = minYPosition;
        transform.position = new Vector3(transform.position.x, originalY, transform.position.z);

        if (maskContainer == null)
            maskContainer = GameObject.Find("MaskContainer")?.transform;
    }

    void Update()
    {
        if (isLocked)
        {
            // Menus, popups, pause: never leave the PLC thinking the blade is on a fruit.
            if (TwinCATManager.Instance != null) TwinCATManager.Instance.SetFruitContactState(false);
            return;
        }

        // 1. Calculate velocity and direction from incoming TwinCAT percent
        if (Time.deltaTime > 0f)
        {
            robotYVelocity = (robotYPercent - previousRobotYPercent) / Time.deltaTime;
            previousRobotYPercent = robotYPercent;
        }

        if (mustReleaseForwardInput)
        {
            if (WasForwardInputReleased())
            {
                mustReleaseForwardInput = false;
            }
            return;
        }

        // 2. HORIZONTAL MOVEMENT (Ground only)
        if (isOnGround && !isAscending && !isDescending)
        {
            float currentSpeed = horizontalSpeed;
            if (GameManager.Instance != null)
                currentSpeed *= GameManager.Instance.SpeedMultiplier;

            transform.Translate(Vector2.right * moveDirection * currentSpeed * Time.deltaTime);

            if (transform.position.x >= rightLimit) moveDirection = -1;
            if (transform.position.x <= leftLimit) moveDirection = 1;
        }

        // 3. VERTICAL MOVEMENT & POSITION MAPPING
        if (controlMode == ControlMode.Robot)
        {
            previousPeelerPosition = transform.position;

            // Direct Position Mapping: Smoothly follow TwinCAT Y position
            float targetWorldY = Mathf.Lerp(minYPosition, maxYPosition, Mathf.Clamp01(robotYPercent / 100f));
            transform.position = new Vector3(transform.position.x, targetWorldY, transform.position.z);

            if (isOnGround && IsForwardInputActive())
            {
                StartAscent();
            }

            if (!isOnGround || isAscending || isDescending)
            {
                if (transform.position.y > maxHeightReached)
                    maxHeightReached = transform.position.y;

                if (IsForwardInputActive())
                {
                    isAscending = true;
                    isDescending = false;
                    UpdateAttemptActivation();
                }
                else if (IsBackwardInputActive())
                {
                    isAscending = false;
                    isDescending = true;
                }

                if (transform.position.y <= originalY + 0.05f && isDescending)
                {
                    ReachGround();
                }
            }
        }
        else // Keyboard Mode
        {
            if ((IsForwardInputActive() && isOnGround) || isAscending)
            {
                if (isOnGround && !isAscending)
                    StartAscent();

                if (isAscending && IsForwardInputActive())
                {
                    previousPeelerPosition = transform.position;
                    transform.Translate(Vector2.up * peelSpeed * Time.deltaTime);

                    if (transform.position.y > maxHeightReached)
                        maxHeightReached = transform.position.y;

                    UpdateAttemptActivation();
                }
            }

            if (IsBackwardInputActive() && !isOnGround)
            {
                previousPeelerPosition = transform.position;
                transform.Translate(Vector2.down * descendSpeed * Time.deltaTime);

                if (transform.position.y <= originalY)
                    ReachGround();
            }
        }

        // 4. ALWAYS EVALUATE CONTACT STATUS (Syncs TRUE and FALSE live to TwinCAT)
        TrackFruitContact();
    }

    void StartAscent()
    {
        isAscending = true;
        isOnGround = false;
        isDescending = false;

        hasCountedAttempt = false;
        touchedAtLeastOneFruit = false;
        lastMaskY = transform.position.y;
        previousPeelerPosition = transform.position;
        swipeStartX = transform.position.x;
        maxHeightReached = transform.position.y;

        targetTopY = -999f;
        strokeTargetTopY = -999f;
        hasValidStrokeTarget = TryResolveStrokeTargetTop(out strokeTargetTopY);

        if (GameManager.Instance != null) GameManager.Instance.NotifyStrokeStarted();
    }

    void ReachGround()
    {
        transform.position = new Vector3(transform.position.x, originalY, transform.position.z);

        isAscending = false;
        isDescending = false;
        isOnGround = true;

        if (!hasCountedAttempt)
        {
            ResetAttemptActivationState();
            return;
        }

        float evaluationTargetTopY = touchedAtLeastOneFruit ? targetTopY : strokeTargetTopY;
        float minHeight = evaluationTargetTopY - 0.15f;

        bool climbedEnough = maxHeightReached >= minHeight;
        bool validCut = climbedEnough && touchedAtLeastOneFruit;

        if (GameManager.Instance != null)
        {
            if (validCut)
                GameManager.Instance.RegisterSuccessfulCut(swipeStartX);
            else
                GameManager.Instance.RegisterFailedCut();
        }

        ResetAttemptActivationState();
    }

    private void UpdateAttemptActivation()
    {
        if (hasCountedAttempt || !hasValidStrokeTarget) return;

        float requiredReach = strokeTargetTopY - originalY;
        if (requiredReach <= Mathf.Epsilon) return;

        float normalizedProgress = Mathf.Clamp01((maxHeightReached - originalY) / requiredReach);
        if (normalizedProgress >= Mathf.Clamp01(attemptActivationThreshold))
        {
            hasCountedAttempt = true;
        }
    }

    private bool TryResolveStrokeTargetTop(out float highestTopY)
    {
        highestTopY = float.NegativeInfinity;
        GameObject[] activeFruits = GameObject.FindGameObjectsWithTag("Fruit");

        foreach (GameObject fruit in activeFruits)
        {
            if (fruit == null || !fruit.activeInHierarchy) continue;

            SpriteRenderer sr = fruit.GetComponent<SpriteRenderer>();
            float fruitTopY = sr != null ? sr.bounds.max.y : fruit.transform.position.y + 1.25f;

            if (float.IsNaN(fruitTopY) || float.IsInfinity(fruitTopY)) continue;
            highestTopY = Mathf.Max(highestTopY, fruitTopY);
        }

        return !float.IsNegativeInfinity(highestTopY) && highestTopY > originalY;
    }

    private void ResetAttemptActivationState()
    {
        hasCountedAttempt = false;
        hasValidStrokeTarget = false;
        strokeTargetTopY = -999f;
    }

    void TrackFruitContact()
    {
        bool currentFrameContact = false;

        // Only evaluate contact while peeler is active off the ground
        if (!isOnGround)
        {
            GameObject[] activeFruits = GameObject.FindGameObjectsWithTag("Fruit");
            float px = transform.position.x;
            float py = transform.position.y;
            float previousY = previousPeelerPosition.y;

            float bladeLeft = px - (actualCutWidth / 2f);
            float bladeRight = px + (actualCutWidth / 2f);

            foreach (GameObject fObj in activeFruits)
            {
                if (!fObj.activeInHierarchy) continue;

                SpriteRenderer sr = fObj.GetComponent<SpriteRenderer>();
                float fTop, fBot, fLeft, fRight;

                if (sr != null)
                {
                    fTop = sr.bounds.max.y;
                    fBot = sr.bounds.min.y;
                    fLeft = sr.bounds.min.x;
                    fRight = sr.bounds.max.x;
                }
                else
                {
                    fTop = fObj.transform.position.y + 1.25f;
                    fBot = fObj.transform.position.y - 1.25f;
                    fLeft = fObj.transform.position.x - 1.75f;
                    fRight = fObj.transform.position.x + 1.75f;
                }

                bool horizontalOverlap = (bladeRight >= fLeft) && (bladeLeft <= fRight);
                float contactStartY = Mathf.Max(previousY, fBot);
                float contactEndY = Mathf.Min(py, fTop);
                bool verticalOverlap = py >= previousY && contactEndY >= contactStartY;

                if (horizontalOverlap && verticalOverlap)
                {
                    touchedAtLeastOneFruit = true;
                    currentFrameContact = true;
                    targetTopY = Mathf.Max(targetTopY, fTop);

                    if (GameManager.Instance != null) GameManager.Instance.NotifyFruitContact(fObj);

                    Vector3 contactStart = InterpolatePositionAtY(previousPeelerPosition, transform.position, contactStartY);
                    Vector3 contactEnd = InterpolatePositionAtY(previousPeelerPosition, transform.position, contactEndY);
                    SpawnInterpolatedMasks(contactStart, contactEnd, true);
                }
            }
        }

        // Send live state (true OR false) to TwinCATManager on every frame
        if (TwinCATManager.Instance != null)
        {
            TwinCATManager.Instance.SetFruitContactState(currentFrameContact);
        }
    }

    void SpawnInterpolatedMasks(Vector3 fromPosition, Vector3 toPosition, bool includeFinalPoint)
    {
        if (toPosition.y < fromPosition.y || maskSpawnInterval <= 0f) return;

        int masksSpawned = 0;
        float nextMaskY = lastMaskY + maskSpawnInterval;
        float firstFrameY = Mathf.Max(nextMaskY, fromPosition.y);

        while (firstFrameY <= toPosition.y && masksSpawned < maxMasksPerFrame)
        {
            Vector3 maskPosition = InterpolatePositionAtY(fromPosition, toPosition, firstFrameY);
            SpawnMaskAt(maskPosition);
            lastMaskY = firstFrameY;
            masksSpawned++;
            firstFrameY += maskSpawnInterval;
        }

        if (includeFinalPoint && masksSpawned < maxMasksPerFrame && toPosition.y > lastMaskY)
        {
            SpawnMaskAt(new Vector3(toPosition.x, toPosition.y, 0f));
            lastMaskY = toPosition.y;
        }
    }

    Vector3 InterpolatePositionAtY(Vector3 fromPosition, Vector3 toPosition, float yPosition)
    {
        if (Mathf.Approximately(fromPosition.y, toPosition.y))
            return new Vector3(toPosition.x, yPosition, 0f);

        float t = Mathf.InverseLerp(fromPosition.y, toPosition.y, yPosition);
        Vector3 position = Vector3.Lerp(fromPosition, toPosition, t);
        position.y = yPosition;
        position.z = 0f;
        return position;
    }

    void SpawnMaskAt(Vector3 position)
    {
        if (maskPrefab == null) return;

        GameObject newMask = Instantiate(maskPrefab, position, Quaternion.identity);
        float spriteUnscaledWidth = 0f;

        SpriteMask mask = newMask.GetComponent<SpriteMask>();
        if (mask != null && mask.sprite != null)
        {
            spriteUnscaledWidth = mask.sprite.bounds.size.x;
        }
        else
        {
            SpriteRenderer sr = newMask.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
                spriteUnscaledWidth = sr.sprite.bounds.size.x;
        }

        if (spriteUnscaledWidth > 0)
        {
            newMask.transform.localScale = new Vector3(
                actualCutWidth / spriteUnscaledWidth,
                newMask.transform.localScale.y,
                newMask.transform.localScale.z
            );
        }

        newMask.transform.SetParent(maskContainer, true);
    }

    public void ResetState()
    {
        transform.position = new Vector3(transform.position.x, originalY, transform.position.z);
        isAscending = false;
        isDescending = false;
        isOnGround = true;
        hasCountedAttempt = false;
        touchedAtLeastOneFruit = false;
        isLocked = false;
        mustReleaseForwardInput = false;
        ResetAttemptActivationState();
    }

    public void SetLocked(bool state) => isLocked = state;

    public void ActivateInputReleaseLock() => mustReleaseForwardInput = true;

    // Backward compatibility method expected by IntroController.cs
    public void ActivateKeyLock() => ActivateInputReleaseLock();

    void OnDrawGizmosSelected()
    {
        float yPos = Application.isPlaying ? originalY : transform.position.y;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(new Vector3(leftLimit, yPos, 0), new Vector3(rightLimit, yPos, 0));
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawCube(new Vector3(transform.position.x, transform.position.y + 0.5f, 0), new Vector3(actualCutWidth, 0.2f, 0));
    }
    #endregion
}