using UnityEngine;

public class PhoneMirrorCamera : MonoBehaviour
{
    [SerializeField] private Camera sourceCamera;

    private Camera mirrorCamera;

    void Awake()
    {
        mirrorCamera = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (sourceCamera == null || mirrorCamera == null)
            return;

        // Follow the user's tracked head exactly.
        transform.position = sourceCamera.transform.position;
        transform.rotation = sourceCamera.transform.rotation;

        // Match the important camera settings.
        mirrorCamera.fieldOfView = sourceCamera.fieldOfView;
        mirrorCamera.nearClipPlane = sourceCamera.nearClipPlane;
        mirrorCamera.farClipPlane = sourceCamera.farClipPlane;
        mirrorCamera.cullingMask = sourceCamera.cullingMask;
    }
}