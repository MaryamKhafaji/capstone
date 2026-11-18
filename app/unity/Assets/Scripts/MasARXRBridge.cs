using System.Reflection;
using UnityEngine;

public class MasARXRBridge : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField] private Transform oldMainCamera;
    [SerializeField] private Transform nrCenterCamera;
    [SerializeField] private Transform smoothHUDAnchor;

    private Transform newCamera;

    void Awake()
    {
        if (oldMainCamera == null)
        {
            Debug.LogError("MasARXRBridge: Old Main Camera reference is missing.");
            return;
        }

        if (nrCenterCamera == null)
        {
            Debug.LogError("MasARXRBridge: NRSDK CenterCamera reference is missing.");
            return;
        }

        newCamera = nrCenterCamera;

        // Move camera-attached UI to NRSDK camera.
        MoveCameraUI();

        // Redirect gameplay scripts to NRSDK camera.
        RewireCameraReferences();

        // Disable old XR system after references/UI are moved.
        DisableOldXRObjects();

        Debug.Log("MasARXRBridge: MasAR+ connected to NRSDK successfully.");
    }

    void MoveCameraUI()
    {
        Canvas[] canvases =
            oldMainCamera.GetComponentsInChildren<Canvas>(true);

        foreach (Canvas canvas in canvases)
        {
            Transform ui = canvas.transform;

            // Skip canvases that are nested inside another canvas.
            Transform parent = ui.parent;
            bool nestedInsideAnotherCanvas = false;

            while (parent != null && parent != oldMainCamera)
            {
                if (parent.GetComponent<Canvas>() != null)
                {
                    nestedInsideAnotherCanvas = true;
                    break;
                }

                parent = parent.parent;
            }

            if (nestedInsideAnotherCanvas)
                continue;

            // Save position relative to the old camera.
            Vector3 relativePosition =
                oldMainCamera.InverseTransformPoint(ui.position);

            Quaternion relativeRotation =
                Quaternion.Inverse(oldMainCamera.rotation) * ui.rotation;

            Vector3 originalScale = ui.localScale;

            // Move UI under NRSDK CenterCamera.
            ui.SetParent(smoothHUDAnchor, false);

            // Restore same camera-relative layout.
            ui.localPosition = relativePosition;
            ui.localRotation = relativeRotation;
            ui.localScale = originalScale;

            // Redirect Event Camera for World Space / Screen Space Camera canvases.
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                Camera nrCamera = newCamera.GetComponent<Camera>();

                if (nrCamera != null)
                    canvas.worldCamera = nrCamera;
            }

            Debug.Log(
                "MasARXRBridge: moved Canvas '" +
                canvas.name +
                "' to NRSDK CenterCamera."
            );
        }
    }

    void RewireCameraReferences()
    {
        MonoBehaviour[] scripts =
            Resources.FindObjectsOfTypeAll<MonoBehaviour>();

        foreach (MonoBehaviour script in scripts)
        {
            if (script == null)
                continue;

            if (!script.gameObject.scene.IsValid())
                continue;

            FieldInfo[] fields =
                script.GetType().GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );

            foreach (FieldInfo field in fields)
            {
                if (field.FieldType != typeof(Transform))
                    continue;

                if (field.Name == "playerCamera" ||
                    field.Name == "cameraTransform")
                {
                    field.SetValue(script, newCamera);

                    Debug.Log(
                        script.GetType().Name +
                        "." +
                        field.Name +
                        " -> NRSDK CenterCamera"
                    );
                }
            }
        }
    }

    void DisableOldXRObjects()
    {
        SetObjectActive("XR Device Simulator", false);
        SetObjectActive("XR Origin (XR Rig)", false);
        SetObjectActive("XR Interaction Hands Setup", false);
    }

    void SetObjectActive(string objectName, bool active)
    {
        Transform[] allTransforms =
            Resources.FindObjectsOfTypeAll<Transform>();

        foreach (Transform t in allTransforms)
        {
            if (!t.gameObject.scene.IsValid())
                continue;

            if (t.name == objectName)
            {
                t.gameObject.SetActive(active);
                return;
            }
        }
    }
}