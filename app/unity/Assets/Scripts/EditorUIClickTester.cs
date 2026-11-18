using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-10000)]
public class EditorUIClickTester : MonoBehaviour
{
#if UNITY_EDITOR

    [Header("Editor-only UI testing")]
    [SerializeField] private Button[] buttons;

    void Awake()
    {
        GameObject nrInput = GameObject.Find("NRInput");

        if (nrInput != null)
            nrInput.SetActive(false);
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        foreach (Button button in buttons)
        {
            if (button == null ||
                !button.gameObject.activeInHierarchy ||
                !button.interactable)
                continue;

            RectTransform rect =
                button.GetComponent<RectTransform>();

            if (rect == null)
                continue;

            Canvas canvas =
                button.GetComponentInParent<Canvas>();

            Camera eventCamera = null;

            if (canvas != null &&
                canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                eventCamera = canvas.worldCamera;

                if (eventCamera == null)
                    eventCamera = Camera.main;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(
                    rect,
                    Input.mousePosition,
                    eventCamera))
            {
                Debug.Log("EDITOR UI CLICK -> " + button.name);

                button.onClick.Invoke();
                return;
            }
        }
    }

#endif
}