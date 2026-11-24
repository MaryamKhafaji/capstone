using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Camera-attached world UI renders in the XREAL stereo view as well as the mirror.
public class HuntLoadingScreen : MonoBehaviour
{
    private bool visible;
    private GameObject screen;
    private Camera view;
    private int mask;
    private CameraClearFlags flags;
    private Color background;

    public void Show() { visible = true; Create(); }
    private void LateUpdate() { if (visible && screen == null) Create(); }
    private void Create()
    {
        if (screen != null || Camera.main == null) return;
        view = Camera.main;
        mask = view.cullingMask;
        flags = view.clearFlags;
        background = view.backgroundColor;
        screen = new GameObject("HuntLoadingScreen", typeof(RectTransform), typeof(Canvas), typeof(Image));
        screen.layer = 31;
        screen.transform.SetParent(view.transform, false);
        screen.transform.localPosition = new Vector3(0, 0, Mathf.Max(0.6f, view.nearClipPlane + 0.1f));
        screen.transform.localScale = Vector3.one * 0.001f;
        screen.GetComponent<RectTransform>().sizeDelta = new Vector2(4000, 4000);
        var canvas = screen.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = view;
        canvas.sortingOrder = 32767;
        screen.GetComponent<Image>().color = Color.black;
        screen.GetComponent<Image>().raycastTarget = false;
        var label = new GameObject("Loading", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.layer = 31;
        label.transform.SetParent(screen.transform, false);
        label.GetComponent<RectTransform>().sizeDelta = new Vector2(800, 160);
        var text = label.GetComponent<TextMeshProUGUI>();
        text.text = "Loading…";
        text.fontSize = 38;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        view.cullingMask = 1 << 31;
        view.clearFlags = CameraClearFlags.SolidColor;
        view.backgroundColor = Color.black;
    }
    public void Hide()
    {
        visible = false;
        if (view != null)
        {
            view.cullingMask = mask;
            view.clearFlags = flags;
            view.backgroundColor = background;
            view = null;
        }
        if (screen != null) Destroy(screen);
        screen = null;
    }
    private void OnDestroy() { Hide(); }
}
