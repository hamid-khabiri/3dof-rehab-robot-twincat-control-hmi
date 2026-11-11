using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class ProductMover : MonoBehaviour
{
    public event Action<ProductMover> Completed;

    private SpriteRenderer spriteRenderer;
    private Vector3 startPosition;
    private Vector3 endPosition;
    private Vector3 startScale;
    private Vector3 endScale;
    private float travelDuration;
    private float elapsedTime;
    private bool isInitialized;
    private bool hasCompleted;
    private bool isCollecting;

    public ProductDefinition ProductDefinition { get; private set; }

    public void Initialize(
        ProductDefinition productDefinition,
        Transform startTransform,
        Transform endTransform,
        float duration,
        Vector3 initialScale,
        Vector3 finalScale)
    {
        if (productDefinition == null)
        {
            throw new ArgumentNullException(nameof(productDefinition));
        }

        if (startTransform == null)
        {
            throw new ArgumentNullException(nameof(startTransform));
        }

        if (endTransform == null)
        {
            throw new ArgumentNullException(nameof(endTransform));
        }

        if (duration <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Travel duration must be greater than zero.");
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        ProductDefinition = productDefinition;
        spriteRenderer.sprite = productDefinition.Sprite;
        startPosition = startTransform.position;
        endPosition = endTransform.position;
        travelDuration = duration;
        startScale = initialScale;
        endScale = finalScale;
        elapsedTime = 0f;
        hasCompleted = false;
        isCollecting = false;

        transform.position = startPosition;
        transform.localScale = startScale;
        isInitialized = true;
    }

    private void Update()
    {
        if (!isInitialized || hasCompleted)
        {
            return;
        }

        elapsedTime += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsedTime / travelDuration);
        transform.position = Vector3.Lerp(startPosition, endPosition, progress);
        transform.localScale = Vector3.Lerp(startScale, endScale, progress);

        if (progress < 1f)
        {
            return;
        }

        transform.position = endPosition;
        transform.localScale = endScale;
        hasCompleted = true;
        Completed?.Invoke(this);
    }

    public bool BeginCollection(Transform target, Vector3 targetOffset, float duration, float finalScaleMultiplier, Action completed)
    {
        if (!isInitialized || !hasCompleted || isCollecting || target == null || duration <= 0f)
        {
            return false;
        }

        isCollecting = true;
        StartCoroutine(Collect(target, targetOffset, duration, finalScaleMultiplier, completed));
        return true;
    }

    private IEnumerator Collect(
        Transform target,
        Vector3 targetOffset,
        float duration,
        float finalScaleMultiplier,
        Action completed)
    {
        Vector3 collectionStartPosition = transform.position;
        Vector3 collectionStartScale = transform.localScale;
        Vector3 collectionEndScale = collectionStartScale * finalScaleMultiplier;
        float collectionElapsed = 0f;

        while (collectionElapsed < duration)
        {
            collectionElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(collectionElapsed / duration);
            float easedProgress = progress * progress * (3f - 2f * progress);
            transform.position = Vector3.LerpUnclamped(
                collectionStartPosition,
                target.position + targetOffset,
                easedProgress);
            transform.localScale = Vector3.LerpUnclamped(
                collectionStartScale,
                collectionEndScale,
                easedProgress);
            yield return null;
        }

        transform.position = target.position + targetOffset;
        transform.localScale = collectionEndScale;
        completed?.Invoke();
    }
}
