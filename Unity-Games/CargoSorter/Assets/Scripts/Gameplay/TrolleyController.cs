using UnityEngine;

public sealed class TrolleyController : MonoBehaviour
{
    [SerializeField] private MonoBehaviour inputProvider;
    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private float minimumX = -2.7f;
    [SerializeField] private float maximumX = 2.7f;

    // Robot end-effector X, in the PLC's units. Set by TwincatConnectorSorter.
    public float Robot_Input_X = 0f;
    public bool useRobotControl = false;

    // Robot X at its home position (middle of the workspace). This value puts the trolley at the center of its track.
    public float robotCenterX = 0f;

    // Robot travel either side of robotCenterX that spans the full trolley track.
    // e.g. 20 = robot X from -20% to +20% moves the trolley end to end (TwinCAT workspace is -100% .. +100%).
    public int numberOfBins = 5;   // set to 3 or 5
    public float robotHalfRange3Bins = 20f;
    public float robotHalfRange5Bins = 50f;

    private IHorizontalInputProvider horizontalInputProvider;

    private void Awake()
    {
        horizontalInputProvider = inputProvider as IHorizontalInputProvider;
        if (horizontalInputProvider == null && !useRobotControl)
        {
            Debug.LogError(
                $"{nameof(TrolleyController)} on '{name}' requires an input provider that implements " +
                $"{nameof(IHorizontalInputProvider)}.",
                this);
        }
    }

    private void Update()
    {
        float lowerBound = Mathf.Min(minimumX, maximumX);
        float upperBound = Mathf.Max(minimumX, maximumX);
        Vector3 position = transform.position;

        if (useRobotControl)
        {
            float halfRange = (numberOfBins == 3) ? robotHalfRange3Bins : robotHalfRange5Bins;
            float romHalf = Mathf.Max(Mathf.Abs(halfRange), 0.01f);
            float lower = robotCenterX - romHalf;
            float upper = robotCenterX + romHalf;
            float t = Mathf.Clamp01((Robot_Input_X - lower) / (upper - lower));
            position.x = Mathf.Lerp(lowerBound, upperBound, t);
            transform.position = position;
            return;
        }

        if (horizontalInputProvider == null)
        {
            return;
        }

        float horizontalInput = Mathf.Clamp(horizontalInputProvider.GetHorizontalInput(), -1f, 1f);
        float nextX = position.x + horizontalInput * Mathf.Abs(movementSpeed) * Time.deltaTime;

        position.x = Mathf.Clamp(nextX, lowerBound, upperBound);
        transform.position = position;
    }
}
