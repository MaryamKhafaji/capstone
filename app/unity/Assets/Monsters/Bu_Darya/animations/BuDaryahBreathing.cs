
using UnityEngine;

public class BuDaryahBreathing : MonoBehaviour
{
    [Header("Breathing Settings")]
    [SerializeField] private float breathDuration = 4f;

    [Header("Subtle Movement")]
    [SerializeField] private float verticalMovement = 0.001f;
    [SerializeField] private float forwardTilt = 0.15f;
    [SerializeField] private float scaleChange = 0.003f;

    private Vector3 originalPosition;
    private Vector3 originalScale;
    private Quaternion originalRotation;
    private float elapsedTime;

    void Start()
    {
        originalPosition = transform.localPosition;
        originalScale = transform.localScale;
        originalRotation = transform.localRotation;
    }

    void Update()
    {
        float phase = elapsedTime * Mathf.PI * 2f /
                      Mathf.Max(0.1f, breathDuration);

        // Smooth inhale and exhale
        float breath = (1f - Mathf.Cos(phase)) * 0.5f;

        // Very slight vertical movement
        transform.localPosition =
            originalPosition +
            Vector3.up * (breath * verticalMovement);

        // Gentle forward-and-back tilt
        transform.localRotation =
            originalRotation *
            Quaternion.Euler(breath * forwardTilt, 0f, 0f);

        // Tiny expansion during inhale
        float expansion = 1f + breath * scaleChange;
        transform.localScale = originalScale * expansion;

        elapsedTime += Time.deltaTime;
    }

    void OnDisable()
    {
        if (!Application.isPlaying) return;

        transform.localPosition = originalPosition;
        transform.localRotation = originalRotation;
        transform.localScale = originalScale;
    }
}
