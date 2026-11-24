
using UnityEngine;

public class TantalFloat : MonoBehaviour
{
    [Header("Floating Settings")]
    [SerializeField] private float floatDuration = 5f;
    [SerializeField] private float floatHeight = 0.025f;

    [Header("Optional Subtle Movement")]
    [SerializeField] private float swayAngle = 0.3f;
    [SerializeField] private float swayDuration = 7f;

    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private float elapsedTime;

    void Start()
    {
        originalPosition = transform.localPosition;
        originalRotation = transform.localRotation;
    }

    void Update()
    {
        elapsedTime += Time.deltaTime;

        // Smooth up-and-down floating.
        float floatPhase = elapsedTime * Mathf.PI * 2f /
                           Mathf.Max(0.1f, floatDuration);

        float floatOffset = Mathf.Sin(floatPhase) * floatHeight;

        transform.localPosition =
            originalPosition +
            Vector3.up * floatOffset;

        // Very gentle side-to-side tilt.
        float swayPhase = elapsedTime * Mathf.PI * 2f /
                          Mathf.Max(0.1f, swayDuration);

        float sway = Mathf.Sin(swayPhase) * swayAngle;

        transform.localRotation =
            originalRotation *
            Quaternion.Euler(0f, 0f, sway);
    }

    void OnDisable()
    {
        if (!Application.isPlaying) return;

        transform.localPosition = originalPosition;
        transform.localRotation = originalRotation;
    }
}
