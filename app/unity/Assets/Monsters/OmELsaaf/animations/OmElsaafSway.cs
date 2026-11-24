
using UnityEngine;

public class OmElsaafSway : MonoBehaviour
{
    [Header("Sway Settings")]
    [SerializeField] private float swayDuration = 5f;
    [SerializeField] private float swayAngle = 1.5f;

    [Header("Optional Movement")]
    [SerializeField] private float verticalMovement = 0f;

    private Quaternion originalRotation;
    private Vector3 originalPosition;
    private float elapsedTime;

    void Start()
    {
        originalRotation = transform.localRotation;
        originalPosition = transform.localPosition;
    }

    void Update()
    {
        float phase = elapsedTime * Mathf.PI * 2f /
                      Mathf.Max(0.1f, swayDuration);

        float sway = Mathf.Sin(phase);

        // Gentle side-to-side rotation.
        transform.localRotation =
            originalRotation *
            Quaternion.Euler(0f, sway * swayAngle, 0f);

        // Optional tiny vertical movement.
        transform.localPosition =
            originalPosition +
            Vector3.up * (sway * verticalMovement);

        elapsedTime += Time.deltaTime;
    }

    void OnDisable()
    {
        if (!Application.isPlaying) return;

        transform.localRotation = originalRotation;
        transform.localPosition = originalPosition;
    }
}
