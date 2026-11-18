
using UnityEngine;

public class SmoothHUDAnchor : MonoBehaviour
{
    [Header("Target")]
    public Transform targetCamera;

    [Header("Smoothing Settings")]
    public bool enableSmoothing = false;

    [Range(1f, 30f)]
    public float positionSmoothness = 12f;

    [Range(1f, 30f)]
    public float rotationSmoothness = 12f;

    void Start()
    {
        if (targetCamera == null)
            return;

        transform.SetPositionAndRotation(
            targetCamera.position,
            targetCamera.rotation
        );
    }

    void LateUpdate()
    {
        if (targetCamera == null)
            return;

        // Instant tracking without smoothing
        if (!enableSmoothing)
        {
            transform.SetPositionAndRotation(
                targetCamera.position,
                targetCamera.rotation
            );

            return;
        }

        // Optional smooth tracking
        float positionT =
            1f - Mathf.Exp(-positionSmoothness * Time.deltaTime);

        float rotationT =
            1f - Mathf.Exp(-rotationSmoothness * Time.deltaTime);

        transform.position = Vector3.Lerp(
            transform.position,
            targetCamera.position,
            positionT
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetCamera.rotation,
            rotationT
        );
    }
}
