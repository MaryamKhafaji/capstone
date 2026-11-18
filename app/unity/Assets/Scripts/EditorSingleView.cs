using UnityEngine;

public class EditorSingleView : MonoBehaviour
{
#if UNITY_EDITOR

    private Camera leftCamera;
    private Camera rightCamera;
    private Camera centerCamera;

    void Start()
    {
        Transform left = transform.Find("LeftCamera");
        Transform center = transform.Find("CenterCamera");
        Transform right = transform.Find("RightCamera");

        if (left != null)
            leftCamera = left.GetComponent<Camera>();

        if (center != null)
            centerCamera = center.GetComponent<Camera>();

        if (right != null)
            rightCamera = right.GetComponent<Camera>();

        // Editor = single normal view
        if (leftCamera != null)
            leftCamera.enabled = false;

        if (rightCamera != null)
            rightCamera.enabled = false;

        if (centerCamera != null)
            centerCamera.enabled = true;

        Debug.Log("EDITOR: Single CenterCamera view enabled.");
    }

    void LateUpdate()
    {
        // NRSDK may try to re-enable them, so keep them off in Editor.
        if (leftCamera != null)
            leftCamera.enabled = false;

        if (rightCamera != null)
            rightCamera.enabled = false;

        if (centerCamera != null)
            centerCamera.enabled = true;
    }

#endif
}