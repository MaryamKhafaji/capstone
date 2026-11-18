
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class UIPanelAnimator : MonoBehaviour
{
    [Header("Animation")]
    [Min(0.01f)]
    public float entranceDuration = 0.35f;

    [Min(0.01f)]
    public float exitDuration = 0.25f;

    [Min(0f)]
    public float slideDistance = 70f;

    [Header("Options")]
    public bool animateOnEnable = true;
    public bool ignoreTimeScale = true;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Coroutine animationRoutine;

    private Vector2 originalPosition;
    private bool initialized;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (initialized)
            return;

        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        originalPosition = rectTransform.anchoredPosition;

        initialized = true;
    }

    private void OnEnable()
    {
        Initialize();

        if (animateOnEnable)
        {
            PlayShow();
        }
    }

    public void Show()
    {
        Initialize();

        if (!gameObject.activeSelf)
        {
            // OnEnable will start the entrance animation.
            gameObject.SetActive(true);

            if (!animateOnEnable)
                PlayShow();
        }
        else
        {
            PlayShow();
        }
    }

    public void Hide()
    {
        Initialize();

        if (!gameObject.activeSelf)
            return;

        StopCurrentAnimation();

        animationRoutine = StartCoroutine(
            AnimateHide()
        );
    }

    public void HideImmediately()
    {
        Initialize();

        StopCurrentAnimation();

        rectTransform.anchoredPosition = originalPosition;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        gameObject.SetActive(false);
    }

    private void PlayShow()
    {
        StopCurrentAnimation();

        animationRoutine = StartCoroutine(
            AnimateShow()
        );
    }

    private IEnumerator AnimateShow()
    {
        Vector2 startPosition =
            originalPosition + Vector2.down * slideDistance;

        rectTransform.anchoredPosition = startPosition;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float elapsed = 0f;

        while (elapsed < entranceDuration)
        {
            elapsed += GetDeltaTime();

            float t = Mathf.Clamp01(
                elapsed / entranceDuration
            );

            // Smooth deceleration.
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            rectTransform.anchoredPosition =
                Vector2.LerpUnclamped(
                    startPosition,
                    originalPosition,
                    eased
                );

            canvasGroup.alpha = eased;

            yield return null;
        }

        rectTransform.anchoredPosition = originalPosition;
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        animationRoutine = null;
    }

    private IEnumerator AnimateHide()
    {
        Vector2 startPosition =
            rectTransform.anchoredPosition;

        float startAlpha = canvasGroup.alpha;

        Vector2 endPosition =
            originalPosition + Vector2.up * slideDistance;

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float elapsed = 0f;

        while (elapsed < exitDuration)
        {
            elapsed += GetDeltaTime();

            float t = Mathf.Clamp01(
                elapsed / exitDuration
            );

            float eased = t * t * (3f - 2f * t);

            rectTransform.anchoredPosition =
                Vector2.LerpUnclamped(
                    startPosition,
                    endPosition,
                    eased
                );

            canvasGroup.alpha =
                Mathf.Lerp(
                    startAlpha,
                    0f,
                    eased
                );

            yield return null;
        }

        rectTransform.anchoredPosition = originalPosition;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        animationRoutine = null;

        gameObject.SetActive(false);
    }

    private void StopCurrentAnimation()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }
    }

    private float GetDeltaTime()
    {
        return ignoreTimeScale
            ? Time.unscaledDeltaTime
            : Time.deltaTime;
    }

    private void OnDisable()
    {
        if (!initialized)
            return;

        StopCurrentAnimation();

        rectTransform.anchoredPosition = originalPosition;
    }
}
